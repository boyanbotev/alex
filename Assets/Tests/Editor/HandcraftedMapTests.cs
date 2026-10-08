using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

public sealed class HandcraftedMapTests : TacticsTestFixture
{
    private Level CreateLevel()
    {
        var level = Asset<Level>();
        level.width = level.height = 20;
        level.map = Asset<MapData>();
        level.map.Resize(4, 3);
        level.terrainSource = TerrainSource.Handcrafted;
        level.cityPlacement = CityPlacementSource.Handcrafted;
        var capital = Asset<CityData>(); capital.cityName = "Capital";
        var neutral = Asset<CityData>(); neutral.cityName = "Neutral";
        level.factions = new[] { new LevelFaction { startingCities = new[] { capital } } };
        level.neutralCities = new[] { neutral };
        // Deliberately store neutrals first: map order must not decide ownership or capitals.
        level.map.cities.Add(new MapCity(neutral, new Vector2Int(3, 2)));
        level.map.cities.Add(new MapCity(capital, Vector2Int.zero));
        return level;
    }

    [Test]
    public void AuthoredPositionsFollowLevelRosterAndUseMapDimensions()
    {
        var level = CreateLevel();
        Assert.That(level.Width, Is.EqualTo(4));
        Assert.That(level.Height, Is.EqualTo(3));
        var capital = Tile(0, 0);
        var neutral = Tile(3, 2);
        var placement = new CityPlacement();
        Drain(placement.Generate(level, grid.grid.Values, new System.Random(1), new GenerationBudget(float.MaxValue)));
        Assert.That(placement.Positions, Is.EqualTo(new[] { capital, neutral }));
        level.terrainSource = TerrainSource.Procedural;
        Assert.That(level.Width, Is.EqualTo(20));
        Assert.Throws<InvalidOperationException>(() => level.map.ValidateCities(level));
    }

    [Test]
    public void MissingOverlappingAndBlockedCitiesAreRejected()
    {
        var level = CreateLevel();
        level.map.ValidateCities(level);
        var neutral = level.map.cities[0];
        level.map.cities.RemoveAt(0);
        Assert.Throws<InvalidOperationException>(() => level.map.ValidateCities(level));
        level.map.cities.Add(new MapCity(neutral.city, Vector2Int.zero));
        Assert.Throws<InvalidOperationException>(() => level.map.ValidateCities(level));
        level.map.cities[1] = neutral;
        level.map.terrain[0] = TerrainType.Mountain;
        Assert.Throws<InvalidOperationException>(() => level.map.ValidateCities(level));
    }

    [Test]
    public void InvalidRuntimeTileFailsBeforeReturningAnyCityPositions()
    {
        var level = CreateLevel();
        Tile(0, 0).terrainType = TerrainType.Mountain;
        Tile(3, 2);
        var placement = new CityPlacement();
        Assert.Throws<InvalidOperationException>(() => Drain(placement.Generate(level, grid.grid.Values,
            new System.Random(1), new GenerationBudget(float.MaxValue))));
        Assert.That(placement.Positions, Is.Null);
    }

    [Test]
    public void ResizePreservesCoordinatesAndRemovesCitiesOutsideNewBounds()
    {
        var level = CreateLevel();
        var map = level.map;
        map.terrain[1 * map.width + 1] = TerrainType.Forest;
        map.Resize(6, 4);
        Assert.That(map.GetTerrain(1, 1), Is.EqualTo(TerrainType.Forest));
        Assert.That(map.GetTerrain(5, 3), Is.EqualTo(TerrainType.Field));
        map.Resize(2, 2);
        Assert.That(map.GetTerrain(1, 1), Is.EqualTo(TerrainType.Forest));
        Assert.That(map.cities.Count, Is.EqualTo(1));
        Assert.That(map.cities[0].city, Is.SameAs(level.factions[0].startingCities[0]));
        map.ValidateTerrain();
        map.terrain = new TerrainType[1];
        Assert.Throws<InvalidOperationException>(map.ValidateTerrain);
    }

    private FactionUnit ConfigurePlacedUnits(Level level)
    {
        var faction = Asset<Faction>();
        faction.cityPrefab = Component<City>().gameObject;
        var unit = Asset<FactionUnit>();
        unit.unitData = Asset<UnitData>();
        unit.prefab = Component<Unit>().gameObject;
        faction.availableUnits = new[] { unit };
        level.factions[0].faction = faction;
        level.factions[0].isAI = false;
        level.factions[0].spawnCapitalUnit = false;
        return unit;
    }

    [Test]
    public void CitylessHumanRequiresPlacedUnitsWhileAIStillRequiresCity()
    {
        var level = CreateLevel();
        var unit = ConfigurePlacedUnits(level);
        level.factions[0].startingCities = Array.Empty<CityData>();
        level.map.cities.RemoveAt(1);
        Assert.Throws<InvalidOperationException>(level.Validate);
        level.map.units.Add(new MapUnit(0, unit, Vector2Int.zero));
        Assert.DoesNotThrow(level.Validate);
        level.factions[0].isAI = true;
        Assert.Throws<InvalidOperationException>(level.Validate);
    }

    [Test]
    public void PlacedUnitsAllowOwnCitiesButRejectForeignCitiesOverlapAndMountains()
    {
        var level = CreateLevel();
        var unit = ConfigurePlacedUnits(level);
        level.map.units.Add(new MapUnit(0, unit, Vector2Int.zero));
        Assert.DoesNotThrow(level.Validate);
        level.map.units.Add(new MapUnit(0, unit, Vector2Int.zero));
        Assert.Throws<InvalidOperationException>(level.Validate);
        level.map.units.RemoveAt(1);
        level.map.units[0] = new MapUnit(0, unit, new Vector2Int(3, 2));
        Assert.Throws<InvalidOperationException>(level.Validate);
        level.map.units[0] = new MapUnit(0, unit, new Vector2Int(1, 0));
        level.map.terrain[1] = TerrainType.Mountain;
        Assert.Throws<InvalidOperationException>(level.Validate);
        level.map.terrain[1] = TerrainType.Field;
        level.factions[0].faction.availableUnits = Array.Empty<FactionUnit>();
        Assert.Throws<InvalidOperationException>(level.Validate);
    }

    [Test]
    public void AutomaticCitiesSkipPlacedUnitsAndHandleCitylessFirstFaction()
    {
        var level = CreateLevel();
        var unit = ConfigurePlacedUnits(level);
        var city = level.factions[0].startingCities[0];
        level.factions[0].startingCities = Array.Empty<CityData>();
        level.factions = new[] { level.factions[0], new LevelFaction { startingCities = new[] { city } } };
        level.neutralCities = Array.Empty<CityData>();
        level.cityPlacement = CityPlacementSource.Automatic;
        level.minMargin = 0;
        level.map.units.Add(new MapUnit(0, unit, Vector2Int.zero));
        Tile(0, 0);
        var free = Tile(1, 0);
        var placement = new CityPlacement();
        Drain(placement.Generate(level, grid.grid.Values, new System.Random(1), new GenerationBudget(float.MaxValue)));
        Assert.That(placement.Positions, Is.EqualTo(new[] { free }));
        level.map.units.Add(new MapUnit(0, unit, new Vector2Int(3, 2)));
        level.map.Resize(2, 2);
        Assert.That(level.map.units.Count, Is.EqualTo(1));
    }

    private static void Drain(IEnumerator routine)
    {
        while (routine.MoveNext()) if (routine.Current is IEnumerator child) Drain(child);
    }
}
