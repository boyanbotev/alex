using UnityEngine;
using Unity.Profiling;
using System.Collections;
using System.Collections.Generic;

public class WorldPopulationManager : MonoBehaviour
{
    public static WorldPopulationManager Instance;
    private static readonly ProfilerMarker creationMarker = new ProfilerMarker("WorldGeneration.City");
    public GameObject villagePrefab;
    [System.NonSerialized] public List<City> allCities = new List<City>();

    private void Awake() => Instance = this;

    public IEnumerator PopulateWorld(GenerationBudget budget)
    {
        Level level = GridGenerator.Instance.level;
        if (villagePrefab == null || villagePrefab.GetComponent<City>() == null)
            throw new System.InvalidOperationException("WorldPopulationManager needs a village prefab with a City component.");
        WorldLoadingOverlay.Show("Placing cities...");
        var placement = new CityPlacement();
        yield return placement.Generate(level, GridManager.Instance.grid.Values, GridGenerator.Instance.GenerationRandom, budget);

        int index = 0;
        for (int f = 0; f < level.factions.Length; f++)
        {
            Player player = TurnManager.Instance.players[f];
            foreach (string cityName in level.factions[f].startingCityNames)
            {
                if (budget.ShouldYield()) { yield return null; budget.ShouldYield(); }
                City city = SpawnCity(placement.Positions[index++], cityName, player);
                if (player.cities.Count == 1) SpawnStartingUnit(player, city);
            }
            TerritoryBorderManager.Instance.RebuildBorder(player);
        }
        if (level.neutralCityNames != null)
            foreach (string cityName in level.neutralCityNames)
            {
                if (budget.ShouldYield()) { yield return null; budget.ShouldYield(); }
                SpawnCity(placement.Positions[index++], cityName, null);
            }
        WorldLoadingOverlay.Show("Creating fog...");
        yield return FogOfWarManager.Instance.CreateFogTiles(budget);
    }

    private City SpawnCity(Tile tile, string cityName, Player owner)
    {
        GameObject cityObj;
        using (creationMarker.Auto())
            cityObj = Instantiate(villagePrefab, tile.transform.position, Quaternion.identity, tile.transform);
        City city = cityObj.GetComponent<City>();
        city.cityName = cityName;
        city.centerTile = tile;
        city.owner = owner;
        tile.city = city;
        city.ClaimTerritory();
        allCities.Add(city);
        if (owner != null)
        {
            owner.cities.Add(city);
            city.SetFaction(owner.faction);
        }
        return city;
    }

    private void SpawnStartingUnit(Player player, City capital)
    {
        GameObject unitObj = Instantiate(player.faction.startingUnit.prefab, capital.centerTile.transform.position, Quaternion.identity);
        Unit unit = unitObj.GetComponent<Unit>();
        unit.owner = player;
        unit.currentTile = capital.centerTile;
        capital.centerTile.currentUnit = unit;
        unit.homeCity = capital;
        unit.hasMoved = false;
        unit.hasAttacked = false;
        capital.units.Add(unit);
        player.units.Add(unit);
    }
}
