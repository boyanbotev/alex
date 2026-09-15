using UnityEngine;
using System.Collections.Generic;
using System;

public class City : MonoBehaviour
{
    public static event Action<Player> OnPlayerChange;
    public static event Action<Player> OnUnsiege;
    public static event Action<Player> OnSiege;
    public string cityName;
    public Player owner;
    public Tile centerTile;
    public Transform model;
    public PopulationUI populationUI;
    public CityStarsUI starsUI;
    public List<Unit> units = new List<Unit>();
    public List<Building> buildings = new List<Building>();

    [Header("Territory")]
    [Tooltip("How many tiles out from centerTile belong to this city's territory.")]
    public int territoryRadius = 1;

    [Header("Capture")]
    public Unit pendingCapturer;

    public bool HasPendingCapture =>
        pendingCapturer != null &&
        pendingCapturer.isAlive &&
        pendingCapturer.currentTile == centerTile &&
        InteractionRules.CanCapture(pendingCapturer.owner, owner);

    public int BaseIncome => 2;
    public int NeuronIncome => TurnManager.Instance != null ? TurnManager.Instance.Neurons.GetIncome(this) : 0;
    public int TotalIncome => HasPendingCapture ? 0 : BaseIncome + NeuronIncome;
    public int UnitCapacity => TotalIncome;
    public void RefreshIncomeLabel()
    {
        if (starsUI != null) starsUI.Set(cityName, TotalIncome);
    }

    private void Start()
    {
        if (populationUI != null) populationUI.gameObject.SetActive(false);
        RefreshIncomeLabel();
    }

    public void Reveal()
    {
        starsUI.gameObject.SetActive(true);
    }

    public void Hide()
    {
        starsUI.gameObject.SetActive(false);
    }

    public void ClaimTerritory()
    {
        if (centerTile == null) return;

        centerTile.territoryCity = this;

        List<Tile> tilesInRange = GridManager.Instance.GetTilesInRange(centerTile, territoryRadius);
        foreach (Tile tile in tilesInRange)
        {
            if (tile.territoryCity == null)
            {
                tile.territoryCity = this;
            }
        }
    }

    public bool SpawnUnit(FactionUnit factionUnit, int cost)
    {
        if (centerTile.currentUnit != null)
        {
            return false;
        }

        if (units.Count >= UnitCapacity)
        {
            return false;
        }

        if (!owner.techState.CanSpawn(factionUnit.unitData))
        {
            return false;
        }

        if (!owner.SpendStars(cost))
        {
            return false;
        }


        GameObject unitObj = Instantiate(factionUnit.prefab, centerTile.transform.position, Quaternion.identity);
        Unit unit = unitObj.GetComponent<Unit>();

        unit.owner = owner;
        unit.currentTile = centerTile;
        centerTile.currentUnit = unit;
        unit.homeCity = this;
        units.Add(unit);

        unit.hasMoved = true;
        unit.hasAttacked = true;
        unit.Deactivate();

        owner.unitsCreated++;
        unit.name = unit.owner.faction.name + " " + unit.data.name + " " + owner.unitsCreated; 

        owner.units.Add(unit);
        return true;
    }

    public bool PlaceBuilding(BuildingData buildingData, Tile targetTile)
    {
        if (buildingData == null || targetTile == null) return false;
        if (buildingData.isNeuron) return owner != null && owner.PlaceNeuron(buildingData, targetTile);
        if (owner == null || targetTile.territoryCity != this || targetTile.city != null) return false;

        if (targetTile == centerTile)
        {
            return false;
        }

        if (targetTile.currentBuilding != null)
        {
            return false;
        }

        if (!owner.techState.CanBuild(buildingData))
        {
            return false;
        }

        if (!buildingData.CanPlaceAt(targetTile, this))
        {
            return false;
        }

        if (!owner.SpendStars(buildingData.cost))
        {
            return false;
        }

        GameObject buildingObj = Instantiate(buildingData.buildingPrefab, targetTile.transform.position, Quaternion.identity);
        Building building = buildingObj.GetComponent<Building>();
        building.Initialize(buildingData, targetTile, this);

        targetTile.currentBuilding = building;
        buildings.Add(building);

        return true;
    }

    public void SetPendingCapture(Unit unit)
    {
        if (unit == null) return;
        if (!InteractionRules.CanCapture(unit.owner, owner)) return;

        pendingCapturer = unit;

        OnSiege?.Invoke(owner);
        RefreshIncomeLabel();
}

    public bool ResolvePendingCapture(bool showUI)
    {
        if (!HasPendingCapture)
        {
            pendingCapturer = null;
            return false;
        }

        Unit capturer = pendingCapturer;
        pendingCapturer = null;

        if (showUI)
        {
            UIManager.Instance.ShowCaptureButton(this, capturer);
        }
        else
        {
            Capture(capturer);
        }

        return true;
    }

    public void ClearPendingCapture()
    {
        if (pendingCapturer == null)
            return;

        pendingCapturer = null;
        OnUnsiege?.Invoke(owner);
        RefreshIncomeLabel();
    }

    public void Capture(Unit capturer)
    {
        if (capturer == null || !capturer.isAlive
            || !InteractionRules.CanCapture(capturer.owner, owner))
            return;

        if (capturer.currentTile != centerTile)
            return;

        Claim(capturer.owner);
        capturer.Deactivate();
        capturer.hasCaptured = true;
    }

    public void Claim(Player claimingPlayer)
    {
        if (owner == claimingPlayer) return;

        pendingCapturer = null;

        if (owner != null) owner.RemoveCity(this);

        foreach (Unit unit in units)
        {
            unit.homeCity = null;
        }
        units.Clear();

        owner = claimingPlayer;
        claimingPlayer.cities.Add(this);

        SetFaction(claimingPlayer.faction);

        FogOfWarManager.Instance.Reveal(claimingPlayer, centerTile, 2);
        TerritoryBorderManager.Instance.RebuildAllBorders(TurnManager.Instance.players);

        TurnManager.Instance.Neurons.Invalidate();
        OnPlayerChange?.Invoke(claimingPlayer);
    }

    public void SetFaction(Faction faction)
    {
        var cityModel = Instantiate(faction.cityPrefab, transform);
        cityModel.transform.position = model.position;
        Destroy(model.gameObject);
        model = cityModel.transform;
    }
}
