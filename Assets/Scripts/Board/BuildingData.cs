using UnityEngine;

[CreateAssetMenu(fileName = "NewBuilding", menuName = "Building/Building Data")]
public class BuildingData : ScriptableObject
{
    public string buildingName;
    public GameObject buildingPrefab;

    [TextArea]
    public string description;

    public int cost;
    public int populationGiven;

    [Tooltip("Tech required to unlock this building. Leave empty if it's available from the start.")]
    public TechData requiredTech;

    [Tooltip("Every condition here must pass for the building to be placeable on a tile. " +
             "See the Conditions folder for available condition types.")]
    public BuildingPlacementCondition[] placementConditions;

    public bool CanPlaceAt(Tile tile, City city)
    {
        if (placementConditions == null) return true;

        foreach (BuildingPlacementCondition condition in placementConditions)
        {
            if (condition != null && !condition.IsSatisfied(tile, city))
            {
                return false;
            }
        }

        return true;
    }
}
