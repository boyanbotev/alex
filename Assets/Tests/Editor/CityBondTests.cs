using NUnit.Framework;
using UnityEngine;

public class CityBondTests : TacticsTestFixture
{
    private Building Segment(int x, int y = 0)
    {
        var segment = Component<Building>();
        segment.tile = Tile(x, y); segment.tile.currentBuilding = segment;
        segment.owner = player; segment.data = Asset<BuildingData>(); segment.data.isNeuron = true;
        return segment;
    }
    private void Perk(City city, CityPerkKind kind, int amount)
    {
        city.data = Asset<CityData>(); city.data.perk = Asset<CityPerkData>();
        city.data.perk.kind = kind; city.data.perk.amount = amount;
    }
    [Test]
    public void SharingIsDirectAndSameKindUsesStrongestValue()
    {
        player.stars = 30;
        var a = City(Tile(0), player); Segment(1);
        var b = City(Tile(2), player); Segment(3);
        var c = City(Tile(4), player);
        Perk(a, CityPerkKind.Healing, 2); Perk(b, CityPerkKind.Healing, 3); Perk(c, CityPerkKind.Dopamine, 4);
        Assert.That(turns.Bonds.TryCreate(player, a, b), Is.True);
        Assert.That(turns.Bonds.TryCreate(player, b, c), Is.True);
        Assert.That(a.PerkAmount(CityPerkKind.Healing), Is.EqualTo(3));
        Assert.That(a.PerkAmount(CityPerkKind.Dopamine), Is.Zero);
        Assert.That(b.PerkAmount(CityPerkKind.Dopamine), Is.EqualTo(4));
        Assert.That(c.PerkAmount(CityPerkKind.Healing), Is.EqualTo(3));
        Assert.That(player.stars, Is.EqualTo(18));
        Assert.That(turns.Bonds.TryCreate(player, b, a), Is.False);
        Assert.That(player.stars, Is.EqualTo(18));
    }
    [Test]
    public void AlliedBondsReserveBothSlotsAndSuspendWhenAllianceEnds()
    {
        player.stars = 20; turns.maxBondsPerCity = 1; turns.bondUpgradeCost = 4;
        var a = City(Tile(0), player); Segment(1);
        var b = City(Tile(2), enemy); Segment(3);
        var c = City(Tile(4), player);
        Perk(b, CityPerkKind.Healing, 3);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(turns.Bonds.TryCreate(player, a, b), Is.False);
        turns.Diplomacy.MakeAlliance(player, enemy);
        Assert.That(turns.Bonds.TryCreate(player, a, b), Is.True);
        Assert.That(turns.Bonds.Count(b), Is.EqualTo(1));
        Assert.That(turns.Bonds.TryCreate(player, c, b), Is.False);
        Assert.That(player.stars, Is.EqualTo(16));
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(a.PerkAmount(CityPerkKind.Healing), Is.Zero);
        Assert.That(turns.Bonds.Count(a), Is.EqualTo(1));
        turns.Diplomacy.MakeAlliance(player, enemy);
        Assert.That(a.PerkAmount(CityPerkKind.Healing), Is.EqualTo(3));
    }
    [Test]
    public void RouteReroutesSuspendsAndRestoresWithoutRepurchase()
    {
        player.stars = 6;
        var a = City(Tile(0), player); var lower = Segment(1); var upper = Segment(1, 1);
        var b = City(Tile(2), player); Perk(b, CityPerkKind.Healing, 2);
        Assert.That(turns.Bonds.TryCreate(player, a, b), Is.True);
        var bond = turns.Bonds.All[0];
        var first = bond.route[0].currentBuilding; var second = first == lower ? upper : lower;
        first.tile.currentBuilding = null; turns.Neurons.Invalidate();
        Assert.That(bond.Active, Is.True);
        Assert.That(bond.route, Does.Contain(second.tile));
        second.tile.currentBuilding = null; turns.Neurons.Invalidate();
        Assert.That(bond.Active, Is.False);
        Assert.That(a.PerkAmount(CityPerkKind.Healing), Is.Zero);
        first.tile.currentBuilding = first; turns.Neurons.Invalidate();
        Assert.That(bond.Active, Is.True);
        Assert.That(player.stars, Is.Zero);
        b.owner = enemy; turns.Neurons.Invalidate();
        Assert.That(turns.Bonds.All, Is.Empty);
        Assert.That(turns.Bonds.IsReinforced(first.tile), Is.False);
    }
    [Test]
    public void UnaffordablePurchaseDoesNotReserveSlots()
    {
        player.stars = 5;
        var a = City(Tile(0), player); Segment(1); var b = City(Tile(2), player);
        Assert.That(turns.Bonds.TryCreate(player, a, b), Is.False);
        Assert.That(turns.Bonds.Count(a), Is.Zero);
        Assert.That(player.stars, Is.EqualTo(5));
    }
    [Test]
    public void HealingBenefitsAlliesButNotPeacefulForeignUnits()
    {
        var city = City(Tile(0), player); Perk(city, CityPerkKind.Healing, 3);
        var unit = Unit(enemy, city.centerTile); unit.data.maxHealth = 20; unit.currentHealth = 1;
        turns.Diplomacy.MakePeace(player, enemy); unit.Heal();
        Assert.That(unit.currentHealth, Is.EqualTo(3));
        turns.Diplomacy.MakeAlliance(player, enemy); unit.Heal();
        Assert.That(unit.currentHealth, Is.EqualTo(8));
    }
    [Test]
    public void DopamineIsStampedAtRecruitmentAndSurvivesBondLoss()
    {
        player.stars = 20; player.faction = Asset<Faction>();
        var a = City(Tile(0), player); Segment(1); var b = City(Tile(2), player);
        Perk(b, CityPerkKind.Dopamine, 3);
        turns.Bonds.TryCreate(player, a, b);
        var recruit = Asset<FactionUnit>(); recruit.unitData = Asset<UnitData>();
        recruit.prefab = Component<Unit>().gameObject;
        Assert.That(a.SpawnUnit(recruit, 2), Is.True);
        var unit = a.centerTile.currentUnit;
        try
        {
            turns.Bonds.RemoveCity(b);
            Assert.That(unit.dopamineBonus, Is.EqualTo(3));
            Assert.That(a.PerkAmount(CityPerkKind.Dopamine), Is.Zero);
        }
        finally { Object.DestroyImmediate(unit.gameObject); }
    }

    [Test]
    public void HomeTerritoryOnlyAddsHealingWhenItHasThePerk()
    {
        var city = City(Tile(0), player);
        var unit = Unit(player, city.centerTile);
        unit.data.maxHealth = 20; unit.currentHealth = 1;
        unit.Heal();
        Assert.That(unit.currentHealth, Is.EqualTo(3));
        Perk(city, CityPerkKind.Healing, 3);
        unit.Heal();
        Assert.That(unit.currentHealth, Is.EqualTo(8));
    }

    [Test]
    public void AdrenalineIsLocalSharedAndLostWhenTheRouteBreaks()
    {
        player.stars = 20;
        var a = City(Tile(0), player); var road = Segment(1); var b = City(Tile(2), player);
        Perk(a, CityPerkKind.Adrenaline, 1);
        var unit = Unit(player, b.centerTile); unit.data.moveRange = 1;
        Assert.That(board.GetMoveRange(unit), Is.EqualTo(1));
        Assert.That(turns.Bonds.TryCreate(player, a, b), Is.True);
        Assert.That(board.GetMoveRange(unit), Is.EqualTo(2));
        var outside = Tile(3);
        int checkpoint = board.Checkpoint();
        board.WithMove(unit, b.centerTile, outside);
        Assert.That(board.GetMoveRange(unit), Is.EqualTo(1));
        board.Rollback(checkpoint);
        Assert.That(board.GetMoveRange(unit), Is.EqualTo(2));
        road.tile.currentBuilding = null; turns.Neurons.Invalidate();
        Assert.That(board.GetMoveRange(unit), Is.EqualTo(1));
        Assert.That(unit.data.moveRange, Is.EqualTo(1));
    }

    [Test]
    public void AdrenalineBenefitsAlliesAndExtendsAIMovementButNotPeacefulVisitors()
    {
        var city = City(Tile(0), enemy); Perk(city, CityPerkKind.Adrenaline, 1);
        var unit = Unit(player, city.centerTile); unit.data.moveRange = 1;
        Tile(1); var destination = Tile(2);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(board.GetMoveRange(unit), Is.EqualTo(1));
        turns.Diplomacy.MakeAlliance(player, enemy);
        Assert.That(board.GetMoveRange(unit), Is.EqualTo(2));
        Assert.That(Candidates(unit).Exists(x => x.moveTile == destination), Is.True);
        int checkpoint = board.Checkpoint(); board.WithWar(player, enemy);
        Assert.That(board.GetMoveRange(unit), Is.EqualTo(1));
        board.Rollback(checkpoint);
    }
}
