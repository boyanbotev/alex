using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EconomyAI : MonoBehaviour
{
    private AIProfile profile;
    private Player controlledPlayer;

    private readonly List<EconomyCandidateAction> _candidateBuffer = new List<EconomyCandidateAction>();
    private readonly List<Unit> _nearbyEnemyBuffer = new List<Unit>();

    public void HandleEconomy(Player controlledPlayer, AIProfile profile)
    {
        this.controlledPlayer = controlledPlayer;
        this.profile = profile;

        while (true)
        {
            GenerateEconomyCandidates(_candidateBuffer);

            EconomyCandidateAction best = null;
            for (int i = 0; i < _candidateBuffer.Count; i++)
            {
                EconomyCandidateAction c = _candidateBuffer[i];
                if (c.cost > controlledPlayer.stars) continue;
                if (best == null || c.score > best.score)
                {
                    best = c;
                }
            }

            if (best == null || best.score <= 0f) break;

            ExecuteEconomyAction(best);
        }
    }

    private void GenerateEconomyCandidates(List<EconomyCandidateAction> buffer)
    {
        buffer.Clear();
        GenerateBuildingCandidates(buffer);
        GenerateSpawnCandidates(buffer);
        GenerateResearchCandidates(buffer);
    }

    private void GenerateBuildingCandidates(List<EconomyCandidateAction> buffer)
    {
        for (int i = 0; i < controlledPlayer.cities.Count; i++)
        {
            City city = controlledPlayer.cities[i];

            for (int j = 0; j < controlledPlayer.faction.availableBuildings.Length; j++)
            {
                BuildingData building = controlledPlayer.faction.availableBuildings[j];
                if (!controlledPlayer.techState.CanBuild(building)) continue;

                Tile tile = FindBestBuildTile(building, city);
                if (tile == null) continue;

                buffer.Add(new EconomyCandidateAction
                {
                    kind = EconomyActionKind.PlaceBuilding,
                    building = building,
                    buildTile = tile,
                    city = city,
                    cost = building.cost,
                    score = ScoreBuilding(building, city)
                });
            }
        }
    }

    private Tile FindBestBuildTile(BuildingData building, City city)
    {
        List<Tile> tiles = GridManager.Instance.GetTilesInRange(city.centerTile, city.territoryRadius);
        for (int i = 0; i < tiles.Count; i++)
        {
            Tile t = tiles[i];
            if (t != city.centerTile && t.currentBuilding == null && building.CanPlaceAt(t, city))
            {
                return t;
            }
        }
        return null;
    }

    private void GenerateSpawnCandidates(List<EconomyCandidateAction> buffer)
    {
        for (int i = 0; i < controlledPlayer.cities.Count; i++)
        {
            City city = controlledPlayer.cities[i];
            if (city.centerTile.currentUnit != null) continue;
            if (city.units.Count >= city.level + 1) continue;

            for (int j = 0; j < controlledPlayer.faction.availableUnits.Length; j++)
            {
                FactionUnit unit = controlledPlayer.faction.availableUnits[j];
                if (!controlledPlayer.techState.CanSpawn(unit.unitData)) continue;

                buffer.Add(new EconomyCandidateAction
                {
                    kind = EconomyActionKind.SpawnUnit,
                    unit = unit,
                    city = city,
                    cost = unit.unitData.cost,
                    score = ScoreUnitForCity(unit, city)
                });
            }
        }
    }

    private float ScoreUnitForCity(FactionUnit candidate, City city)
    {
        UnitData data = candidate.unitData;

        GetNearbyEnemies(city, _nearbyEnemyBuffer);

        float score = 0f;
        int nearbyMeleeCount = 0;

        for (int i = 0; i < _nearbyEnemyBuffer.Count; i++)
        {
            Unit enemy = _nearbyEnemyBuffer[i];
            if (enemy.data.attackRange == 1)
            {
                nearbyMeleeCount++;
            }
            score += CalculateCounterStrength(candidate, enemy);
        }

        if (nearbyMeleeCount > 0)
        {
            float meleeThreat = nearbyMeleeCount * nearbyMeleeCount;
            score += meleeThreat * data.defensePower * data.maxHealth * profile.meleeVulnerabilityWeight;
        }

        if (HasUncapturedCityNearby(city))
        {
            score += profile.expansionWeight * data.moveRange;
        }

        return score;
    }

    private void GetNearbyEnemies(City city, List<Unit> buffer)
    {
        buffer.Clear();

        for (int i = 0; i < TurnManager.Instance.players.Count; i++)
        {
            Player player = TurnManager.Instance.players[i];
            if (player == controlledPlayer) continue;

            List<Unit> units = player.units;
            for (int j = 0; j < units.Count; j++)
            {
                Unit unit = units[j];
                if (unit == null || !unit.isAlive) continue;

                int distance = Utils.GridDistance(city.centerTile.gridPosition, unit.currentTile.gridPosition);
                if (distance <= 3)
                {
                    buffer.Add(unit);
                }
            }
        }
    }

    private bool HasUncapturedCityNearby(City city)
    {
        List<City> allCities = WorldPopulationManager.Instance.allCities;
        for (int i = 0; i < allCities.Count; i++)
        {
            City otherCity = allCities[i];
            if (otherCity == city || otherCity.owner == controlledPlayer) continue;

            int distance = Utils.GridDistance(city.centerTile.gridPosition, otherCity.centerTile.gridPosition);
            if (distance <= 8) return true;
        }
        return false;
    }

    private float CalculateCounterStrength(FactionUnit unit, Unit enemy)
    {
        var counters = unit.unitData.counters;
        for (int i = 0; i < counters.Length; i++)
        {
            if (counters[i].unit == enemy.data)
            {
                return counters[i].strength * profile.counterWeight;
            }
        }
        return 0f;
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

    private void GenerateResearchCandidates(List<EconomyCandidateAction> buffer)
    {
        for (int i = 0; i < controlledPlayer.faction.availableTech.Length; i++)
        {
            TechData tech = controlledPlayer.faction.availableTech[i];
            if (!controlledPlayer.techState.CanResearch(tech)) continue;

            buffer.Add(new EconomyCandidateAction
            {
                kind = EconomyActionKind.ResearchTech,
                tech = tech,
                cost = tech.cost,
                score = ScoreResearch(tech)
            });
        }
    }

    private float ScoreResearch(TechData tech)
    {
        float score = profile.researchBaseWeight;

        for (int i = 0; i < controlledPlayer.faction.availableBuildings.Length; i++)
        {
            if (controlledPlayer.faction.availableBuildings[i].requiredTech == tech)
            {
                score += profile.researchBuildingUnlockWeight;
            }
        }

        for (int i = 0; i < controlledPlayer.faction.availableUnits.Length; i++)
        {
            if (controlledPlayer.faction.availableUnits[i].unitData.requiredTech == tech)
            {
                score += ScoreUnitUnlock(controlledPlayer.faction.availableUnits[i]);
            }
        }

        for (int i = 0; i < controlledPlayer.faction.availableTech.Length; i++)
        {
            TechData other = controlledPlayer.faction.availableTech[i];
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
        float counterScore = 0f;
        int enemyCount = 0;

        for (int i = 0; i < TurnManager.Instance.players.Count; i++)
        {
            Player player = TurnManager.Instance.players[i];
            if (player == controlledPlayer) continue;

            List<Unit> units = player.units;
            for (int j = 0; j < units.Count; j++)
            {
                counterScore += CalculateCounterStrength(unit, units[j]);
                enemyCount++;
            }
        }

        return enemyCount > 0 ? counterScore / enemyCount : 0f;
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