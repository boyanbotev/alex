using UnityEngine;
using System.Collections.Generic;

public class WorldPopulationManager : MonoBehaviour
{
    public static WorldPopulationManager Instance;

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
    public void PopulateWorld()
    {
        SpawnNeutralVillages();
        AssignPlayerCapitalsAndUnits();
    }

    private void SpawnNeutralVillages()
    {
        List<Tile> validLandTiles = GetValidLandTiles();
        ShuffleList(validLandTiles); // Randomize placement order

        foreach (Tile tile in validLandTiles)
        {
            if (allCities.Count >= targetCityCount) break;

            if (IsFarEnoughFromOtherCities(tile) && IsFarEnoughFromEdge(tile))
            {
                // Instantiate Village/City model
                GameObject cityObj = Instantiate(villagePrefab, tile.transform.position, Quaternion.identity, tile.transform);
                City city = cityObj.GetComponent<City>();

                city.cityName = $"Village {allCities.Count + 1}";
                city.centerTile = tile;
                city.owner = null; // Unclaimed neutral village
                tile.city = city;

                city.ClaimTerritory();

                allCities.Add(city);
            }
        }

        Debug.Log($"Successfully spawned {allCities.Count} villages/cities.");
    }

    private void AssignPlayerCapitalsAndUnits()
    {
        List<Player> players = TurnManager.Instance.players;

        if (players.Count > allCities.Count)
        {
            Debug.LogError("Not enough cities generated for the number of players!");
            return;
        }

        // Pick capitals that maximize starting distance between players (Farthest-Point Algorithm)
        List<City> capitals = SelectDistributedCapitals(players.Count);

        for (int i = 0; i < players.Count; i++)
        {
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
        return Mathf.Abs(candidateTile.gridPosition.x - GridManager.Instance.width) > minMargin
            && candidateTile.gridPosition.x >= minMargin
            && Mathf.Abs(candidateTile.gridPosition.y - GridManager.Instance.width) > minMargin
            && candidateTile.gridPosition.y >= minMargin;
    }

    private List<Tile> GetValidLandTiles()
    {
        List<Tile> landTiles = new List<Tile>();
        foreach (var kvp in GridManager.Instance.grid)
        {
            Tile tile = kvp.Value;
            // Villages can only spawn on Fields or Forests (not Water/Mountains)
            if (tile.terrainType == TerrainType.Field || tile.terrainType == TerrainType.Forest)
            {
                landTiles.Add(tile);
            }
        }
        return landTiles;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
