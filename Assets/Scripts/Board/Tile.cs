using UnityEngine;

public enum TerrainType { Field, Forest, Mountain, Water }

public class Tile : MonoBehaviour
{
    public Vector2Int gridPosition;
    public TerrainType terrainType;
    public int movementCost = 1;

    [Header("Occupants")]
    public Unit currentUnit;
    public City city;
    public Building currentBuilding;

    [Header("Territory")]
    [Tooltip("The city whose territory this tile belongs to (used for building placement). " +
         "Distinct from 'city' above, which is only set on the tile a city is physically built on.")]
    public City territoryCity;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject highlight;

    public void SetHighlight(bool active, Color color)
    {
        if (highlight != null)
        {
            highlight.gameObject.SetActive(active);
            highlight.gameObject.GetComponent<MeshRenderer>().material.color = color;
        }
    }
}