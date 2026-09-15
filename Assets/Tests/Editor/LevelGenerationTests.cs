using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class LevelGenerationTests : TacticsTestFixture
{
    private Level CreateLevel()
    {
        var level = Asset<Level>();
        level.width = 24;
        level.height = 12;
        level.factions = new[] {
            new LevelFaction { startingCityNames = new[] { "Capital A", "Town A" } },
            new LevelFaction { startingCityNames = new[] { "Capital B", "Town B", "Port B" } }
        };
        level.neutralCityNames = new[] { "Neutral A", "Neutral B" };
        return level;
    }

    private CityPlacement Place(Level level, int seed = 17)
    {
        var plan = new CityPlacement();
        Drain(plan.Generate(level, grid.grid.Values, new System.Random(seed), new GenerationBudget(float.MaxValue)));
        return plan;
    }

    private static void Drain(IEnumerator routine)
    {
        while (routine.MoveNext())
            if (routine.Current is IEnumerator nested) Drain(nested);
    }

    [Test]
    public void MultipleStartingCitiesRespectSpacingMarginsAndClusterAroundTheirCapital()
    {
        var level = CreateLevel();
        for (int x = 0; x < level.width; x++)
            for (int y = 0; y < level.height; y++) Tile(x, y).terrainType = TerrainType.Field;
        var positions = Place(level).Positions;
        Assert.That(positions.Length, Is.EqualTo(7));
        Assert.That(positions.Distinct().Count(), Is.EqualTo(7));
        foreach (Tile tile in positions)
        {
            Assert.That(tile.gridPosition.x, Is.InRange(1, 22));
            Assert.That(tile.gridPosition.y, Is.InRange(1, 10));
            foreach (Tile other in positions)
            {
                if (tile == other) continue;
                Vector2Int d = tile.gridPosition - other.gridPosition;
                Assert.That(Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)), Is.GreaterThanOrEqualTo(3));
            }
        }
        Assert.That(Vector2Int.Distance(positions[1].gridPosition, positions[0].gridPosition),
            Is.LessThan(Vector2Int.Distance(positions[1].gridPosition, positions[2].gridPosition)));
        for (int i = 3; i <= 4; i++)
            Assert.That(Vector2Int.Distance(positions[i].gridPosition, positions[2].gridPosition),
                Is.LessThan(Vector2Int.Distance(positions[i].gridPosition, positions[0].gridPosition)));
        Assert.That(Place(level).Positions, Is.EqualTo(positions), "Fixed seed should reproduce placement.");
    }

    [Test]
    public void InsufficientLandFailsWithoutReturningPartialPlacement()
    {
        var level = CreateLevel();
        Tile(3, 3).terrainType = TerrainType.Field;
        var plan = new CityPlacement();
        Assert.Throws<InvalidOperationException>(() => Drain(plan.Generate(level, grid.grid.Values,
            new System.Random(0), new GenerationBudget(float.MaxValue))));
        Assert.That(plan.Positions, Is.Null);
        Assert.That(population.allCities, Is.Empty);
    }

    [Test]
    public void SingleCapitalAndNoNeutralsWorksAndAvoidsWater()
    {
        var level = CreateLevel();
        level.factions = new[] { new LevelFaction { startingCityNames = new[] { "Only City" } } };
        level.neutralCityNames = Array.Empty<string>();
        Tile(3, 3).terrainType = TerrainType.Water;
        Tile(4, 4).terrainType = TerrainType.Mountain;
        Tile valid = Tile(5, 5);
        valid.terrainType = TerrainType.Forest;
        Assert.That(Place(level).Positions, Is.EqualTo(new[] { valid }));
    }

    [Test]
    public void RosterComesFromLevelAndDoesNotStoreRuntimeStateInAsset()
    {
        var level = CreateLevel();
        level.factions[0].faction = Asset<Faction>();
        level.factions[0].isAI = false;
        level.factions[0].startingStars = 12;
        level.factions[1].faction = Asset<Faction>();
        turns.players.Clear();
        turns.InitializePlayers(level);
        Assert.That(turns.players.Count, Is.EqualTo(2));
        Assert.That(turns.players[0].faction, Is.SameAs(level.factions[0].faction));
        Assert.That(turns.players[0].isAI, Is.False);
        Assert.That(turns.players[0].stars, Is.EqualTo(12));
        Assert.That(turns.players[1].isAI, Is.True);
        turns.players[0].stars = 0;
        Assert.That(level.factions[0].startingStars, Is.EqualTo(12));
    }

    [Test]
    public void LevelRejectsDuplicateOrBlankNames()
    {
        var level = CreateLevel();
        foreach (var entry in level.factions)
        {
            entry.faction = Asset<Faction>();
            entry.faction.cityPrefab = Component<City>().gameObject;
            entry.faction.startingUnit = Asset<FactionUnit>();
            entry.faction.startingUnit.prefab = Component<Unit>().gameObject;
        }
        level.factions[0].isAI = false;
        Assert.DoesNotThrow(level.Validate);
        var copy = UnityEngine.Object.Instantiate(level);
        try
        {
            copy.neutralCityNames = new[] { copy.factions[0].startingCityNames[0] };
            Assert.Throws<InvalidOperationException>(copy.Validate);
            copy.neutralCityNames = new[] { " " };
            Assert.Throws<InvalidOperationException>(copy.Validate);
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }
}
