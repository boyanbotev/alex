using UnityEngine;

public class Building : MonoBehaviour
{
    public BuildingData data;
    public Tile tile;
    public City parentCity;
    public Player owner;
    public int paidCost;
    public int DemolitionRefund => data == null ? 0 : Mathf.FloorToInt(Mathf.Max(0, paidCost) * Mathf.Clamp01(data.demolitionRefundFraction));
    public bool IsPlacedNeuron => this != null && data != null && data.isNeuron && tile != null && tile.currentBuilding == this;

    public bool CanDemolish(Player actor) => IsPlacedNeuron && actor != null && actor == owner &&
        TurnManager.Instance != null && TurnManager.Instance.ActivePlayer == actor &&
        actor.visibleTiles != null && actor.visibleTiles.IsVisible(tile);

    public bool TryDemolish(Player actor)
    {
        if (!CanDemolish(actor)) return false;
        RemoveNeuron(actor);
        return true;
    }

    // Call only after action validation. Clear occupancy before notifying listeners or paying out.
    internal void RemoveNeuron(Player recipient)
    {
        if (!IsPlacedNeuron) return;
        Tile previousTile = tile;
        previousTile.currentBuilding = null;
        gameObject.SetActive(false);
        NeuronSegmentVisual.RefreshAround(previousTile);
        TurnManager.Instance.Neurons.Invalidate();
        recipient.AddStars(DemolitionRefund);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    public void Initialize(BuildingData buildingData, Tile placedTile, City owningCity)
    {
        data = buildingData;
        tile = placedTile;
        parentCity = owningCity;
        owner = owningCity != null ? owningCity.owner : null;
        paidCost = data.cost;

    }

    private void OnDestroy()
    {
        bool occupied = tile != null && tile.currentBuilding == this;
        if (occupied) tile.currentBuilding = null;
        if (occupied && data != null && data.isNeuron && TurnManager.Instance != null)
        {
            NeuronSegmentVisual.RefreshAround(tile);
            TurnManager.Instance.Neurons.Invalidate();
        }
    }
}
