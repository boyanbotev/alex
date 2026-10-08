using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Map", menuName = "Game/Map")]
public sealed class MapData : ScriptableObject
{
    [Min(1)] public int width = 15;
    [Min(1)] public int height = 15;
    public TerrainType[] terrain = Array.Empty<TerrainType>();
    public List<MapCity> cities = new();
    public List<MapUnit> units = new();

    public bool Contains(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;
    public TerrainType GetTerrain(int x, int y) => terrain[y * width + x];

    // Preserve the overlapping area when resizing; new cells default to Field.
    public void Resize(int newWidth, int newHeight)
    {
        newWidth = Mathf.Max(1, newWidth);
        newHeight = Mathf.Max(1, newHeight);
        var resized = new TerrainType[checked(newWidth * newHeight)];
        if (terrain != null && width > 0 && height > 0 && terrain.Length == (long)width * height)
            for (int y = 0; y < Mathf.Min(height, newHeight); y++)
                Array.Copy(terrain, y * width, resized, y * newWidth, Mathf.Min(width, newWidth));
        width = newWidth;
        height = newHeight;
        terrain = resized;
        cities ??= new List<MapCity>();
        cities.RemoveAll(c => !Contains(c.position));
        units ??= new List<MapUnit>();
        units.RemoveAll(u => !Contains(u.position));
    }

    public void ValidateTerrain()
    {
        if (width <= 0 || height <= 0 || terrain == null || terrain.Length != (long)width * height)
            throw new InvalidOperationException($"Map '{name}' has invalid dimensions or terrain data. Open it in the map painter.");
        foreach (TerrainType type in terrain)
            if (type < TerrainType.Field || type > TerrainType.Water)
                throw new InvalidOperationException($"Map '{name}' contains an unknown terrain type.");
    }

    public void ValidateCities(Level level)
    {
        if (width != level.Width || height != level.Height)
            throw new InvalidOperationException($"Map '{name}' must match the procedural grid size for handcrafted cities.");
        var expected = new HashSet<CityData>();
        foreach (var faction in level.factions)
            foreach (var city in faction.startingCities) expected.Add(city);
        if (level.neutralCities != null)
            foreach (var city in level.neutralCities) expected.Add(city);
        var positions = new HashSet<Vector2Int>();
        if (cities == null) throw new InvalidOperationException($"Map '{name}' has no city placements.");
        foreach (var entry in cities)
        {
            if (entry.city == null || !expected.Remove(entry.city))
                throw new InvalidOperationException($"Map '{name}' contains a duplicate city or a city absent from this Level.");
            if (!Contains(entry.position) || !positions.Add(entry.position))
                throw new InvalidOperationException($"Map '{name}': city '{entry.city.cityName}' is outside the map or overlaps another city.");
            if (level.terrainSource == TerrainSource.Handcrafted &&
                !IsCityTerrain(GetTerrain(entry.position.x, entry.position.y)))
                throw new InvalidOperationException($"Map '{name}': city '{entry.city.cityName}' must be on Field or Forest.");
        }
        if (expected.Count > 0)
            throw new InvalidOperationException($"Map '{name}' is missing {expected.Count} of this Level's cities.");
    }

    public void ValidateUnits(Level level)
    {
        if (units == null || units.Count == 0) return;
        if (width != level.Width || height != level.Height)
            throw new InvalidOperationException($"Map '{name}' must match the level grid for unit placements.");
        var positions = new HashSet<Vector2Int>();
        foreach (var entry in units)
        {
            if (entry.factionIndex < 0 || entry.factionIndex >= level.factions.Length ||
                entry.unit == null || entry.unit.unitData == null || entry.unit.prefab == null ||
                entry.unit.prefab.GetComponent<Unit>() == null ||
                level.factions[entry.factionIndex].faction.availableUnits == null ||
                Array.IndexOf(level.factions[entry.factionIndex].faction.availableUnits, entry.unit) < 0)
                throw new InvalidOperationException($"Map '{name}' has a unit outside its faction's roster or missing its prefab/data.");
            if (!Contains(entry.position) || !positions.Add(entry.position))
                throw new InvalidOperationException($"Map '{name}' has units outside the grid or sharing a tile.");
            if (level.terrainSource == TerrainSource.Handcrafted && GetTerrain(entry.position.x, entry.position.y) == TerrainType.Mountain)
                throw new InvalidOperationException($"Map '{name}' has a unit on a mountain.");
            if (level.cityPlacement == CityPlacementSource.Handcrafted)
                foreach (var city in cities)
                    if (city.position == entry.position &&
                        Array.IndexOf(level.factions[entry.factionIndex].startingCities, city.city) < 0)
                        throw new InvalidOperationException($"Map '{name}' has a unit on a neutral or another faction's city.");
        }
    }

    public static bool IsCityTerrain(TerrainType type) => type == TerrainType.Field || type == TerrainType.Forest;
}

[Serializable]
public struct MapCity
{
    public CityData city;
    public Vector2Int position;
    public MapCity(CityData city, Vector2Int position) { this.city = city; this.position = position; }
}

[Serializable]
public struct MapUnit
{
    public int factionIndex;
    public FactionUnit unit;
    public Vector2Int position;
    public MapUnit(int factionIndex, FactionUnit unit, Vector2Int position)
    { this.factionIndex = factionIndex; this.unit = unit; this.position = position; }
}
