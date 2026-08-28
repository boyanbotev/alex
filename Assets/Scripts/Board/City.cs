using UnityEngine;
using System.Collections.Generic;

public class City : MonoBehaviour
{
    public string cityName;
    public Player owner;
    public Tile centerTile;
    public Transform model;
    public PopulationUI populationUI;
    public CityStarsUI starsUI;
    public List<Unit> units = new List<Unit>();
    public List<Building> buildings = new List<Building>();
    public int level = 1;
    public int currentPopulation = 0;
    public int populationToLevelUp = 2;

    [Header("Territory")]
    [Tooltip("How many tiles out from centerTile belong to this city's territory. Grows with level in LevelUp().")]
    public int territoryRadius = 1;

    [Header("Capture")]
    public Unit pendingCapturer;

    public bool HasPendingCapture =>
        pendingCapturer != null &&
        pendingCapturer.isAlive &&
        pendingCapturer.currentTile == centerTile &&
        pendingCapturer.owner != owner;

    public int BaseIncome => level + 1;

    private void Start()
    {
        populationUI.Set(currentPopulation, populationToLevelUp);
        starsUI.Set(BaseIncome);
    }

    public void Reveal()
    {
        populationUI.gameObject.SetActive(true);
        starsUI.gameObject.SetActive(true);
    }

    public void Hide()
    {
        populationUI.gameObject.SetActive(false);
        starsUI.gameObject.SetActive(false);
    }

    public void AddPopulation(int amount)
    {
        currentPopulation += amount;
        if (currentPopulation >= populationToLevelUp)
        {
            LevelUp();
        }

        populationUI.Set(currentPopulation, populationToLevelUp);
    }

    private void LevelUp()
    {
        currentPopulation -= populationToLevelUp;
        level++;
        populationToLevelUp = level + 1;
        Debug.Log($"{cityName} leveled up to Level {level}!");

        starsUI.Set(BaseIncome);
    }

    public void ClaimTerritory()
    {
        if (centerTile == null) return;

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
            Debug.Log("City space is occupied!");
            return false;
        }

        if (units.Count >= level + 1)
        {
            Debug.Log("City cannto create more units");
            return false;
        }

        if (!owner.techState.CanSpawn(factionUnit.unitData))
        {
            Debug.Log($"{factionUnit.unitData.requiredTech.name} has not been researched yet!");
            return false;
        }

        if (!owner.SpendStars(cost))
        {
            Debug.Log("Not enough Stars!");
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

        if (targetTile == centerTile)
        {
            Debug.Log("Cannot place a building on the city center tile.");
            return false;
        }

        if (targetTile.currentBuilding != null)
        {
            Debug.Log("Tile already has a building!");
            return false;
        }

        if (!owner.techState.CanBuild(buildingData))
        {
            Debug.Log($"{buildingData.requiredTech.name} has not been researched yet!");
            return false;
        }

        if (!buildingData.CanPlaceAt(targetTile, this))
        {
            Debug.Log($"Cannot place {buildingData.buildingName} here - placement conditions not met.");
            return false;
        }

        if (!owner.SpendStars(buildingData.cost))
        {
            Debug.Log("Not enough Stars!");
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
        if (owner == unit.owner) return;

        pendingCapturer = unit;

        Debug.Log(
            $"{unit.owner.factionName} is occupying {cityName}. " +
            $"Capture will resolve at the start of their next turn."
        );
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

    public void Capture(Unit capturer)
    {
        if (capturer == null || !capturer.isAlive)
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
        cityName = $"{claimingPlayer.factionName} Town";

        SetFaction(claimingPlayer.faction);

        FogOfWarManager.Instance.Reveal(claimingPlayer, centerTile, 2);

        Debug.Log($"{claimingPlayer.factionName} captured a city!");
    }

    public void SetFaction(Faction faction)
    {
        var cityModel = Instantiate(faction.cityPrefab, transform);
        cityModel.transform.position = model.position;
        Destroy(model.gameObject);
        model = cityModel.transform;
    }
}