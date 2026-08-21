using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TacticsAI : MonoBehaviour
{
    private AIProfile profile;
    private Player controlledPlayer;

    public IEnumerator PlayTurn(Player player, AIProfile profile)
    {
        this.profile = profile;
        controlledPlayer = player;

        while (true)
        {
            List<CandidateAction> candidates = GenerateAllCandidates(controlledPlayer, BoardState.Live);

            if (candidates.Count == 0)
                break;

            // Lookahead is much more expensive than the immediate heuristic, so only
            // spend it on each unit's most promising options rather than every candidate.
            // Grouping by unit (rather than a single global top-K) makes sure a unit whose
            // best move looks mediocre in isolation still gets a chance to prove its
            // long-term value - other units simply have strong candidates too.
            List<CandidateAction> shortlist = candidates
                .GroupBy(c => c.unit)
                .SelectMany(g => g.OrderByDescending(c => c.score).Take(Mathf.Max(1, profile.perUnitLookaheadCandidates)))
                .ToList();

            CandidateAction best = shortlist
                .OrderByDescending(c => EvaluateWithLookahead(c, BoardState.Live))
                .First();

            yield return Execute(best);
        }
    }

    /// <summary>
    /// Scores a candidate not just by its own immediate heuristic value, but by
    /// (a) how much more value I can extract from the rest of my turn once this
    /// action has happened, minus (b) how dangerous the resulting position is
    /// once each enemy gets to respond with their own best simulated turn.
    /// </summary>
    private float EvaluateWithLookahead(CandidateAction action, BoardState board)
    {
        BoardState afterAction = ApplySim(board, action);

        (BoardState ownState, float ownFollowUpScore) =
            RolloutGreedyTurn(controlledPlayer, afterAction, profile.ownRolloutSteps);

        float enemyThreat = 0f;
        foreach (Player enemy in TurnManager.Instance.players)
        {
            if (enemy == controlledPlayer)
                continue;

            (_, float enemyScore) = RolloutGreedyTurn(enemy, ownState, profile.enemyRolloutSteps);
            enemyThreat += enemyScore;
        }

        return action.score + ownFollowUpScore - enemyThreat * profile.enemyThreatWeight;
    }

    /// <summary>
    /// Greedily simulates up to <paramref name="maxSteps"/> further actions for
    /// <paramref name="player"/>, starting from <paramref name="board"/>, always taking
    /// that player's best-scoring available action at each step (the same greedy policy
    /// PlayTurn itself uses, minus the lookahead evaluation, to keep this cheap enough to
    /// call repeatedly). Used both to project my own remaining turn and to model an
    /// enemy's most plausible reply.
    /// </summary>
    private (BoardState state, float totalScore) RolloutGreedyTurn(Player player, BoardState board, int maxSteps)
    {
        BoardState state = board;
        float total = 0f;

        for (int step = 0; step < maxSteps; step++)
        {
            List<CandidateAction> candidates = GenerateAllCandidates(player, state);
            if (candidates.Count == 0)
                break;

            CandidateAction bestNext = candidates.OrderByDescending(c => c.score).First();
            state = ApplySim(state, bestNext);
            total += bestNext.score;
        }

        return (state, total);
    }

    /// <summary>
    /// Applies a candidate action to a BoardState and returns the resulting state,
    /// mirroring Execute()'s real mutation logic but touching no GameObjects at all -
    /// this is what makes it safe to call many times per decision while searching.
    /// </summary>
    private BoardState ApplySim(BoardState board, CandidateAction action)
    {
        Unit unit = action.unit;
        Tile from = board.GetTile(unit);
        Tile to = action.moveTile;
        BoardState next = board;

        if (action.kind == ActionKind.DoNothing)
            return next.WithDeactivated(unit);

        if (to != from)
        {
            next = next.WithMove(unit, from, to);

            if (to.city != null && next.GetOwner(to.city) != unit.owner)
                next = next.WithCityClaim(to.city, unit.owner);
        }

        if (action.kind == ActionKind.Attack && action.target != null && next.IsAlive(action.target))
        {
            Unit target = action.target;
            Tile targetTile = next.GetTile(target);

            (int dmg, int retaliation) = PredictDamage(unit, target, next);

            int newTargetHealth = next.GetHealth(target) - dmg;
            bool killed = newTargetHealth <= 0;

            next = next.WithDamage(target, newTargetHealth);
            next = next.WithAttacked(unit);

            bool meleeAttack = unit.data.attackRange == 1;

            if (killed)
            {
                if (meleeAttack)
                {
                    next = next.WithMove(unit, to, targetTile);

                    if (targetTile.city != null && next.GetOwner(targetTile.city) != unit.owner)
                        next = next.WithCityClaim(targetTile.city, unit.owner);
                }
            }
            else if (Utils.IsWithinDistance(targetTile.gridPosition, to.gridPosition, target.data.attackRange))
            {
                int newAttackerHealth = next.GetHealth(unit) - retaliation;
                next = next.WithDamage(unit, newAttackerHealth);
            }
        }

        return next;
    }

    private List<CandidateAction> GenerateAllCandidates(Player player, BoardState board)
    {
        return player.units
            .Where(u => IsUnitAvailable(u, board))
            .SelectMany(u => GenerateCandidates(u, board))
            .ToList();
    }

    private bool IsUnitAvailable(Unit unit, BoardState board)
    {
        return unit != null
               && board.IsAlive(unit)
               && board.IsActive(unit)
               && !(board.HasMoved(unit) && board.HasAttacked(unit));
    }

    private IEnumerable<CandidateAction> GenerateCandidates(Unit unit, BoardState board)
    {
        bool staticUnit = unit.data.skills.Any(s => s == Skill.Static);
        Tile currentTile = board.GetTile(unit);

        List<Tile> possiblePositions = new List<Tile> { currentTile };

        if (!board.HasMoved(unit))
        {
            possiblePositions.AddRange(
                GridManager.Instance
                    .GetTilesInRange(currentTile, unit.data.moveRange)
                    .Where(t => board.GetOccupant(t) == null)
            );
        }

        foreach (Tile position in possiblePositions)
        {
            bool moved = position != currentTile;

            if (moved)
            {
                float score = ScoreMove(unit, currentTile, position, board);

                yield return new CandidateAction
                {
                    unit = unit,
                    moveTile = position,
                    target = null,
                    kind = ActionKind.MoveOnly,
                    score = score
                };
            }
            else
            {
                yield return new CandidateAction
                {
                    unit = unit,
                    moveTile = currentTile,
                    target = null,
                    kind = ActionKind.DoNothing,
                    score = ScoreMove(unit, currentTile, currentTile, board)
                };
            }

            if (board.HasAttacked(unit))
                continue;

            if (moved && staticUnit)
                continue;

            foreach (Tile attackTile in GridManager.Instance.GetTilesInRange(position, unit.data.attackRange))
            {
                Unit target = board.GetOccupant(attackTile);

                if (target == null || target.owner == unit.owner || !board.IsAlive(target))
                    continue;

                float score = ScoreAttack(unit, position, target, board);

                yield return new CandidateAction
                {
                    unit = unit,
                    moveTile = position,
                    target = target,
                    kind = ActionKind.Attack,
                    score = score
                };
            }
        }
    }

    private float ScoreAttack(Unit unit, Tile from, Unit target, BoardState board)
    {
        (int damage, int retaliation) = PredictDamage(unit, target, board);

        bool kills = board.GetHealth(target) - damage <= 0;

        float score = 0f;

        score += damage * profile.damageWeight;

        if (kills)
        {
            score += target.data.cost * profile.killWeight;
        }

        bool canRetaliate = !kills && CanRetaliate(target, from, board);

        if (canRetaliate)
        {
            score -= retaliation * profile.retaliationWeight;
        }

        score += ScorePosition(unit, from, board);

        Tile targetTile = board.GetTile(target);

        if (kills &&
            unit.data.attackRange == 1 &&
            targetTile != null &&
            targetTile.city != null &&
            board.GetOwner(targetTile.city) != unit.owner)
        {
            score += profile.cityCaptureWeight;
        }

        if (ExposesToLethalCounter(unit, from, kills ? target : null, board))
        {
            score -= unit.data.cost * profile.survivalWeight;
        }

        score += ScoreCityProgress(board.GetTile(unit), from, unit.owner, board);

        return score;
    }

    private float ScoreMove(Unit unit, Tile from, Tile to, BoardState board)
    {
        float score = 0f;

        score += ScoreCityProgress(from, to, unit.owner, board);

        if (to.city != null && board.GetOwner(to.city) != unit.owner)
        {
            score += profile.cityCaptureWeight;
        }

        score += ScorePosition(unit, to, board);

        if (ExposesToLethalCounter(unit, to, null, board))
        {
            score -= unit.data.cost * profile.survivalWeight;
        }

        return score;
    }

    private float ScoreCityProgress(Tile from, Tile to, Player owner, BoardState board)
    {
        int oldDistance = DistanceToNearestUncapturedCity(from, owner, board);
        int newDistance = DistanceToNearestUncapturedCity(to, owner, board);

        if (oldDistance == int.MaxValue || newDistance == int.MaxValue)
            return 0f;

        int improvement = oldDistance - newDistance;

        if (improvement <= 0)
            return 0f;

        return improvement * profile.cityProgressWeight;
    }

    private float ScorePosition(Unit unit, Tile tile, BoardState board)
    {
        float score = 0f;

        foreach (Player enemy in TurnManager.Instance.players)
        {
            if (enemy == unit.owner)
                continue;

            foreach (Unit enemyUnit in enemy.units)
            {
                if (enemyUnit == null || !board.IsAlive(enemyUnit))
                    continue;

                int distance = Utils.GridDistance(
                    tile.gridPosition,
                    board.GetTile(enemyUnit).gridPosition
                );

                // being within attack range next turn is good
                if (distance <= unit.data.attackRange)
                {
                    score += profile.positionWeight;
                }

                // melee units benefit from moving toward enemies
                if (unit.data.attackRange == 1 &&
                    distance <= unit.data.moveRange + 1)
                {
                    score += profile.positionWeight * 0.5f;
                }
            }
        }

        return score;
    }

    private bool ExposesToLethalCounter(Unit unit, Tile destination, Unit justKilled, BoardState board)
    {
        foreach (Player enemy in TurnManager.Instance.players)
        {
            if (enemy == unit.owner)
                continue;

            foreach (Unit enemyUnit in enemy.units)
            {
                if (enemyUnit == null ||
                    !board.IsAlive(enemyUnit) ||
                    enemyUnit == justKilled)
                {
                    continue;
                }

                if (!CanReachAndAttack(enemyUnit, destination, board))
                    continue;

                (int damage, int _) = PredictDamage(enemyUnit, unit, board);

                if (damage >= board.GetHealth(unit))
                    return true;
            }
        }

        return false;
    }

    private bool CanReachAndAttack(Unit enemy, Tile targetTile, BoardState board)
    {
        int distance = Utils.GridDistance(
            board.GetTile(enemy).gridPosition,
            targetTile.gridPosition
        );

        return distance <= enemy.data.moveRange + enemy.data.attackRange;
    }

    private bool CanRetaliate(Unit defender, Tile attackerPosition, BoardState board)
    {
        int distance = Utils.GridDistance(
            board.GetTile(defender).gridPosition,
            attackerPosition.gridPosition
        );

        return distance <= defender.data.attackRange;
    }

    private int DistanceToNearestUncapturedCity(Tile from, Player owner, BoardState board)
    {
        int bestDistance = int.MaxValue;

        foreach (City city in WorldPopulationManager.Instance.allCities)
        {
            if (city == null || board.GetOwner(city) == owner)
                continue;

            int distance = Utils.GridDistance(
                from.gridPosition,
                city.centerTile.gridPosition
            );

            if (distance < bestDistance)
                bestDistance = distance;
        }

        return bestDistance;
    }

    private (int, int) PredictDamage(Unit attacker, Unit defender, BoardState board)
    {
        return CombatMath.CalculateDamage(
            attacker.data.attackPower, board.GetHealth(attacker), attacker.data.maxHealth,
            defender.data.defensePower, board.GetHealth(defender), defender.data.maxHealth
        );
    }

    private IEnumerator Execute(CandidateAction action)
    {
        bool visible = IsVisibleToLocalPlayer(action);

        Tile targetTile = action.target != null
            ? action.target.currentTile
            : null;

        bool meleeAttack =
            action.kind == ActionKind.Attack &&
            action.unit.data.attackRange == 1;

        if (action.kind == ActionKind.DoNothing)
        {
            action.unit.Deactivate();
            yield return new WaitForSeconds(0.1f);
        }

        // move first
        if (action.moveTile != action.unit.currentTile)
        {
            action.unit.MoveTo(action.moveTile);

            if (action.moveTile.city != null)
            {
                action.moveTile.city.Claim(action.unit.owner);
            }
        }

        // then attack
        if (action.kind == ActionKind.Attack &&
            action.target != null &&
            action.target.isAlive)
        {
            action.unit.Attack(action.target);

            if (meleeAttack &&
                !action.target.isAlive &&
                targetTile != null)
            {
                action.unit.MoveTo(targetTile);

                if (targetTile.city != null)
                {
                    targetTile.city.Claim(action.unit.owner);
                }
            }
        }

        if (visible)
            yield return new WaitForSeconds(0.35f);
    }

    private bool IsVisibleToLocalPlayer(CandidateAction action)
    {
        return true;
    }
}
