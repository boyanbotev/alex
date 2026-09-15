using System;
using NUnit.Framework;

public class FactionAuthoringTests : TacticsTestFixture
{
    [Test]
    public void StartingTechIsFactionSpecificFreeAndUnlocksRecruitmentAndBuildings()
    {
        var first = Asset<Faction>();
        var second = Asset<Faction>();
        var archery = Asset<TechData>();
        var riding = Asset<TechData>();
        var prerequisite = Asset<TechData>();
        archery.prerequisites = new[] { prerequisite };
        archery.cost = 99;
        first.startingUnlockedTech = new[] { archery, archery, null };
        second.startingUnlockedTech = new[] { riding };
        var level = Asset<Level>();
        level.factions = new[] {
            new LevelFaction { faction = first, startingStars = 5 },
            new LevelFaction { faction = second, startingStars = 7 },
            new LevelFaction { faction = first, startingStars = 3 }
        };
        turns.players.Clear();
        turns.InitializePlayers(level);
        var a = turns.players[0];
        var b = turns.players[1];
        Assert.That(a.techState.IsUnlocked(archery), Is.True);
        Assert.That(a.techState.IsUnlocked(riding), Is.False);
        Assert.That(b.techState.IsUnlocked(riding), Is.True);
        Assert.That(b.techState.IsUnlocked(archery), Is.False);
        Assert.That(a.techState.IsUnlocked(prerequisite), Is.False);
        Assert.That(a.techState.CanResearch(archery), Is.False);
        Assert.That(a.stars, Is.EqualTo(5));
        Assert.That(b.stars, Is.EqualTo(7));
        var unit = Asset<UnitData>();
        unit.requiredTech = archery;
        var building = Asset<BuildingData>();
        building.requiredTech = archery;
        Assert.That(a.techState.CanSpawn(unit), Is.True);
        Assert.That(b.techState.CanSpawn(unit), Is.False);
        Assert.That(a.techState.CanBuild(building), Is.True);
        Assert.That(b.techState.CanBuild(building), Is.False);
        var laterTech = Asset<TechData>();
        Assert.That(a.techState.TryResearch(laterTech, a), Is.True);
        Assert.That(turns.players[2].techState.IsUnlocked(laterTech), Is.False);
        Assert.That(first.startingUnlockedTech.Length, Is.EqualTo(3));
    }

    [Test]
    public void EmptyStartingTechPreservesDefaultLockedState()
    {
        var state = new PlayerTechState();
        var tech = Asset<TechData>();
        state.InitializeStartingTech(null);
        Assert.That(state.IsUnlocked(tech), Is.False);
        state.InitializeStartingTech(new[] { tech });
        state.InitializeStartingTech(Array.Empty<TechData>());
        Assert.That(state.IsUnlocked(tech), Is.False);
    }

    [Test]
    public void RecruitmentUsesFactionStatsInsteadOfPrefabStats()
    {
        player.faction = Asset<Faction>();
        var tile = Tile(0);
        var city = City(tile, player);
        var prefab = Component<Unit>();
        prefab.data = Asset<UnitData>();
        var entry = Asset<FactionUnit>();
        entry.faction = player.faction;
        entry.prefab = prefab.gameObject;
        entry.unitData = Asset<UnitData>();
        entry.unitData.maxHealth = 30;
        Assert.That(city.SpawnUnit(entry, 0), Is.True);
        try
        {
            Assert.That(tile.currentUnit.data, Is.SameAs(entry.unitData));
            Assert.That(prefab.data, Is.Not.SameAs(entry.unitData));
        }
        finally { UnityEngine.Object.DestroyImmediate(tile.currentUnit.gameObject); }
    }

    [Test]
    public void StartingUnitUsesFactionStatsInsteadOfPrefabStats()
    {
        player.faction = Asset<Faction>();
        var city = City(Tile(0), player);
        var entry = Asset<FactionUnit>();
        entry.prefab = Component<Unit>().gameObject;
        entry.prefab.GetComponent<Unit>().data = Asset<UnitData>();
        entry.unitData = Asset<UnitData>();
        player.faction.startingUnit = entry;
        Call(population, "SpawnStartingUnit", player, city);
        try { Assert.That(city.centerTile.currentUnit.data, Is.SameAs(entry.unitData)); }
        finally { UnityEngine.Object.DestroyImmediate(city.centerTile.currentUnit.gameObject); }
    }

    [Test]
    public void CustomStatsStillMatchBaseUnitCounters()
    {
        var original = Asset<UnitData>();
        var customised = Asset<UnitData>();
        customised.counterType = original;
        var defender = Unit(enemy, Tile(0));
        defender.data = customised;
        var attacker = Asset<FactionUnit>();
        attacker.unitData = Asset<UnitData>();
        attacker.unitData.counters = new[] { new Counter { unit = original, strength = 2 } };
        var economy = Component<EconomyAI>();
        typeof(EconomyAI).GetField("profile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(economy, profile);
        profile.counterWeight = 3;
        Assert.That((float)Call(economy, "CalculateCounterStrength", attacker, defender), Is.EqualTo(6));
    }
}
