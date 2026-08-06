using UnityEngine;
using System.Collections.Generic;

public class City : MonoBehaviour
{
    public string cityName;
    public Player owner;
    public Tile centerTile;
    public Transform model;
    public List<Unit> units = new List<Unit>();
    public int level = 1;
    public int currentPopulation = 0;
    public int populationToLevelUp = 2;

    public int BaseIncome => level + 1; // Polytopia star generation formula

    public void AddPopulation(int amount)
    {
        currentPopulation += amount;
        if (currentPopulation >= populationToLevelUp)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        currentPopulation -= populationToLevelUp;
        level++;
        populationToLevelUp = level + 1;
        owner.AddStars(3); // Level-up reward
        Debug.Log($"{cityName} leveled up to Level {level}!");
    }

    public bool SpawnUnit(GameObject unitPrefab, int cost)
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

        if (!owner.SpendStars(cost))
        {
            Debug.Log("Not enough Stars!");
            return false;
        }

        GameObject unitObj = Instantiate(unitPrefab, centerTile.transform.position, Quaternion.identity);
        Unit unit = unitObj.GetComponent<Unit>();

        unit.owner = owner;
        unit.currentTile = centerTile;
        centerTile.currentUnit = unit;
        unit.homeCity = this;
        units.Add(unit);

        // Spawned units cannot move or attack on the same turn
        unit.hasMoved = true;
        unit.hasAttacked = true;
        unit.Deactivate();

        owner.units.Add(unit);
        return true;
    }

    public void Claim(Player claimingPlayer)
    {
        if (owner == claimingPlayer) return;

        if (owner != null) owner.RemoveCity(this);

        owner = claimingPlayer;
        claimingPlayer.cities.Add(this);
        cityName = $"{claimingPlayer.factionName} Town";

        SetFaction(claimingPlayer.faction);

        // Reward player with immediate stars or level up
        claimingPlayer.AddStars(2);

        Debug.Log($"{claimingPlayer.factionName} captured a neutral village!");
    }

    public void SetFaction(Faction faction)
    {
        var cityModel = Instantiate(faction.cityPrefab, transform);
        cityModel.transform.position = model.position;
        Destroy(model.gameObject);
        model = cityModel.transform;
    }
}