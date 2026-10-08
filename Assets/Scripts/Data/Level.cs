using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Level", menuName = "Game/Level")]
public class Level : ScriptableObject {
    [Header("Grid")]
    [Min(1)] public int width = 15;
    [Min(1)] public int height = 15;
    [Min(0.01f)] public float tileSize = 1f;
    public bool is3DIsometric = true;

    [Header("Terrain")]
    [Min(0.001f)] public float noiseScale = 0.15f;
    public bool randomizeSeed = true;
    public int seed;

    [Header("Map")]
    public TerrainSource terrainSource;
    public MapData map;
    [Tooltip("Handcrafted cities use the positions in the map asset. Procedural terrain keeps these tiles as fields.")]
    public CityPlacementSource cityPlacement;
    public int Width => terrainSource == TerrainSource.Handcrafted && map != null ? map.width : width;
    public int Height => terrainSource == TerrainSource.Handcrafted && map != null ? map.height : height;

    [Header("Cities")]
    [Min(1)] public int minCityDistance = 3;
    [Min(0)] public int minMargin = 1;
    public LevelFaction[] factions = Array.Empty<LevelFaction>();
    public CityData[] neutralCities = Array.Empty<CityData>();

    public Tutorial tutorial;

    [Header("Ending Story")]
    public TextAsset endingStory;

    [Header("Movement")]

    [Tooltip("Prevent diagonal movement when both adjacent orthogonal tiles contain enemy units.")]
    public bool blockDiagonalsBetweenEnemies = true;

    [Tooltip("Prevent diagonal movement when both adjacent orthogonal tiles are impassable (currently mountains).")]
    public bool blockDiagonalsBetweenImpassableTiles = true;

    public int CityCount {
        get {
            int count = neutralCities?.Length ?? 0;
            if (factions != null)
                foreach (var entry in factions) count += entry?.startingCities?.Length ?? 0;
            return count;
        }
    }

    public void Validate() {
        if ((terrainSource == TerrainSource.Handcrafted || cityPlacement == CityPlacementSource.Handcrafted) && map == null)
            throw new InvalidOperationException($"Level '{name}' needs a map asset for handcrafted terrain or cities.");
        if (terrainSource == TerrainSource.Handcrafted) map.ValidateTerrain();
        if (Width <= 0 || Height <= 0 || tileSize <= 0 ||
            (terrainSource == TerrainSource.Procedural && noiseScale <= 0) ||
            (cityPlacement == CityPlacementSource.Automatic && (minCityDistance < 1 || minMargin < 0 ||
                minMargin >= (Width + 1) / 2 || minMargin >= (Height + 1) / 2)))
            throw new InvalidOperationException($"Level '{name}' has invalid grid or city spacing settings.");
        if (factions == null || factions.Length == 0)
            throw new InvalidOperationException($"Level '{name}' needs at least one faction.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int humanCount = 0;
        foreach (var entry in factions) {
            if (entry == null || entry.faction == null || entry.faction.cityPrefab == null ||
                (entry.spawnCapitalUnit && entry.startingCities != null && entry.startingCities.Length > 0 &&
                 (entry.faction.startingUnit == null || entry.faction.startingUnit.unitData == null || entry.faction.startingUnit.prefab == null ||
                  entry.faction.startingUnit.prefab.GetComponent<Unit>() == null)) ||
                entry.startingStars < 0 || entry.startingCities == null || (entry.isAI && entry.startingCities.Length == 0))
                throw new InvalidOperationException($"Level '{name}': each faction needs valid prefabs and non-negative stars; AI factions also need a starting city.");
            if (!entry.isAI) humanCount++;
            ValidateNames(entry.startingCities, names);
        }
        ValidateNames(neutralCities, names);
        if (cityPlacement == CityPlacementSource.Handcrafted) map.ValidateCities(this);
        if (map != null) map.ValidateUnits(this);
        for (int i = 0; i < factions.Length; i++)
            if (factions[i].startingCities.Length == 0 &&
                (map == null || map.units == null || !map.units.Exists(u => u.factionIndex == i)))
                throw new InvalidOperationException($"Level '{name}': a cityless faction needs at least one placed unit.");
        if (humanCount != 1)
            throw new InvalidOperationException($"Level '{name}' needs exactly one human faction for the current UI.");
    }

    private static void ValidateNames(CityData[] cities, HashSet<string> names) {
        if (cities == null) return;
        foreach (CityData city in cities)
            if (city == null || string.IsNullOrWhiteSpace(city.cityName) || !names.Add(city.cityName.Trim()))
                throw new InvalidOperationException("City names must be non-empty and unique throughout the Level.");
    }
}

public enum TerrainSource { Procedural, Handcrafted }
public enum CityPlacementSource { Automatic, Handcrafted }

[Serializable]
public class LevelFaction {
    public Faction faction;
    public Color color = Color.white;
    public bool isAI = true;
    [Min(0)] public int startingStars = 5;
    [Tooltip("Spawn the faction's default starting unit on its capital unless a placed unit occupies that tile.")]
    public bool spawnCapitalUnit = true;
    [Tooltip("The first city is the capital. Human factions may start without cities if they have placed units.")]
    public CityData[] startingCities = Array.Empty<CityData>();
}
