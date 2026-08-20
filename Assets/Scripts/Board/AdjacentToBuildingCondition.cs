using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AdjacentToBuilding", menuName = "Building/Placement Conditions/Adjacent To Building")]
public class AdjacentToBuildingCondition : BuildingPlacementCondition
{
    [Tooltip("Leave empty to require ANY building nearby.")]
    public BuildingData requiredBuilding;

    public override bool IsSatisfied(Tile tile, City city)
    {
        List<Tile> neighbours = GridManager.Instance.GetTilesInRange(tile, 1);

        foreach (Tile neighbour in neighbours)
        {
            if (neighbour == tile || neighbour.currentBuilding == null) continue;

            if (requiredBuilding == null || neighbour.currentBuilding.data == requiredBuilding)
            {
                return true;
            }
        }

        return false;
    }
}
