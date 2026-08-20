using UnityEngine;
public abstract class BuildingPlacementCondition : ScriptableObject
{
    public abstract bool IsSatisfied(Tile tile, City city);
}
