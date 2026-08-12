using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnAI : MonoBehaviour
{
    public Player controlledPlayer;
    [SerializeField] private float cityPullWeight = 2f;

    public IEnumerator PlayTurn()
    {
        List<Unit> active = controlledPlayer.units
            .Where(u => u.isAlive && u.isActive && !(u.hasMoved && u.hasAttacked))
            .ToList();

        while (active.Count > 0)
        {
            CandidateAction? best = null;

            foreach (Unit unit in active)
                foreach (CandidateAction c in GenerateCandidates(unit))
                    if (best == null || c.score > best.Value.score)
                        best = c;

            if (best == null || best.Value.score <= 0f) break;

            yield return Execute(best.Value);
            active.RemoveAll(u => !u.isAlive || !u.isActive || (u.hasMoved && u.hasAttacked));
        }

        HandleCitySpawns();

        Debug.Log("finished");
    }

    private List<CandidateAction> GenerateCandidates(Unit unit)
    {
        var results = new List<CandidateAction>();
        bool deactivatesOnMove = unit.data.skills.Any(s => s == Skill.Static);

        var moveTiles = new List<Tile> { unit.currentTile };
        if (!unit.hasMoved)
            moveTiles.AddRange(GridManager.Instance
                .GetTilesInRange(unit.currentTile, unit.data.moveRange)
                .Where(t => t.currentUnit == null));

        foreach (Tile moveTile in moveTiles)
        {
            bool moved = moveTile != unit.currentTile;

            if (moved)
                results.Add(new CandidateAction
                {
                    unit = unit,
                    moveTile = moveTile,
                    target = null,
                    kind = ActionKind.MoveOnly,
                    score = ScoreMove(unit, moveTile)
                });

            if (unit.hasAttacked || (moved && deactivatesOnMove)) continue;

            foreach (Tile t in GridManager.Instance.GetTilesInRange(moveTile, unit.data.attackRange))
            {
                if (t.currentUnit == null || t.currentUnit.owner == unit.owner) continue;
                results.Add(new CandidateAction
                {
                    unit = unit,
                    moveTile = moveTile,
                    target = t.currentUnit,
                    kind = ActionKind.Attack,
                    score = ScoreAttack(unit, moveTile, t.currentUnit)
                });
            }
        }
        return results;
    }

    private float ScoreAttack(Unit unit, Tile from, Unit target)
    {
        var (dmg, retaliation) = PredictDamage(unit, target);
        bool kills = target.currentHealth - dmg <= 0;

        float score = kills ? target.data.cost : dmg;
        if (!kills) score -= retaliation * 0.5f;
        if (kills && target.homeCity != null) score += 5f;
        if (ExposesToLethalCounter(unit, from, kills ? target : null)) score -= unit.data.cost;
        return score;
    }

    private float ScoreMove(Unit unit, Tile to)
    {
        float score = 0f;
        // if is closer to uncaptured city than before, increase the value, the higher the better.
        if (to.city != null && to.city.owner != unit.owner) score += 4f;

        var (nearestCity, distance) = NearestUncapturedCity(to, unit.owner);
        if (nearestCity != null)
            score += cityPullWeight / (1f + distance);

        if (ExposesToLethalCounter(unit, to, null)) score -= unit.data.cost;
        return score;
    }

    private bool ExposesToLethalCounter(Unit unit, Tile to, Unit justKilled)
    {
        foreach (Player enemy in TurnManager.Instance.players)
        {
            if (enemy == unit.owner) continue;
            foreach (Unit e in enemy.units)
            {
                if (e == justKilled || !e.isAlive) continue;
                if (Utils.GridDistance(e.currentTile.gridPosition, to.gridPosition) > e.data.moveRange + e.data.attackRange) continue;
                var (dmg, _) = PredictDamage(e, unit);
                if (dmg >= unit.currentHealth) return true;
            }
        }
        return false;
    }

    private (City city, int distance) NearestUncapturedCity(Tile from, Player owner)
    {
        City nearest = null;
        int bestDist = int.MaxValue;

        foreach (City city in WorldPopulationManager.Instance.allCities)
        {
            if (city.owner == owner) continue;
            int d = Utils.GridDistance(from.gridPosition, city.centerTile.gridPosition);
            if (d < bestDist) { bestDist = d; nearest = city; }
        }
        return (nearest, bestDist);
    }

    private void HandleCitySpawns()
    {
        List<City> spawnCities = controlledPlayer.cities.FindAll(c => c.centerTile.currentUnit == null && c.units.Count < c.level + 1);
        foreach (City city in spawnCities)
        {
            FactionUnit unit = BestAffordableUnit(city, spawnCities.Count);
            GameObject unitPrefab = unit.prefab;
            if (unitPrefab != null)
            {
                int cost = unit.unitData.cost;

                if (controlledPlayer.stars >= cost)
                {
                    city.SpawnUnit(unitPrefab, cost);
                }
            }
        }
    }

    private FactionUnit BestAffordableUnit(City city, int spawnCitiesCount)
    {
        FactionUnit best = null;
        float bestScore = float.MinValue;

        int budget = Mathf.RoundToInt(controlledPlayer.stars / spawnCitiesCount);

        // actually it should spawn units to counter your most nearby units
        // nope. it should score units based on how useful they would be as counters

        foreach (FactionUnit candidate in controlledPlayer.faction.availableUnits)
        {
            if (candidate.unitData.cost > controlledPlayer.stars) continue;

            if (candidate.unitData.cost > bestScore) { bestScore = candidate.unitData.cost; best = candidate; }
        }

        return best;
    }

    private IEnumerator Execute(CandidateAction a)
    {
        bool visible = IsVisibleToLocalPlayer(a);

        if (a.moveTile != a.unit.currentTile) a.unit.MoveTo(a.moveTile);
        if (a.kind == ActionKind.Attack) a.unit.Attack(a.target);

        if (visible)
            yield return new WaitForSeconds(0.35f);
    }

    private (int, int) PredictDamage(Unit attacker, Unit defender) {
        return attacker.CalculateDamage(attacker, defender);
    }

    private bool IsVisibleToLocalPlayer(CandidateAction a) => true;
}