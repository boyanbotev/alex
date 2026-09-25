using NUnit.Framework;
using UnityEngine;

public class CityBondTests : TacticsTestFixture
{
    [Test]
    public void MapPerksAreNativeFirstDeduplicatedAndRemoveSuspendedBonuses()
    {
        player.stars = 100;
        var a = City(Tile(0), player); var road = Segment(1);
        var b = City(Tile(2), player); var otherRoad = Segment(0, 1); var c = City(Tile(0, 2), player);
        Perk(a, CityPerkKind.Healing, 3); a.data.perk.perkName = "Oxytocin";
        Perk(b, CityPerkKind.Fortification, 3); b.data.perk.perkName = "Cortisol";
        Perk(c, CityPerkKind.Healing, 3); c.data.perk.perkName = "Oxytocin";
        Assert.That(turns.Bonds.TryCreate(player, a, b), Is.True);
        Assert.That(turns.Bonds.TryCreate(player, a, c), Is.True);
        Assert.That(CityPerkIcons.Row(a), Is.EqualTo("Oxytocin<sup>1</sup>  Cortisol<sup>1</sup>"));
        Assert.That(c.TryUpgradePerk(player), Is.True);
        Assert.That(CityPerkIcons.Row(a), Is.EqualTo("Oxytocin<sup>2</sup>  Cortisol<sup>1</sup>"));
        road.tile.currentBuilding = null;
        otherRoad.tile.currentBuilding = null;
        turns.Neurons.Invalidate();
        Assert.That(CityPerkIcons.Row(a), Is.EqualTo("Oxytocin<sup>1</sup>"));
    }

    [TestCase(CityPerkKind.Healing)]
    [TestCase(CityPerkKind.Fortification)]
    [TestCase(CityPerkKind.Adrenaline)]
    public void PerkUpgradeCostsTenIsLocalAndPreservesPassive(CityPerkKind kind)
    {
        player.faction = Asset<Faction>();
        var a = City(Tile(0), player); var b = City(Tile(2), player);
        Perk(a, kind, 3); b.data = a.data;
        var unit = Asset<FactionUnit>(); unit.unitData = Asset<UnitData>();
        unit.unitData.requiredPerk = kind; unit.unitData.requiredPerkLevel = 2;
        player.faction.availableUnits = new[] { unit };
        Assert.That(a.CanRecruit(unit), Is.False);
        player.stars = 9;
        Assert.That(a.TryUpgradePerk(player), Is.False);
        Assert.That(player.stars, Is.EqualTo(9));
        player.stars = 20;
        Assert.That(a.TryUpgradePerk(enemy), Is.False);
        Assert.That(a.TryUpgradePerk(player), Is.True);
        Assert.That(player.stars, Is.EqualTo(10));
        Assert.That(a.TryUpgradePerk(player), Is.False);
        Assert.That(player.stars, Is.EqualTo(10));
        Assert.That(a.PerkLevel, Is.EqualTo(2));
        Assert.That(b.PerkLevel, Is.EqualTo(1));
        Assert.That(a.PerkAmount(kind), Is.EqualTo(3));
        Assert.That(a.CanRecruit(unit), Is.True);
        Assert.That(b.CanRecruit(unit), Is.False);
        player.faction.availableUnits = System.Array.Empty<FactionUnit>();
        Assert.That(a.CanRecruit(unit), Is.False);
    }

    [Test]
    public void UpgradedPerkSharesDirectlyAndRevertsOnBondLoss()
    {
        player.faction = Asset<Faction>(); player.stars = 100;
        var a = City(Tile(0), player); var road = Segment(1);
        var b = City(Tile(2), player); Segment(3); var c = City(Tile(4), player);
        Perk(a, CityPerkKind.Healing, 0); Perk(b, CityPerkKind.Healing, 0);
        var unit = Asset<FactionUnit>(); unit.unitData = Asset<UnitData>();
        unit.unitData.requiredPerk = CityPerkKind.Healing; unit.unitData.requiredPerkLevel = 2;
        player.faction.availableUnits = new[] { unit };
        Assert.That(turns.Bonds.TryCreate(player, a, b), Is.True);
        Assert.That(turns.Bonds.TryCreate(player, b, c), Is.True);
        Assert.That(a.TryUpgradePerk(player), Is.True);
        Assert.That(b.CanRecruit(unit), Is.True);
        Assert.That(c.CanRecruit(unit), Is.False);
        Assert.That(c.CanUpgradePerk(player), Is.False);
        road.tile.currentBuilding = null; turns.Neurons.Invalidate();
        Assert.That(b.CanRecruit(unit), Is.False);
        Assert.That(turns.Bonds.GetLevel(b, CityPerkKind.Healing), Is.EqualTo(1));
        Assert.That(a.CanRecruit(unit), Is.True);
    }

    [TestCase(CityPerkKind.Healing)]
    [TestCase(CityPerkKind.Fortification)]
    [TestCase(CityPerkKind.Adrenaline)]
    public void RecruitmentRequiresRosterAndPerkRegardlessOfTechOrPassiveAmount(CityPerkKind kind)
    {
        player.faction = Asset<Faction>();
        player.stars = 20;
        var city = City(Tile(0), player);
        var entry = Asset<FactionUnit>();
        entry.unitData = Asset<UnitData>();
        entry.unitData.requiredPerk = kind;
        entry.unitData.requiredTech = Asset<TechData>();
        entry.prefab = Component<Unit>().gameObject;
        player.faction.availableUnits = new[] { entry };
        Assert.That(city.CanRecruit(entry), Is.False);
        player.techState.InitializeStartingTech(new[] { entry.unitData.requiredTech });
        Assert.That(city.CanRecruit(entry), Is.False);
        Assert.That(city.SpawnUnit(entry, 2), Is.False);
        Assert.That(player.stars, Is.EqualTo(20));
        player.techState.InitializeStartingTech(null);
        Perk(city, kind, 0);
        Assert.That(city.CanRecruit(entry), Is.True);
        player.faction.availableUnits = System.Array.Empty<FactionUnit>();
        Assert.That(city.CanRecruit(entry), Is.False);
        Assert.That(city.SpawnUnit(entry, 2), Is.False);
        Assert.That(player.stars, Is.EqualTo(20));
        entry.unitData.requiredPerk = CityPerkKind.None;
        player.faction.availableUnits = new[] { entry };
        city.data.perk = null;
        Assert.That(city.CanRecruit(entry), Is.True);
    }

    [Test]
    public void RecruitmentSharesOnlyDirectActiveBondsAndEndsWhenSevered()
    {
        player.faction = Asset<Faction>(); player.stars = 30;
        var a = City(Tile(0), player); var road = Segment(1);
        var b = City(Tile(2), player); Segment(3); var c = City(Tile(4), player);
        Perk(a, CityPerkKind.Healing, 0);
        var entry = Asset<FactionUnit>(); entry.unitData = Asset<UnitData>();
        entry.unitData.requiredPerk = CityPerkKind.Healing;
        player.faction.availableUnits = new[] { entry };
        Assert.That(turns.Bonds.TryCreate(player, a, b), Is.True);
        Assert.That(turns.Bonds.TryCreate(player, b, c), Is.True);
        Assert.That(b.CanRecruit(entry), Is.True);
        Assert.That(c.CanRecruit(entry), Is.False);
        road.tile.currentBuilding = null; turns.Neurons.Invalidate();
        Assert.That(b.CanRecruit(entry), Is.False);
        Assert.That(a.CanRecruit(entry), Is.True);
    }

    [Test]
    public void FortificationSharesWithoutStackingAndEndsOutsideTerritory()
    {
        player.stars = 20;
        var a = City(Tile(0), player); var road = Segment(1); var b = City(Tile(2), player);
        Perk(a, CityPerkKind.Fortification, 1);
        var unit = Unit(player, b.centerTile);
        int basic = unit.data.defensePower;
        Assert.That(board.GetDefensePower(unit), Is.EqualTo(basic));
        Assert.That(turns.Bonds.TryCreate(player, a, b), Is.True);
        Assert.That(board.GetDefensePower(unit), Is.EqualTo(basic + 1));
        Perk(b, CityPerkKind.Fortification, 1);
        Assert.That(board.GetDefensePower(unit), Is.EqualTo(basic + 1));
        b.data.perk = null;
        int checkpoint = board.Checkpoint();
        board.WithMove(unit, b.centerTile, Tile(3));
        Assert.That(board.GetDefensePower(unit), Is.EqualTo(basic));
        board.Rollback(checkpoint);
        road.tile.currentBuilding = null; turns.Neurons.Invalidate();
        Assert.That(board.GetDefensePower(unit), Is.EqualTo(basic));
        Assert.That(unit.data.defensePower, Is.EqualTo(basic));
    }

    [Test]
    public void FortificationChangesRealAndSimulatedDamageEquallyForAllies()
    {
        var ally = Component<Player>(); turns.players.Add(ally);
        var city = City(Tile(0), ally);
        var defender = Unit(player, city.centerTile);
        var attacker = Unit(enemy, Tile(1));
        attacker.data.attackPower = 2; defender.data.defensePower = 2;
        turns.Diplomacy.MakePeace(player, ally);
        Perk(city, CityPerkKind.Fortification, 1);
        var basic = CombatMath.PredictDamage(attacker, defender, BoardState.Live);
        Assert.That(board.GetDefensePower(defender), Is.EqualTo(2));
        turns.Diplomacy.MakeAlliance(player, ally);
        var fortified = CombatMath.PredictDamage(attacker, defender, BoardState.Live);
        Assert.That(fortified.Item1, Is.LessThan(basic.Item1));
        Assert.That(fortified.Item2, Is.GreaterThan(basic.Item2));
        Assert.That(ActionSimulator.PredictDamage(attacker, defender, board), Is.EqualTo(fortified));
        Assert.That(attacker.PredictAttackDamage(defender).damage, Is.EqualTo(fortified.Item1));
        int checkpoint = board.Checkpoint(); board.WithWar(player, ally);
        Assert.That(ActionSimulator.PredictDamage(attacker, defender, board), Is.EqualTo(basic));
        board.Rollback(checkpoint);
    }

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
    public void ReinforcementFollowsLinksAndPreservesSharedRoute()
    {
        player.stars = 30;
        var a = City(Tile(0), player);
        var first = Segment(1).tile;
        var junction = Segment(2).tile;
        var b = City(Tile(3), player);
        var c = City(Tile(3, 1), player);
        var branch = Segment(2, 1).tile;
        Assert.That(turns.Bonds.TryCreate(player, a, b), Is.True);
        Assert.That(turns.Bonds.IsReinforced(a.centerTile, first), Is.True);
        Assert.That(turns.Bonds.IsReinforced(first, junction), Is.True);
        Assert.That(turns.Bonds.IsReinforced(junction, first), Is.True);
        Assert.That(turns.Bonds.IsReinforced(junction, b.centerTile), Is.True);
        Assert.That(turns.Bonds.IsReinforced(junction, c.centerTile), Is.False);
        Assert.That(turns.Bonds.IsReinforced(junction, branch), Is.False);

        Assert.That(turns.Bonds.TryCreate(player, a, c), Is.True);
        turns.Bonds.RemoveCity(b);
        Assert.That(turns.Bonds.IsReinforced(first, junction), Is.True);
        Assert.That(turns.Bonds.IsReinforced(junction, c.centerTile), Is.True);
        Assert.That(turns.Bonds.IsReinforced(junction, b.centerTile), Is.False);
        Assert.That(turns.Bonds.IsReinforced(junction, branch), Is.False);
        turns.Bonds.RemoveCity(c);
        Assert.That(turns.Bonds.IsReinforced(first, junction), Is.False);
        Assert.That(turns.Bonds.IsReinforced(a.centerTile, first), Is.False);
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
