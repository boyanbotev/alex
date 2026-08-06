using UnityEngine;
using System.Collections.Generic;

public class City : MonoBehaviour
{
    public string cityName;
    public Player owner;
    public Tile centerTile;

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

        // Spawned units cannot move or attack on the same turn
        unit.hasMoved = true;
        unit.hasAttacked = true;

        owner.units.Add(unit);
        return true;
    }

    public void ClaimVillage(Player claimingPlayer)
    {
        if (owner != null) return; // Already owned by someone

        owner = claimingPlayer;
        claimingPlayer.cities.Add(this);
        cityName = $"{claimingPlayer.factionName} Town";

        // Reward player with immediate stars or level up
        claimingPlayer.AddStars(2);

        Debug.Log($"{claimingPlayer.factionName} captured a neutral village!");
    }
}