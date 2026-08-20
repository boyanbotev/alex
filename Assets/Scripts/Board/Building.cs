using UnityEngine;

// Sits on the instantiated building prefab, analogous to how Unit sits on
// spawned unit prefabs. Created and initialized by City.PlaceBuilding.
public class Building : MonoBehaviour
{
    public BuildingData data;
    public Tile tile;
    public City parentCity;

    public void Initialize(BuildingData buildingData, Tile placedTile, City owningCity)
    {
        data = buildingData;
        tile = placedTile;
        parentCity = owningCity;

        if (parentCity != null && data.populationGiven > 0)
        {
            parentCity.AddPopulation(data.populationGiven);
        }
    }
}
