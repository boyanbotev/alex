using UnityEngine;

public class Building : MonoBehaviour
{
    public BuildingData data;
    public Tile tile;
    public City parentCity;
    public Player owner;
    public int paidCost;
    public int DemolitionRefund => data == null ? 0 : Mathf.FloorToInt(paidCost * data.demolitionRefundFraction);

    public void Initialize(BuildingData buildingData, Tile placedTile, City owningCity)
    {
        data = buildingData;
        tile = placedTile;
        parentCity = owningCity;
        owner = owningCity != null ? owningCity.owner : null;
        paidCost = data.cost;

        if (!data.isNeuron && parentCity != null && data.populationGiven > 0)
        {
            parentCity.AddPopulation(data.populationGiven);
        }
    }

    private void OnDestroy()
    {
        if (tile != null && tile.currentBuilding == this) tile.currentBuilding = null;
        if (data != null && data.isNeuron && TurnManager.Instance != null)
            TurnManager.Instance.Neurons.Invalidate();
    }
}
