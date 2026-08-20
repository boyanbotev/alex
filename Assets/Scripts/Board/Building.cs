using UnityEngine;

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
