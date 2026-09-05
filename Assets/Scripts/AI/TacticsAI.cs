using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TacticsAI : MonoBehaviour
{
    private AIProfile profile;
    private Player controlledPlayer;
    private Player humanPlayer;

    private readonly List<CandidateAction> _allCandidates = new List<CandidateAction>(64);
    private readonly List<(int start, int count)> _unitRanges = new List<(int start, int count)>(16);
    private readonly List<CandidateAction> _shortlist = new List<CandidateAction>(32);
    private readonly List<Tile> _scratchPositions = new List<Tile>(16);
    private readonly System.Diagnostics.Stopwatch _frameBudgetTimer = new System.Diagnostics.Stopwatch();

    private CandidateAction[] _topKScratch;

    private static readonly WaitForSeconds ActionAnimationWait = new WaitForSeconds(0.3f);

    private void Start()
    {
        humanPlayer = TurnManager.Instance.players.Find(p => !p.isAI);
    }

    public IEnumerator PlayTurn(Player player, AIProfile profile)
    {
        this.profile = profile;
        controlledPlayer = player;

        while (true)
        {
            GenerateAllCandidates(controlledPlayer, BoardState.Live);

            if (_allCandidates.Count == 0)
                break;

            int perUnitCount = Mathf.Max(1, profile.perUnitLookaheadCandidates);
            SelectShortlist(perUnitCount, _shortlist);

            CandidateAction best = default;
            float bestScore = float.NegativeInfinity;

            _frameBudgetTimer.Restart();

            for (int i = 0; i < _shortlist.Count; i++)
            {
                float score = EvaluateWithLookahead(_shortlist[i], BoardState.Live);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = _shortlist[i];
                }

                bool moreToEvaluate = i < _shortlist.Count - 1;
                if (moreToEvaluate && _frameBudgetTimer.Elapsed.TotalMilliseconds >= profile.LookaheadFrameBudgetMs)
                {
                    yield return null;
                    _frameBudgetTimer.Restart();
                }
            }

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
        int checkpoint = board.Checkpoint();

        try
        {
            ApplySim(board, action);

            float ownFollowUpScore =
                RolloutGreedyTurn(
                    controlledPlayer,
                    board,
                    profile.ownRolloutSteps);

            float enemyThreat = 0f;

            foreach (Player enemy in TurnManager.Instance.players)
            {
                if (enemy == controlledPlayer)
                    continue;

                enemyThreat += RolloutGreedyTurn(
                    enemy,
                    board,
                    profile.enemyRolloutSteps);
            }

            return action.score +
                   ownFollowUpScore -
                   enemyThreat * profile.enemyThreatWeight;
        }
        finally
        {
            board.Rollback(checkpoint);
        }
    }

    /// <summary>
    /// Greedily simulates up to <paramref name="maxSteps"/> further actions for
    /// <paramref name="player"/>, starting from <paramref name="board"/>, always taking
    /// that player's best-scoring available action at each step (the same greedy policy
    /// PlayTurn itself uses, minus the lookahead evaluation, to keep this cheap enough to
    /// call repeatedly). Used both to project my own remaining turn and to model an
    /// enemy's most plausible reply.
    /// </summary>
    private float RolloutGreedyTurn(
        Player player,
        BoardState board,
        int maxSteps)
    {
        float total = 0f;

        for (int step = 0; step < maxSteps; step++)
        {
            GenerateAllCandidates(player, board);

            if (_allCandidates.Count == 0)
                break;

            CandidateAction bestNext = _allCandidates[0];

            for (int i = 1; i < _allCandidates.Count; i++)
            {
                if (_allCandidates[i].score > bestNext.score)
                    bestNext = _allCandidates[i];
            }

            ApplySim(board, bestNext);
            total += bestNext.score;
        }

        return total;
    }

    /// <summary>
    /// Applies a candidate action to a BoardState and returns the resulting state,
    /// mirroring Execute()'s real mutation logic but touching no GameObjects at all -
    /// this is what makes it safe to call many times per decision while searching.
    /// </summary>
    private void ApplySim(BoardState board, CandidateAction action)
    {
        Unit unit = action.unit;

        Tile from = board.GetTile(unit);
        Tile to = action.moveTile;

        if (action.kind == ActionKind.DoNothing)
        {
            board.WithDeactivated(unit);
            return;
        }

        if (to != from)
        {
            board.WithMove(unit, from, to);

            if (to.city != null &&
                board.GetOwner(to.city) != unit.owner)
            {
                board.WithPendingCityCapture(to.city, unit);
            }
        }

        if (action.kind != ActionKind.Attack ||
            action.target == null ||
            !board.IsAlive(action.target))
        {
            return;
        }

        Unit target = action.target;
        Tile targetTile = board.GetTile(target);

        (int damage, int retaliation) =
            PredictDamage(unit, target, board);

        int newTargetHealth =
            board.GetHealth(target) - damage;

        bool killed = newTargetHealth <= 0;

        board.WithDamage(target, newTargetHealth);
        board.WithAttacked(unit);

        bool meleeAttack = unit.data.attackRange == 1;

        if (killed)
        {
            if (meleeAttack)
            {
                board.WithMove(unit, to, targetTile);

                if (targetTile.city != null &&
                    board.GetOwner(targetTile.city) != unit.owner)
                {
                    board.WithPendingCityCapture(targetTile.city, unit);
                }
            }
        }
        else if (Utils.IsWithinDistance(
                     targetTile.gridPosition,
                     to.gridPosition,
                     target.data.attackRange))
        {
            int newAttackerHealth =
                board.GetHealth(unit) - retaliation;

            board.WithDamage(unit, newAttackerHealth);
        }
    }

    private void GenerateAllCandidates(Player player, BoardState board)
    {
        _allCandidates.Clear();
        _unitRanges.Clear();

        foreach (Unit unit in player.units)
        {
            if (!IsUnitAvailable(unit, board))
                continue;

            int start = _allCandidates.Count;
            AppendCandidatesForUnit(unit, board, _allCandidates);
            int count = _allCandidates.Count - start;

            if (count > 0)
                _unitRanges.Add((start, count));
        }
    }

    private bool IsUnitAvailable(Unit unit, BoardState board)
    {
        return unit != null
               && board.IsAlive(unit)
               && board.IsActive(unit)
               && !(board.HasMoved(unit) && board.HasAttacked(unit));
    }

    private void AppendCandidatesForUnit(Unit unit, BoardState board, List<CandidateAction> output)
    {
        bool staticUnit = HasSkill(unit.data.skills, Skill.Static);
        Tile currentTile = board.GetTile(unit);

        _scratchPositions.Clear();
        _scratchPositions.Add(currentTile);

        if (!board.HasMoved(unit))
        {
            foreach (Tile tile in GridManager.Instance.GetTilesInRange(currentTile, unit.data.moveRange))
            {
                if (board.GetOccupant(tile) == null)
                    _scratchPositions.Add(tile);
            }
        }

        for (int p = 0; p < _scratchPositions.Count; p++)
        {
            Tile position = _scratchPositions[p];
            bool moved = position != currentTile;

            if (moved)
            {
                output.Add(new CandidateAction
                {
                    unit = unit,
                    moveTile = position,
                    target = null,
                    kind = ActionKind.MoveOnly,
                    score = ScoreMove(unit, currentTile, position, board)
                });
            }
            else
            {
                output.Add(new CandidateAction
                {
                    unit = unit,
                    moveTile = currentTile,
                    target = null,
                    kind = ActionKind.DoNothing,
                    score = ScoreMove(unit, currentTile, currentTile, board)
                });
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

                output.Add(new CandidateAction
                {
                    unit = unit,
                    moveTile = position,
                    target = target,
                    kind = ActionKind.Attack,
                    score = ScoreAttack(unit, position, target, board)
                });
            }
        }
    }

    // Manual replacement for `skills.Any(s => s == skill)`. Takes IReadOnlyList<Skill> so it
    // works for either an array or a List<Skill> field on UnitData without boxing an
    // enumerator. Adjust the parameter type if unit.data.skills isn't index-accessible.
    private static bool HasSkill(IReadOnlyList<Skill> skills, Skill skill)
    {
        if (skills == null)
            return false;

        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] == skill)
                return true;
        }

        return false;
    }

    private void EnsureTopKScratch(int k)
    {
        if (_topKScratch == null || _topKScratch.Length < k)
            _topKScratch = new CandidateAction[Mathf.Max(1, k)];
    }

    private void SelectShortlist(int perUnitCount, List<CandidateAction> shortlistOut)
    {
        shortlistOut.Clear();
        EnsureTopKScratch(perUnitCount);

        for (int r = 0; r < _unitRanges.Count; r++)
        {
            (int start, int count) = _unitRanges[r];
            int kept = SelectTopKForRange(start, count, perUnitCount);

            for (int i = 0; i < kept; i++)
                shortlistOut.Add(_topKScratch[i]);
        }
    }

    private int SelectTopKForRange(int start, int count, int k)
    {
        int kept = 0;

        for (int i = 0; i < count; i++)
        {
            CandidateAction candidate = _allCandidates[start + i];

            if (kept < k)
            {
                int insertAt = kept;
                while (insertAt > 0 && _topKScratch[insertAt - 1].score < candidate.score)
                {
                    _topKScratch[insertAt] = _topKScratch[insertAt - 1];
                    insertAt--;
                }
                _topKScratch[insertAt] = candidate;
                kept++;
            }
            else if (candidate.score > _topKScratch[kept - 1].score)
            {
                int insertAt = kept - 1;
                while (insertAt > 0 && _topKScratch[insertAt - 1].score < candidate.score)
                {
                    _topKScratch[insertAt] = _topKScratch[insertAt - 1];
                    insertAt--;
                }
                _topKScratch[insertAt] = candidate;
            }
        }

        return kept;
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

            yield return null;
        }

        // move first
        if (action.moveTile != action.unit.currentTile)
        {
            action.unit.MoveTo(action.moveTile);
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
            }
        }

        if (visible) yield return ActionAnimationWait;
        else yield return null;
    }

    private bool IsVisibleToLocalPlayer(CandidateAction action)
    {
        if (humanPlayer.visibleTiles.IsVisible(action.unit.currentTile))
            return true;

        if (humanPlayer.visibleTiles.IsVisible(action.moveTile))
            return true;

        return false;
    }
}
