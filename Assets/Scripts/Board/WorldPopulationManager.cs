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
        Level level = GameManager.Instance.Level;
        if (villagePrefab == null || villagePrefab.GetComponent<City>() == null)
            throw new System.InvalidOperationException("WorldPopulationManager needs a village prefab with a City component.");
        WorldLoadingOverlay.Show("Placing cities...");
        var placement = new CityPlacement();
        yield return placement.Generate(level, GridManager.Instance.grid.Values, GridGenerator.Instance.GenerationRandom, budget);

        int index = 0;
        for (int f = 0; f < level.factions.Length; f++)
        {
            Player player = TurnManager.Instance.players[f];
            foreach (CityData cityData in level.factions[f].startingCities)
            {
                if (budget.ShouldYield()) { yield return null; budget.ShouldYield(); }
                SpawnCity(placement.Positions[index++], cityData, player);
            }
            TerritoryBorderManager.Instance.RebuildBorder(player);
        }
        if (level.neutralCities != null)
            foreach (CityData cityData in level.neutralCities)
            {
                if (budget.ShouldYield()) { yield return null; budget.ShouldYield(); }
                SpawnCity(placement.Positions[index++], cityData, null);
            }
        if (level.map != null && level.map.units != null)
            foreach (var entry in level.map.units)
            {
                if (budget.ShouldYield()) { yield return null; budget.ShouldYield(); }
                Player player = TurnManager.Instance.players[entry.factionIndex];
                Tile tile = GridManager.Instance.GetTileAt(entry.position);
                if (tile == null || tile.terrainType == TerrainType.Mountain || tile.currentUnit != null ||
                    (tile.city != null && tile.city.owner != player))
                    throw new System.InvalidOperationException($"Cannot place '{entry.unit.name}' at {entry.position}: blocked tile or foreign city.");
                SpawnInitialUnit(player, entry.unit, tile, tile.city);
            }
        for (int f = 0; f < level.factions.Length; f++)
        {
            Player player = TurnManager.Instance.players[f];
            if (!level.factions[f].spawnCapitalUnit || player.cities.Count == 0) continue;
            City capital = player.cities[0];
            if (capital.centerTile.currentUnit == null)
                SpawnInitialUnit(player, player.faction.startingUnit, capital.centerTile, capital);
        }
        WorldLoadingOverlay.Show("Creating fog...");
        yield return FogOfWarManager.Instance.CreateFogTiles(budget);
    }

    private City SpawnCity(Tile tile, CityData cityData, Player owner)
    {
        GameObject cityObj;
        using (creationMarker.Auto())
            cityObj = Instantiate(villagePrefab, tile.transform.position, Quaternion.identity, tile.transform);
        City city = cityObj.GetComponent<City>();
        city.data = cityData;
        city.cityName = cityData.cityName;
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

    private void SpawnInitialUnit(Player player, FactionUnit profile, Tile tile, City home)
    {
        GameObject unitObj = Instantiate(profile.prefab, tile.transform.position, Quaternion.identity);
        Unit unit = unitObj.GetComponent<Unit>();
        unit.data = profile.unitData;
        unit.owner = player;
        unit.currentTile = tile;
        tile.currentUnit = unit;
        unit.homeCity = home;
        unit.dopamineBonus = home != null ? home.PerkAmount(CityPerkKind.Dopamine) : 0;
        unit.hasMoved = false;
        unit.hasAttacked = false;
        if (home != null) home.units.Add(unit);
        player.units.Add(unit);
    }
}
