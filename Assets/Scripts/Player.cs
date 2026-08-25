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

            totalIncome += city.BaseIncome;
        }
        return totalIncome;
    }

    public void RemoveCity(City city)
    {
        cities.Remove(city);
    }

    public bool IsAlive()
    {
        return cities.Count > 0 || units.Count > 0;
    }
}