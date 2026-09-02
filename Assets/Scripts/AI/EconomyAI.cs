using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EconomyAI : MonoBehaviour
{
    private AIProfile profile;
    private Player controlledPlayer;

    public void HandleEconomy(Player controlledPlayer, AIProfile profile)
    {
        this.controlledPlayer = controlledPlayer;
        this.profile = profile;

        while (true)
        {
            List<EconomyCandidateAction> candidates = GenerateEconomyCandidates()
                .Where(c => c.cost <= controlledPlayer.stars)
                .ToList();

            if (candidates.Count == 0) break;

            EconomyCandidateAction best = candidates.OrderByDescending(c => c.score).First();
            if (best.score <= 0f) break;

            ExecuteEconomyAction(best);
        }
    }

    private List<EconomyCandidateAction> GenerateEconomyCandidates()
    {
        List<EconomyCandidateAction> immediateCandidates = new List<EconomyCandidateAction>();
        immediateCandidates.AddRange(GenerateBuildingCandidates());
        immediateCandidates.AddRange(GenerateSpawnCandidates());

        List<EconomyCandidateAction> researchCandidates = GenerateResearchCandidates().ToList();

        List<EconomyCandidateAction> all = new List<EconomyCandidateAction>(immediateCandidates);
        all.AddRange(researchCandidates);
        return all;
    }

    private IEnumerable<EconomyCandidateAction> GenerateBuildingCandidates()
    {
        foreach (City city in controlledPlayer.cities)
        {
            foreach (BuildingData building in controlledPlayer.faction.availableBuildings)
            {
                if (!controlledPlayer.techState.CanBuild(building)) continue;

                Tile tile = FindBestBuildTile(building, city);
                if (tile == null) continue;

                yield return new EconomyCandidateAction
                {
                    kind = EconomyActionKind.PlaceBuilding,
                    building = building,
                    buildTile = tile,
                    city = city,
                    cost = building.cost,
                    score = ScoreBuilding(building, city)
                };
            }
        }
    }

    private Tile FindBestBuildTile(BuildingData building, City city)
    {
        return GridManager.Instance.GetTilesInRange(city.centerTile, city.territoryRadius)
            .Where(t => t != city.centerTile && t.currentBuilding == null && building.CanPlaceAt(t, city))
            .FirstOrDefault();
    }

    private IEnumerable<EconomyCandidateAction> GenerateSpawnCandidates()
    {
        var spawnCities = controlledPlayer.cities
            .Where(c => c.centerTile.currentUnit == null && c.units.Count < c.level + 1);

        foreach (City city in spawnCities)
        {
            foreach (FactionUnit unit in controlledPlayer.faction.availableUnits)
            {
                if (!controlledPlayer.techState.CanSpawn(unit.unitData)) continue;

                float score = ScoreUnitForCity(unit, city);

                yield return new EconomyCandidateAction
                {
                    kind = EconomyActionKind.SpawnUnit,
                    unit = unit,
                    city = city,
                    cost = unit.unitData.cost,
                    score = score
                };
            }
        }
    }

    private float ScoreUnitForCity(FactionUnit candidate, City city)
    {
        float score = 0f;
        int nearbyMeleeCount = 0;
        UnitData data = candidate.unitData;
        List<Unit> nearbyEnemies = GetNearbyEnemies(city);

        foreach (Unit enemy in nearbyEnemies)
        {
            if (enemy.data.attackRange == 1)
            {
                nearbyMeleeCount++;
            }
        }

        if (nearbyMeleeCount > 0)
        {
            float meleeThreat = nearbyMeleeCount * nearbyMeleeCount;
            score += meleeThreat * data.defensePower * data.maxHealth * profile.meleeVulnerabilityWeight;
        }

        List<Unit> enemies = GetNearbyEnemies(city, 6); // relatively nearby

        foreach (Unit enemy in enemies)
        {
            score += CalculateCounterStrength(candidate, enemy);
        }

        if (HasUncapturedCityNearby(city))
        {
            score += profile.expansionWeight * data.moveRange;
        }

        return score;
    }

    private List<Unit> GetNearbyEnemies(City city, int range = 3)
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

            if (distance <= 8)
                return true;
        }

        return false;
    }

    float CalculateCounterStrength(FactionUnit unit, Unit enemy)
    {
        return unit.unitData.counters.FirstOrDefault(c => c.unit == enemy.data).strength * profile.counterWeight;
    }

    private float ScoreBuilding(BuildingData building, City city)
    {
        float score = profile.buildingBaseWeight;

        if (building.populationGiven > 0 && city.populationToLevelUp > 0)
        {
            float progressFraction = Mathf.Clamp01((float)building.populationGiven / city.populationToLevelUp);
            score += progressFraction * profile.cityGrowthWeight;

            bool completesLevelUp = city.currentPopulation + building.populationGiven >= city.populationToLevelUp;
            if (completesLevelUp)
            {
                score += profile.cityGrowthWeight;
            }
        }

        return score;
    }

    private IEnumerable<EconomyCandidateAction> GenerateResearchCandidates()
    {
        foreach (TechData tech in controlledPlayer.faction.availableTech)
        {
            if (!controlledPlayer.techState.CanResearch(tech)) continue;

            yield return new EconomyCandidateAction
            {
                kind = EconomyActionKind.ResearchTech,
                tech = tech,
                cost = tech.cost,
                score = ScoreResearch(tech)
            };
        }
    }

    private float ScoreResearch(TechData tech)
    {
        float score = profile.researchBaseWeight;

        foreach (BuildingData building in controlledPlayer.faction.availableBuildings)
        {
            if (building.requiredTech == tech)
            {
                score += profile.researchBuildingUnlockWeight;
            }
        }

        foreach (FactionUnit unit in controlledPlayer.faction.availableUnits)
        {
            if (unit.unitData.requiredTech == tech)
            {
                score += ScoreUnitUnlock(unit);
            }
        }

        foreach (TechData other in controlledPlayer.faction.availableTech)
        {
            if (other.prerequisites != null && other.prerequisites.Contains(tech))
            {
                score += profile.researchBridgeWeight;
            }
        }

        return score;
    }

    // should be more sophisticated and more like the standard scoring
    private float ScoreUnitUnlock(FactionUnit unit)
    {
        float counterScore = 0;
        int enemyCount = 0;

        foreach (var player in TurnManager.Instance.players)
        {
            if (player == controlledPlayer) continue;

            foreach (var enemyUnit in player.units)
            {
                counterScore += CalculateCounterStrength(unit, enemyUnit);
                enemyCount++;
            }
        }

        return counterScore / enemyCount;
    }

    private void ExecuteEconomyAction(EconomyCandidateAction c)
    {
        switch (c.kind)
        {
            case EconomyActionKind.ResearchTech:
                controlledPlayer.techState.TryResearch(c.tech, controlledPlayer);
                break;
            case EconomyActionKind.PlaceBuilding:
                c.city.PlaceBuilding(c.building, c.buildTile);
                break;
            case EconomyActionKind.SpawnUnit:
                c.city.SpawnUnit(c.unit, c.unit.unitData.cost);
                break;
        }
    }
}
