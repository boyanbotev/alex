using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnAI : MonoBehaviour
{
    public Player controlledPlayer;

    [Header("Strategic Weights")]
    [SerializeField] private float cityCaptureWeight = 40f;
    [SerializeField] private float cityProgressWeight = 3f;
    [SerializeField] private float damageWeight = 1f;
    [SerializeField] private float killWeight = 5f;
    [SerializeField] private float retaliationWeight = 1f;
    [SerializeField] private float survivalWeight = 4f;
    [SerializeField] private float positionWeight = 1f;
    [SerializeField] private float counterWeight = 8f;

    [Header("Spawn Weights")]
    [SerializeField] private float nearbyEnemyWeight = 3f;
    [SerializeField] private float nearbyCityWeight = 2f;
    [SerializeField] private float cityDefenseWeight = 5f;
    [SerializeField] private float expansionWeight = 4f;

    [Header("AI Personality")]
    [SerializeField] private float aggression = 1f;
    [SerializeField] private float expansionism = 1f;
    [SerializeField] private float defence = 1f;

    public IEnumerator PlayTurn()
    {
        while (true)
        {
            List<CandidateAction> candidates = controlledPlayer.units
                .Where(IsUnitAvailable)
                .SelectMany(GenerateCandidates)
                .ToList();

            if (candidates.Count == 0)
                break;

            CandidateAction best = candidates
                .OrderByDescending(a => a.score)
                .FirstOrDefault();

            if (best.score <= 0f)
                break;

            yield return Execute(best);
        }

        HandleCitySpawns();

        Debug.Log($"{controlledPlayer.factionName} AI finished turn.");
    }

    private bool IsUnitAvailable(Unit unit)
    {
        return unit != null
               && unit.isAlive
               && unit.isActive
               && !(unit.hasMoved && unit.hasAttacked);
    }

    private IEnumerable<CandidateAction> GenerateCandidates(Unit unit)
    {
        bool staticUnit = unit.data.skills.Any(s => s == Skill.Static);

        List<Tile> possiblePositions = new List<Tile>
        {
            unit.currentTile
        };

        if (!unit.hasMoved)
        {
            possiblePositions.AddRange(
                GridManager.Instance
                    .GetTilesInRange(unit.currentTile, unit.data.moveRange)
                    .Where(t => t.currentUnit == null)
            );
        }

        foreach (Tile position in possiblePositions)
        {
            bool moved = position != unit.currentTile;

            if (moved)
            {
                float score = ScoreMove(unit, unit.currentTile, position);

                yield return new CandidateAction
                {
                    unit = unit,
                    moveTile = position,
                    target = null,
                    kind = ActionKind.MoveOnly,
                    score = score
                };
            }

            if (unit.hasAttacked)
                continue;

            if (moved && staticUnit)
                continue;

            foreach (Tile attackTile in GridManager.Instance.GetTilesInRange(
                         position,
                         unit.data.attackRange))
            {
                if (attackTile.currentUnit == null)
                    continue;

                Unit target = attackTile.currentUnit;

                if (target.owner == unit.owner)
                    continue;

                float score = ScoreAttack(
                    unit,
                    position,
                    target
                );

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

    private float ScoreAttack(Unit unit, Tile from, Unit target)
    {
        (int damage, int retaliation) = PredictDamage(unit, target);

        bool kills = target.currentHealth - damage <= 0;

        float score = 0f;

        score += damage * damageWeight * aggression;

        if (kills)
        {
            score += target.data.cost * killWeight * aggression;
        }

        bool canRetaliate = !kills &&
                            CanRetaliate(target, from);

        if (canRetaliate)
        {
            score -= retaliation * retaliationWeight;
        }

        score += ScorePosition(unit, from);

        if (kills &&
            unit.data.attackRange == 1 &&
            target.currentTile != null &&
            target.currentTile.city != null &&
            target.currentTile.city.owner != unit.owner)
        {
            score += cityCaptureWeight * expansionism;
        }

        if (ExposesToLethalCounter(unit, from, kills ? target : null))
        {
            score -= unit.data.cost * survivalWeight;
        }

        score += ScoreCityProgress(unit.currentTile, from, unit.owner);

        return score;
    }

    private float ScoreMove(Unit unit, Tile from, Tile to)
    {
        float score = 0f;

        score += ScoreCityProgress(from, to, unit.owner);

        if (to.city != null && to.city.owner != unit.owner)
        {
            score += cityCaptureWeight * expansionism;
        }

        score += ScorePosition(unit, to);

        if (ExposesToLethalCounter(unit, to, null))
        {
            score -= unit.data.cost * survivalWeight;
        }

        return score;
    }

    private float ScoreCityProgress(Tile from, Tile to, Player owner)
    {
        int oldDistance = DistanceToNearestUncapturedCity(from, owner);
        int newDistance = DistanceToNearestUncapturedCity(to, owner);

        if (oldDistance == int.MaxValue || newDistance == int.MaxValue)
            return 0f;

        int improvement = oldDistance - newDistance;

        if (improvement <= 0)
            return 0f;

        return improvement * cityProgressWeight * expansionism;
    }

    private float ScorePosition(Unit unit, Tile tile)
    {
        float score = 0f;

        foreach (Player enemy in TurnManager.Instance.players)
        {
            if (enemy == unit.owner)
                continue;

            foreach (Unit enemyUnit in enemy.units)
            {
                if (enemyUnit == null || !enemyUnit.isAlive)
                    continue;

                int distance = Utils.GridDistance(
                    tile.gridPosition,
                    enemyUnit.currentTile.gridPosition
                );

                // being within attack range next turn is good
                if (distance <= unit.data.attackRange)
                {
                    score += positionWeight;
                }

                // melee units benefit from moving toward enemies
                if (unit.data.attackRange == 1 &&
                    distance <= unit.data.moveRange + 1)
                {
                    score += positionWeight * 0.5f;
                }
            }
        }

        return score;
    }

    private bool ExposesToLethalCounter(Unit unit, Tile destination, Unit justKilled)
    {
        foreach (Player enemy in TurnManager.Instance.players)
        {
            if (enemy == unit.owner)
                continue;

            foreach (Unit enemyUnit in enemy.units)
            {
                if (enemyUnit == null ||
                    !enemyUnit.isAlive ||
                    enemyUnit == justKilled)
                {
                    continue;
                }

                if (!CanReachAndAttack(enemyUnit, destination))
                    continue;

                (int damage, int _) = PredictDamage(enemyUnit, unit);

                if (damage >= unit.currentHealth)
                    return true;
            }
        }

        return false;
    }

    private bool CanReachAndAttack(Unit enemy, Tile targetTile)
    {
        int distance = Utils.GridDistance(
            enemy.currentTile.gridPosition,
            targetTile.gridPosition
        );

        return distance <= enemy.data.moveRange + enemy.data.attackRange;
    }

    private bool CanRetaliate(Unit defender, Tile attackerPosition)
    {
        int distance = Utils.GridDistance(
            defender.currentTile.gridPosition,
            attackerPosition.gridPosition
        );

        return distance <= defender.data.attackRange;
    }

    private int DistanceToNearestUncapturedCity(Tile from, Player owner)
    {
        int bestDistance = int.MaxValue;

        foreach (City city in WorldPopulationManager.Instance.allCities)
        {
            if (city == null || city.owner == owner)
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


    private IEnumerator Execute(CandidateAction action)
    {
        bool visible = IsVisibleToLocalPlayer(action);

        Tile targetTile = action.target != null
            ? action.target.currentTile
            : null;

        bool meleeAttack =
            action.kind == ActionKind.Attack &&
            action.unit.data.attackRange == 1;

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

    private void HandleCitySpawns()
    {
        List<City> spawnCities = controlledPlayer.cities
            .Where(c =>
                c.centerTile.currentUnit == null &&
                c.units.Count < c.level + 1)
            .ToList();

        foreach (City city in spawnCities)
        {
            FactionUnit bestUnit = BestAffordableUnit(city, spawnCities.Count);

            if (bestUnit == null)
                continue;

            if (controlledPlayer.stars < bestUnit.unitData.cost)
                continue;

            city.SpawnUnit(
                bestUnit.prefab,
                bestUnit.unitData.cost
            );
        }
    }

    private FactionUnit BestAffordableUnit(City city, int spawnCitiesCount)
    {
        FactionUnit best = null;
        float bestScore = float.MinValue;

        foreach (FactionUnit candidate in controlledPlayer.faction.availableUnits)
        {
            if (candidate.unitData.cost > controlledPlayer.stars)
                continue;

            float score = ScoreUnitForCity(
                candidate,
                city
            );

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private float ScoreUnitForCity(FactionUnit candidate, City city)
    {
        float score = 0f;

        UnitData data = candidate.unitData;

        List<Unit> nearbyEnemies = GetNearbyEnemies(city);

        foreach (Unit enemy in nearbyEnemies)
        {
            int distance = Utils.GridDistance(
                city.centerTile.gridPosition,
                enemy.currentTile.gridPosition
            );

            float proximity =
                Mathf.Max(0f, data.moveRange + data.attackRange - distance);

            score += proximity * nearbyEnemyWeight;

            score += CalculateCounterStrength(candidate, enemy);
        }

        if (nearbyEnemies.Count > 0)
        {
            score += cityDefenseWeight * defence;
        }

        if (HasUncapturedCityNearby(city))
        {
            score += expansionWeight *
                     expansionism *
                     data.moveRange;
        }

        score += data.moveRange * 0.5f;

        score += data.maxHealth * 0.1f;

        score += data.cost * 0.1f;

        return score;
    }

    private List<Unit> GetNearbyEnemies(City city)
    {
        List<Unit> enemies = new List<Unit>();

        foreach (Player player in TurnManager.Instance.players)
        {
            if (player == controlledPlayer)
                continue;

            foreach (Unit unit in player.units)
            {
                if (unit == null || !unit.isAlive)
                    continue;

                int distance = Utils.GridDistance(
                    city.centerTile.gridPosition,
                    unit.currentTile.gridPosition
                );

                if (distance <= 3)
                {
                    enemies.Add(unit);
                }
            }
        }

        return enemies;
    }

    private bool HasUncapturedCityNearby(City city)
    {
        foreach (City otherCity in WorldPopulationManager.Instance.allCities)
        {
            if (otherCity == city ||
                otherCity.owner == controlledPlayer)
                continue;

            int distance = Utils.GridDistance(
                city.centerTile.gridPosition,
                otherCity.centerTile.gridPosition
            );

            if (distance <= 6)
                return true;
        }

        return false;
    }

    float CalculateCounterStrength(FactionUnit unit, Unit enemy)
    {
        if (unit.unitData == enemy.data) return 0;

        else return unit.unitData.counters.FirstOrDefault(c => c.unit == enemy.data).strength * counterWeight;
    }

    private (int, int) PredictDamage(Unit attacker, Unit defender)
    {
        return attacker.CalculateDamage(attacker, defender);
    }

    private bool IsVisibleToLocalPlayer(CandidateAction action)
    {
        return true;
    }
}