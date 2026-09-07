using UnityEngine;
using Unity.Profiling;
using System.Collections;
using System.Collections.Generic;

public class WorldPopulationManager : MonoBehaviour
{
    public static WorldPopulationManager Instance;
    private static readonly ProfilerMarker creationMarker = new ProfilerMarker("WorldGeneration.City");

    [Header("Prefabs")]
    public GameObject villagePrefab;

    [Header("Population Settings")]
    [Tooltip("Minimum grid distance between any two cities")]
    public int minCityDistance = 3;
    [Tooltip("How many neutral villages to spawn on the map")]
    public int targetCityCount = 8;
    [Tooltip("How much margin around villages to the edge of the map")]
    public int minMargin = 1;

    [Header("State")]
    public List<City> allCities = new List<City>();

    private void Awake()
    {
        Instance = this;
    }

    // Called right after GridGenerator.cs finishes generating tiles
    public IEnumerator PopulateWorld(GenerationBudget budget)
    {
        WorldLoadingOverlay.Show("Placing cities...");
        yield return SpawnNeutralVillages(budget);
        WorldLoadingOverlay.Show("Preparing players...");
        yield return AssignPlayerCapitalsAndUnits(budget);
        WorldLoadingOverlay.Show("Creating fog...");
        yield return FogOfWarManager.Instance.CreateFogTiles(budget);
    }

    private IEnumerator SpawnNeutralVillages(GenerationBudget budget)
    {
        var validLandTiles = new List<Tile>();
        foreach (var entry in GridManager.Instance.grid)
        {
            if (budget.ShouldYield())
            {
                yield return null;
                budget.ShouldYield(); // Start timing this frame before doing more work.
            }
            Tile tile = entry.Value;
            if (tile.terrainType == TerrainType.Field || tile.terrainType == TerrainType.Forest)
                validLandTiles.Add(tile);
        }
        for (int i = 0; i < validLandTiles.Count; i++)
        {
            if (budget.ShouldYield())
            {
                yield return null;
                budget.ShouldYield(); // Start timing this frame before doing more work.
            }
            int randomIndex = Random.Range(i, validLandTiles.Count);
            Tile temp = validLandTiles[i];
            validLandTiles[i] = validLandTiles[randomIndex];
            validLandTiles[randomIndex] = temp;
        }

        foreach (Tile tile in validLandTiles)
        {
            if (allCities.Count >= targetCityCount) break;
            if (budget.ShouldYield())
            {
                yield return null;
                budget.ShouldYield(); // Start timing this frame before doing more work.
            }

            if (IsFarEnoughFromOtherCities(tile) && IsFarEnoughFromEdge(tile))
            {
                // Instantiate Village/City model
                GameObject cityObj;
                using (creationMarker.Auto())
                    cityObj = Instantiate(villagePrefab, tile.transform.position, Quaternion.identity, tile.transform);
                City city = cityObj.GetComponent<City>();

                city.cityName = $"Village {allCities.Count + 1}";
                city.centerTile = tile;
                city.owner = null; // Unclaimed neutral village
                tile.city = city;

                city.ClaimTerritory();

                allCities.Add(city);
            }
        }
    }

    private IEnumerator AssignPlayerCapitalsAndUnits(GenerationBudget budget)
    {
        List<Player> players = TurnManager.Instance.players;

        if (players.Count > allCities.Count)
        {
            throw new System.InvalidOperationException("Not enough cities for all players. Adjust city placement settings.");
        }

        // Pick capitals that maximize starting distance between players (Farthest-Point Algorithm)
        List<City> capitals = SelectDistributedCapitals(players.Count);

        for (int i = 0; i < players.Count; i++)
        {
            if (budget.ShouldYield())
            {
                yield return null;
                budget.ShouldYield(); // Start timing this frame before doing more work.
            }
            Player player = players[i];
            City capital = capitals[i];

            // 1. Claim City as Capital
            capital.owner = player;
            capital.cityName = $"{player.factionName} Capital";
            player.cities.Add(capital);

            capital.SetFaction(player.faction);

            // 2. Spawn Starting Unit (Warrior) on top of the Capital
            GameObject unitObj = Instantiate(player.faction.startingUnit.prefab, capital.centerTile.transform.position, Quaternion.identity);
            Unit unit = unitObj.GetComponent<Unit>();

            unit.owner = player;
            unit.currentTile = capital.centerTile;
            capital.centerTile.currentUnit = unit;
            unit.homeCity = capital;

            // Note: Starting units CAN move on Turn 1
            unit.hasMoved = false;
            unit.hasAttacked = false;

            TerritoryBorderManager.Instance.RebuildBorder(player);

            capital.units.Add(unit);
            player.units.Add(unit);
        }
    }

    // Farthest-Point Sampling: Guarantees players spawn as far from each other as possible
    private List<City> SelectDistributedCapitals(int count)
    {
        List<City> selected = new List<City>();
        if (allCities.Count == 0) return selected;

        // Pick 1st capital randomly
        selected.Add(allCities[Random.Range(0, allCities.Count)]);

        while (selected.Count < count)
        {
            City bestCandidate = null;
            float maxMinDistance = -1f;

            foreach (City candidate in allCities)
            {
                if (selected.Contains(candidate)) continue;

                // Calculate minimum distance from candidate to any already selected capital
                float minDistanceToCapital = float.MaxValue;
                foreach (City capital in selected)
                {
                    float dist = Vector2Int.Distance(candidate.centerTile.gridPosition, capital.centerTile.gridPosition);
                    if (dist < minDistanceToCapital)
                    {
                        minDistanceToCapital = dist;
                    }
                }

                // We want the candidate whose minimum distance to a capital is as large as possible
                if (minDistanceToCapital > maxMinDistance)
                {
                    maxMinDistance = minDistanceToCapital;
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate != null)
            {
                selected.Add(bestCandidate);
            }
            else
            {
                break;
            }
        }

        return selected;
    }

    private bool IsFarEnoughFromOtherCities(Tile candidateTile)
    {
        foreach (City city in allCities)
        {
            int dist = Mathf.Max(
                Mathf.Abs(candidateTile.gridPosition.x - city.centerTile.gridPosition.x),
                Mathf.Abs(candidateTile.gridPosition.y - city.centerTile.gridPosition.y)
            );

            if (dist < minCityDistance) return false;
        }
        return true;
    }

    private bool IsFarEnoughFromEdge(Tile candidateTile)
    {
        return Mathf.Abs(candidateTile.gridPosition.x - GridGenerator.Instance.boardSettings.width) > minMargin
            && candidateTile.gridPosition.x >= minMargin
            && Mathf.Abs(candidateTile.gridPosition.y - GridGenerator.Instance.boardSettings.width) > minMargin
            && candidateTile.gridPosition.y >= minMargin;
    }

}
