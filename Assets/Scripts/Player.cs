using UnityEngine;
using System.Collections.Generic;
using System;

public class Player : MonoBehaviour
{
    public static event Action<int> OnUpdateStars;
    public string factionName;
    public Faction faction;
    public Color factionColor;
    public int stars = 5;
    public bool isAI;
    public int unitsCreated = 0;

    public List<City> cities = new List<City>();
    public List<Unit> units = new List<Unit>();
    public PlayerTechState techState = new PlayerTechState();
    public VisibilityState visibleTiles;

    public void AddStars(int amount)
    {
        stars += amount;
        OnUpdateStars?.Invoke(stars);
    }

    public bool SpendStars(int amount)
    {
        if (stars >= amount)
        {
            stars -= amount;
            OnUpdateStars?.Invoke(stars);
            return true;
        }
        return false;
    }

    public int CalculateTurnIncome()
    {
        int totalIncome = 0;
        foreach (var city in cities)
        {
            if (city.HasPendingCapture) continue;

            totalIncome += city.TotalIncome;
        }
        return totalIncome;
    }

    public void RemoveCity(City city)
    {
        cities.Remove(city);
    }

    public bool CanPlaceNeuron(BuildingData data, Tile tile)
    {
        if (!CanPlaceNeuronSite(data, tile)) return false;
        return HasNeuronBuildAnchor(tile);
    }

    // Shared with route planning, where earlier planned segments provide the anchor.
    public bool CanPlaceNeuronSite(BuildingData data, Tile tile)
    {
        if (data == null || !data.isNeuron || !techState.CanBuild(data) || tile == null ||
            tile.city != null || tile.currentBuilding != null || faction == null || faction.availableBuildings == null ||
            System.Array.IndexOf(faction.availableBuildings, data) < 0) return false;
        if (visibleTiles == null || !visibleTiles.IsVisible(tile)) return false;
        var diplomacy = TurnManager.Instance.Diplomacy;
        if (tile.territoryCity != null && diplomacy.IsAtWar(this, tile.territoryCity.owner)) return false;
        return true;
    }

    public bool HasNeuronBuildAnchor(Tile tile)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            Tile neighbour = GridManager.Instance.GetTileAt(tile.gridPosition + new Vector2Int(dx, dy));
            if (neighbour == null) continue;
            if (neighbour.city != null && neighbour.city.owner == this) return true;
            Building segment = neighbour.currentBuilding;
            if (segment != null && segment.data != null && segment.data.isNeuron && segment.owner == this) return true;
        }
        return false;
    }

    public bool PlaceNeuron(BuildingData data, Tile tile)
    {
        if (TurnManager.Instance.ActivePlayer != this || !CanPlaceNeuron(data, tile) ||
            data.buildingPrefab == null || data.buildingPrefab.GetComponent<Building>() == null ||
            data.cost < 0 || !SpendStars(data.cost)) return false;
        var building = Instantiate(data.buildingPrefab, tile.transform.position, Quaternion.identity).GetComponent<Building>();
        building.Initialize(data, tile, null);
        building.owner = this;
        tile.currentBuilding = building;
        NeuronSegmentVisual.RefreshAround(tile);
        TurnManager.Instance.Neurons.Invalidate();
        return true;
    }

    public bool IsAlive()
    {
        return cities.Count > 0 || units.Count > 0;
    }
}
