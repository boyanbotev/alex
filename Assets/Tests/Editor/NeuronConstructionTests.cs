using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class NeuronConstructionTests : TacticsTestFixture
{
    private BuildingData neuron;
    private readonly NeuronConstructionPlanner planner = new();
    private readonly List<EconomyCandidateAction> candidates = new();

    private void Configure()
    {
        player.isAI = true;
        player.visibleTiles = new VisibilityState(20, 20);
        player.faction = Asset<Faction>();
        player.faction.availableUnits = System.Array.Empty<FactionUnit>();
        player.faction.availableTech = System.Array.Empty<TechData>();
        neuron = Asset<BuildingData>();
        neuron.isNeuron = true;
        neuron.cost = 3;
        neuron.buildingPrefab = Component<Building>().gameObject;
        player.faction.availableBuildings = new[] { neuron };
    }

    private Tile Visible(int x, int y = 0)
    {
        var tile = Tile(x, y);
        player.visibleTiles.SetVisible(tile.gridPosition);
        return tile;
    }

    private City Endpoint(Tile tile, Player owner)
    {
        var city = City(tile, owner);
        if (owner != null) owner.cities.Add(city);
        return city;
    }

    private Building Road(Tile tile, Player owner)
    {
        var building = Component<Building>();
        building.tile = tile;
        building.owner = owner;
        building.data = neuron;
        tile.currentBuilding = building;
        return building;
    }

    private List<EconomyCandidateAction> Plan()
    {
        candidates.Clear();
        planner.GenerateCandidates(player, profile, candidates);
        return candidates;
    }

    [TearDown]
    public void RemovePurchasedBuildings()
    {
        // Purchases instantiate objects independently of the fixture's object registry.
        foreach (var tile in grid.grid.Values)
            if (tile != null && tile.currentBuilding != null)
                Object.DestroyImmediate(tile.currentBuilding.gameObject);
    }

    [Test]
    public void EconomyCompletesPartialRouteAcrossTurnsAndStopsOnceConnected()
    {
        Configure();
        var a = Endpoint(Visible(0), player);
        var one = Visible(1); var two = Visible(2); var three = Visible(3);
        var b = Endpoint(Visible(4), player);
        var economy = Component<EconomyAI>();
        player.stars = 3;
        economy.HandleEconomy(player, profile);
        Assert.That(one.currentBuilding, Is.Not.Null);
        Assert.That(two.currentBuilding, Is.Null);
        Assert.That(a.NeuronIncome, Is.Zero);
        player.stars = 3;
        economy.HandleEconomy(player, profile);
        Assert.That(two.currentBuilding, Is.Not.Null);
        player.stars = 3;
        economy.HandleEconomy(player, profile);
        Assert.That(three.currentBuilding, Is.Not.Null);
        Assert.That(player.stars, Is.Zero);
        Assert.That(turns.Neurons.AreConnected(a, b), Is.True);
        Assert.That(Plan(), Is.Empty);
        player.stars = 10;
        economy.HandleEconomy(player, profile);
        Assert.That(player.stars, Is.EqualTo(10));
    }

    [Test]
    public void ExistingRoadsMakeLongerRouteCheaperThanDirectConstruction()
    {
        Configure();
        Endpoint(Visible(0, 0), player);
        Endpoint(Visible(4, 0), player);
        Visible(1, 0); Visible(2, 0); Visible(3, 0);
        Road(Visible(0, 1), player); Road(Visible(1, 2), player);
        Road(Visible(2, 2), player); Road(Visible(3, 2), player);
        var gap = Visible(4, 1);
        Assert.That(Plan()[0].buildTile, Is.SameAs(gap));
        Assert.That(candidates[0].score, Is.EqualTo(profile.neuronConstructionWeight * 2f / 3f).Within(0.001f));
    }

    [Test]
    public void PeacefulConnectionCountsOnlyOurEndpointIncomeAndUsesDiagonals()
    {
        Configure();
        Endpoint(Visible(0, 0), player);
        var gap = Visible(1, 1);
        Endpoint(Visible(2, 2), enemy);
        Assert.That(Plan(), Is.Empty);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(Plan()[0].buildTile, Is.SameAs(gap));
        Assert.That(candidates[0].score, Is.EqualTo(profile.neuronConstructionWeight / 3f).Within(0.001f));
    }

    [Test]
    public void ThirdPartyRoadMustPermitBothEndpointOwners()
    {
        Configure();
        var third = Component<Player>(); turns.players.Add(third);
        Endpoint(Visible(0), player);
        Visible(1);
        Road(Visible(2), third);
        Endpoint(Visible(3), enemy);
        turns.Diplomacy.MakePeace(player, enemy);
        turns.Diplomacy.MakePeace(player, third);
        Assert.That(Plan(), Is.Empty);
        turns.Diplomacy.MakePeace(enemy, third);
        Assert.That(Plan(), Is.Not.Empty);
    }

    [Test]
    public void PeacefulRoadDoesNotAnchorOurConstructionBeyondIt()
    {
        Configure();
        Endpoint(Visible(0), player);
        Road(Visible(1), enemy);
        Visible(2);
        Endpoint(Visible(3), enemy);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(Plan(), Is.Empty);
    }

    [Test]
    public void HiddenCityOrGapUnclaimedCityAndOccupiedBuildingBlockPlanning()
    {
        Configure();
        Endpoint(Visible(0), player);
        var middle = Tile(1);
        var destination = Endpoint(Tile(2), enemy);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(Plan(), Is.Empty);
        player.visibleTiles.SetVisible(destination.centerTile.gridPosition);
        Assert.That(Plan(), Is.Empty);
        player.visibleTiles.SetVisible(middle.gridPosition);
        Assert.That(Plan(), Is.Not.Empty);
        var blocker = Endpoint(middle, null);
        Assert.That(Plan(), Is.Empty);
        middle.city = null;
        population.allCities.Remove(blocker);
        var building = Road(middle, player);
        building.data = Asset<BuildingData>();
        Assert.That(Plan(), Is.Empty);
    }

    [Test]
    public void TechBudgetAndEnemyTerritoryAreRespected()
    {
        Configure();
        Endpoint(Visible(0), player);
        var gap = Visible(1);
        Endpoint(Visible(2), player);
        neuron.requiredTech = Asset<TechData>();
        Assert.That(Plan(), Is.Empty);
        neuron.requiredTech = null;
        profile.neuronMaxPaybackTurns = 1;
        Assert.That(Plan(), Is.Empty); // Cost 3, income 2.
        profile.neuronMaxPaybackTurns = 2;
        Assert.That(Plan(), Is.Not.Empty);
        player.stars = 2;
        Assert.That(Plan(), Is.Empty);
        player.stars = 3;
        gap.territoryCity = Endpoint(Visible(9), enemy);
        Assert.That(Plan(), Is.Empty);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(Plan(), Is.Not.Empty);
    }
}
