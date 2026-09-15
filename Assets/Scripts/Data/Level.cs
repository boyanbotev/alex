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

    [Header("Cities")]
    [Min(1)] public int minCityDistance = 3;
    [Min(0)] public int minMargin = 1;
    public LevelFaction[] factions = Array.Empty<LevelFaction>();
    public string[] neutralCityNames = Array.Empty<string>();

    [Header("Movement")]

    [Tooltip("Prevent diagonal movement when both adjacent orthogonal tiles contain enemy units.")]
    public bool blockDiagonalsBetweenEnemies = true;

    public int CityCount {
        get {
            int count = neutralCityNames?.Length ?? 0;
            if (factions != null)
                foreach (var entry in factions) count += entry?.startingCityNames?.Length ?? 0;
            return count;
        }
    }

    public void Validate() {
        if (width <= 0 || height <= 0 || tileSize <= 0 || noiseScale <= 0 ||
            minCityDistance < 1 || minMargin < 0 || minMargin >= (width + 1) / 2 || minMargin >= (height + 1) / 2)
            throw new InvalidOperationException($"Level '{name}' has invalid grid or city spacing settings.");
        if (factions == null || factions.Length == 0)
            throw new InvalidOperationException($"Level '{name}' needs at least one faction.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int humanCount = 0;
        foreach (var entry in factions) {
            if (entry == null || entry.faction == null || entry.faction.cityPrefab == null ||
                entry.faction.startingUnit == null || entry.faction.startingUnit.prefab == null ||
                entry.faction.startingUnit.prefab.GetComponent<Unit>() == null ||
                entry.startingStars < 0 || entry.startingCityNames == null || entry.startingCityNames.Length == 0)
                throw new InvalidOperationException($"Level '{name}': each faction needs its prefabs, starting cities and non-negative stars.");
            if (!entry.isAI) humanCount++;
            ValidateNames(entry.startingCityNames, names);
        }
        if (humanCount != 1)
            throw new InvalidOperationException($"Level '{name}' needs exactly one human faction for the current UI.");
        ValidateNames(neutralCityNames, names);
    }

    private static void ValidateNames(string[] cities, HashSet<string> names) {
        if (cities == null) return;
        foreach (string city in cities)
            if (string.IsNullOrWhiteSpace(city) || !names.Add(city.Trim()))
                throw new InvalidOperationException("City names must be non-empty and unique throughout the Level.");
    }
}

[Serializable]
public class LevelFaction {
    public Faction faction;
    public Color color = Color.white;
    public bool isAI = true;
    [Min(0)] public int startingStars = 5;
    [Tooltip("The first city is the capital and receives the faction's starting unit.")]
    public string[] startingCityNames = { "Capital" };
}
