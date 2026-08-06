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

    [Header("Visual Feedback")]
    [SerializeField] private GameObject highlight;

    public void SetHighlight(bool active, Color color)
    {
        if (highlight != null)
        {
            highlight.gameObject.SetActive(active);
            // TODO: get to work with materials
            highlight.gameObject.GetComponent<MeshRenderer>().material.color = color;
        }
    }
}