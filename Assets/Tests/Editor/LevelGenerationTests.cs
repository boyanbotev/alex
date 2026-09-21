using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class LevelGenerationTests : TacticsTestFixture
{
    private CityData[] Cities(params string[] names) => names.Select(n => { var city = Asset<CityData>(); city.cityName = n; return city; }).ToArray();
    private Level CreateLevel()
    {
        var level = Asset<Level>();
        level.width = 24;
        level.height = 12;
        level.factions = new[] {
            new LevelFaction { startingCities = Cities("Capital A", "Town A") },
            new LevelFaction { startingCities = Cities("Capital B", "Town B", "Port B") }
        };
        level.neutralCities = Cities("Neutral A", "Neutral B");
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
        // Towns use the nearest still-unassigned sites, without moving the scattered locations.
        foreach (int i in new[] { 1, 3, 4 })
        {
            Tile capital = positions[i == 1 ? 0 : 2];
            float distance = (positions[i].gridPosition - capital.gridPosition).sqrMagnitude;
            for (int j = i + 1; j < positions.Length; j++)
                Assert.That(distance, Is.LessThanOrEqualTo(
                    (positions[j].gridPosition - capital.gridPosition).sqrMagnitude));
        }
        Assert.That(Place(level).Positions, Is.EqualTo(positions), "Fixed seed should reproduce placement.");
    }

    [Test]
    public void CityLocationsDoNotDependOnFactionOwnership()
    {
        var level = CreateLevel();
        for (int x = 0; x < level.width; x++)
            for (int y = 0; y < level.height; y++) Tile(x, y).terrainType = TerrainType.Field;
        var originalFactions = level.factions;
        var originalNeutrals = level.neutralCities;
        for (int seed = 0; seed < 20; seed++)
        {
            level.factions = originalFactions;
            level.neutralCities = originalNeutrals;
            var grouped = Place(level, seed).Positions;
            level.factions = new[] { new LevelFaction { startingCities = Cities("Capital") } };
            level.neutralCities = Cities("A", "B", "C", "D", "E", "F");
            Assert.That(Place(level, seed).Positions, Is.EquivalentTo(grouped),
                $"Ownership should not affect city locations (seed {seed}).");
        }
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
        level.factions = new[] { new LevelFaction { startingCities = Cities("Only City") } };
        level.neutralCities = Array.Empty<CityData>();
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
            entry.faction.startingUnit.unitData = Asset<UnitData>();
            entry.faction.startingUnit.prefab = Component<Unit>().gameObject;
        }
        level.factions[0].isAI = false;
        Assert.DoesNotThrow(level.Validate);
        var copy = UnityEngine.Object.Instantiate(level);
        try
        {
            copy.neutralCities = new[] { copy.factions[0].startingCities[0] };
            Assert.Throws<InvalidOperationException>(copy.Validate);
            copy.neutralCities = Cities(" ");
            Assert.Throws<InvalidOperationException>(copy.Validate);
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }
}
