using UnityEngine;
using System.Collections.Generic;

public class Player : MonoBehaviour
{
    public string factionName;
    public Faction faction;
    public Color factionColor;
    public int stars = 5;

    public List<City> cities = new List<City>();
    public List<Unit> units = new List<Unit>();

    public void AddStars(int amount) => stars += amount;

    public bool SpendStars(int amount)
    {
        if (stars >= amount)
        {
            stars -= amount;
            return true;
        }
        return false;
    }

    public int CalculateTurnIncome()
    {
        int totalIncome = 0;
        foreach (var city in cities)
        {
            totalIncome += city.BaseIncome;
        }
        return totalIncome;
    }
}