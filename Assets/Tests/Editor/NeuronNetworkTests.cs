using NUnit.Framework;
using UnityEngine;

public class NeuronNetworkTests : TacticsTestFixture
{
    private Building Segment(int x, int y, Player owner)
    {
        var tile = Tile(x, y);
        var building = Component<Building>();
        building.data = Asset<BuildingData>();
        building.data.isNeuron = true;
        building.owner = owner;
        building.tile = tile;
        tile.currentBuilding = building;
        return building;
    }

    [Test]
    public void DiagonalsConnectAndInterveningCitiesStopTraversal()
    {
        var a = City(Tile(0, 0), player);
        Segment(1, 1, player);
        var b = City(Tile(2, 2), player);
        Segment(3, 3, player);
        var c = City(Tile(4, 4), player);
        Assert.That(turns.Neurons.AreConnected(a, b), Is.True);
        Assert.That(turns.Neurons.AreConnected(b, c), Is.True);
        Assert.That(turns.Neurons.AreConnected(a, c), Is.False);
        Assert.That(b.NeuronIncome, Is.EqualTo(2));
    }

    [Test]
    public void PeaceWarAndRestoredPeaceUpdateBothEndpoints()
    {
        var a = City(Tile(0), player);
        Segment(1, 0, player);
        var b = City(Tile(2), enemy);
        Assert.That(a.NeuronIncome, Is.Zero);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(a.NeuronIncome, Is.EqualTo(1));
        Assert.That(b.NeuronIncome, Is.EqualTo(1));
        turns.Diplomacy.DeclareWar(player, enemy);
        Assert.That(a.NeuronIncome, Is.Zero);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(a.NeuronIncome, Is.EqualTo(1));
    }

    [Test]
    public void ThirdPartyMustBeAtPeaceWithBothEndpoints()
    {
        var third = Component<Player>();
        turns.players.Add(third);
        var a = City(Tile(0), player);
        Segment(1, 0, third);
        var b = City(Tile(2), enemy);
        turns.Diplomacy.MakePeace(player, enemy);
        turns.Diplomacy.MakePeace(player, third);
        Assert.That(turns.Neurons.AreConnected(a, b), Is.False);
        turns.Diplomacy.MakePeace(enemy, third);
        Assert.That(turns.Neurons.AreConnected(a, b), Is.True);
        turns.Diplomacy.DeclareWar(player, third);
        Assert.That(b.NeuronIncome, Is.Zero);
    }

    [Test]
    public void AlternateRouteSurvivesCutWithoutDuplicateIncome()
    {
        var a = City(Tile(0), player);
        var b = City(Tile(2), player);
        var upper = Segment(1, 1, player);
        var lower = Segment(1, 0, player);
        Assert.That(a.NeuronIncome, Is.EqualTo(1));
        lower.tile.currentBuilding = null;
        turns.Neurons.Invalidate();
        Assert.That(a.NeuronIncome, Is.EqualTo(1));
        upper.tile.currentBuilding = null;
        turns.Neurons.Invalidate();
        Assert.That(a.NeuronIncome, Is.Zero);
        Assert.That(b.NeuronIncome, Is.Zero);
    }

    [Test]
    public void PlacementUsesBuilderOwnershipAndPeacefulTerritory()
    {
        var data = Asset<BuildingData>();
        data.isNeuron = true;
        player.faction = Asset<Faction>();
        player.faction.availableBuildings = new[] { data };
        player.visibleTiles = new VisibilityState(10, 10);
        City(Tile(0), player);
        var target = Tile(1, 1);
        player.visibleTiles.SetVisible(target.gridPosition);
        Assert.That(player.CanPlaceNeuron(data, target), Is.True);
        target.territoryCity = City(Tile(8, 8), enemy);
        Assert.That(player.CanPlaceNeuron(data, target), Is.False);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(player.CanPlaceNeuron(data, target), Is.True);
        target.currentBuilding = Component<Building>();
        Assert.That(player.CanPlaceNeuron(data, target), Is.False);
        target.currentBuilding = null;
        data.constructionDisabled = true;
        Assert.That(player.CanPlaceNeuron(data, target), Is.False);
    }

    [Test]
    public void BranchJunctionConnectsAllEndpointsAndDoesNotConnectCityToItself()
    {
        var a = City(Tile(0, 2), player);
        var b = City(Tile(4, 2), player);
        var c = City(Tile(2, 4), player);
        Segment(1, 2, player);
        Segment(2, 2, player);
        Segment(3, 2, player);
        Segment(2, 3, player);
        Assert.That(a.NeuronIncome, Is.EqualTo(2));
        Assert.That(b.NeuronIncome, Is.EqualTo(2));
        Assert.That(c.NeuronIncome, Is.EqualTo(2));
        Assert.That(turns.Neurons.AreConnected(a, a), Is.False);
    }

    [Test]
    public void PurchaseOutsideTerritoryChargesOnceAndNeedsOwnedExtension()
    {
        var data = Asset<BuildingData>();
        data.isNeuron = true;
        data.cost = 3;
        data.populationGiven = 10; // Neurons must never grant population, even with stale asset data.
        data.buildingPrefab = Component<Building>().gameObject;
        player.faction = Asset<Faction>();
        player.faction.availableBuildings = new[] { data };
        player.visibleTiles = new VisibilityState(10, 10);
        var city = City(Tile(0), player);
        var target = Tile(1, 1);
        var extension = Tile(2, 2);
        player.visibleTiles.SetVisible(target.gridPosition);
        player.visibleTiles.SetVisible(extension.gridPosition);
        Assert.That(player.PlaceNeuron(data, target), Is.True);
        var built = target.currentBuilding;
        try
        {
            Assert.That(built.owner, Is.SameAs(player));
            Assert.That(built.parentCity, Is.Null);
            Assert.That(built.paidCost, Is.EqualTo(3));
            Assert.That(built.DemolitionRefund, Is.EqualTo(1));
            Assert.That(city.currentPopulation, Is.Zero);
            Assert.That(player.stars, Is.EqualTo(2));
            Assert.That(player.PlaceNeuron(data, target), Is.False);
            Assert.That(player.CanPlaceNeuron(data, extension), Is.True);
            Assert.That(player.PlaceNeuron(data, extension), Is.False); // Insufficient stars.
            Assert.That(extension.currentBuilding, Is.Null);
            Assert.That(player.stars, Is.EqualTo(2));
            built.owner = enemy;
            turns.Diplomacy.MakePeace(player, enemy);
            Assert.That(player.CanPlaceNeuron(data, extension), Is.False);
        }
        finally { Object.DestroyImmediate(built.gameObject); }
    }

    [Test]
    public void OwnershipChangeReevaluatesIncomeWithoutChangingSegmentOwner()
    {
        var a = City(Tile(0), player);
        var b = City(Tile(2), player);
        var segment = Segment(1, 0, player);
        Assert.That(a.NeuronIncome, Is.EqualTo(1));
        b.owner = enemy;
        turns.Neurons.Invalidate();
        Assert.That(a.NeuronIncome, Is.Zero);
        Assert.That(b.NeuronIncome, Is.Zero);
        Assert.That(segment.owner, Is.SameAs(player));
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(b.NeuronIncome, Is.EqualTo(1));
    }

    [Test]
    public void CityIncomeIncludesNetworkButSiegedCityDoesNotPay()
    {
        var a = City(Tile(0), player);
        var b = City(Tile(2), player);
        player.cities.AddRange(new[] { a, b });
        Segment(1, 0, player);
        Assert.That(player.CalculateTurnIncome(), Is.EqualTo(6));
        a.SetPendingCapture(Unit(enemy, a.centerTile));
        Assert.That(player.CalculateTurnIncome(), Is.EqualTo(3));
    }
}
