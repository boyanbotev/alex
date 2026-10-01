using NUnit.Framework;
using UnityEngine;

public class NeuronActionsTests : TacticsTestFixture
{
    private Building Segment(Tile tile, Player owner)
    {
        var building = Component<Building>();
        building.data = Asset<BuildingData>();
        building.data.isNeuron = true;
        building.data.demolitionRefundFraction = 0.5f;
        building.paidCost = 3;
        building.owner = owner;
        building.tile = tile;
        tile.currentBuilding = building;
        return building;
    }

    private void Reveal(Tile tile)
    {
        if (player.visibleTiles == null) player.visibleTiles = new VisibilityState(10, 10);
        player.visibleTiles.SetVisible(tile.gridPosition);
    }

    [Test]
    public void OwnDemolitionPaysOnceAndImmediatelyCutsIncome()
    {
        var a = City(Tile(0), player);
        var b = City(Tile(2), player);
        var tile = Tile(1);
        var segment = Segment(tile, player);
        Reveal(tile);
        Assert.That(a.NeuronIncome, Is.EqualTo(1));
        Assert.That(segment.TryDemolish(player), Is.True);
        Assert.That(tile.currentBuilding, Is.Null);
        Assert.That(player.stars, Is.EqualTo(6));
        Assert.That(a.NeuronIncome, Is.Zero);
        Assert.That(b.NeuronIncome, Is.Zero);
        Assert.That(segment.TryDemolish(player), Is.False);
        Assert.That(player.stars, Is.EqualTo(6));
    }

    [Test]
    public void SeverAtPeaceDeclaresWarPaysAttackerAndConsumesAttack()
    {
        var tile = Tile(1);
        var segment = Segment(tile, enemy);
        var unit = Unit(player, tile);
        unit.hasMoved = true;
        Reveal(tile);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(unit.TrySeverNeuron(segment), Is.True);
        Assert.That(turns.Diplomacy.IsAtWar(player, enemy), Is.True);
        Assert.That(player.stars, Is.EqualTo(6));
        Assert.That(enemy.stars, Is.EqualTo(5));
        Assert.That(unit.hasAttacked && unit.hasMoved, Is.True);
        Assert.That(unit.isActive, Is.False);
        Assert.That(tile.currentBuilding, Is.Null);
        Assert.That(unit.TrySeverNeuron(segment), Is.False);
        Assert.That(player.stars, Is.EqualTo(6));
    }

    [Test]
    public void AlliedSegmentsAreProtectedEvenIfButtonWasOpenedBeforeAlliance()
    {
        var tile = Tile(1);
        var segment = Segment(tile, enemy);
        var unit = Unit(player, tile);
        Reveal(tile);
        Assert.That(unit.CanSeverNeuron(segment), Is.True);
        Call(turns.Diplomacy, "SetRelation", player, enemy, DiplomaticRelation.Allied);
        Assert.That(unit.TrySeverNeuron(segment), Is.False);
        Assert.That(segment.TryDemolish(player), Is.False);
        Assert.That(tile.currentBuilding, Is.SameAs(segment));
        Assert.That(unit.hasAttacked, Is.False);
        Assert.That(player.stars, Is.EqualTo(5));
        Assert.That(turns.Diplomacy.GetRelation(player, enemy), Is.EqualTo(DiplomaticRelation.Allied));
    }

    [Test]
    public void SeverRequiresVisibleOccupiedTileActiveMilitaryUnitAndItsTurn()
    {
        var tile = Tile(1);
        var segment = Segment(tile, enemy);
        var unit = Unit(player, tile);
        Assert.That(unit.TrySeverNeuron(segment), Is.False);
        Reveal(tile);
        unit.currentTile = Tile(2);
        Assert.That(unit.TrySeverNeuron(segment), Is.False);
        unit.currentTile = tile;
        unit.hasAttacked = true;
        Assert.That(unit.TrySeverNeuron(segment), Is.False);
        unit.hasAttacked = false;
        unit.isActive = false;
        Assert.That(unit.TrySeverNeuron(segment), Is.False);
        unit.isActive = true;
        unit.data.attackPower = 0;
        Assert.That(unit.TrySeverNeuron(segment), Is.False);
        unit.data.attackPower = 2;
        turns.activePlayerIndex = 1;
        Assert.That(unit.TrySeverNeuron(segment), Is.False);
        Assert.That(tile.currentBuilding, Is.SameAs(segment));
        Assert.That(player.stars, Is.EqualTo(5));
    }

    [Test]
    public void OwnDemolitionRejectsOtherTurnAndRefundUsesPaidCost()
    {
        var tile = Tile(1);
        var segment = Segment(tile, player);
        Reveal(tile);
        segment.data.cost = 100;
        Assert.That(segment.DemolitionRefund, Is.EqualTo(1));
        turns.activePlayerIndex = 1;
        Assert.That(segment.TryDemolish(player), Is.False);
        Assert.That(tile.currentBuilding, Is.SameAs(segment));
    }

}
