using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;
    public Dictionary<Vector2Int, Tile> grid = new Dictionary<Vector2Int, Tile>();

    private void Awake()
    {
        Instance = this;
    }

    public Tile GetTileAt(Vector2Int pos)
    {
        grid.TryGetValue(pos, out Tile tile);
        return tile;
    }

    public List<Tile> GetTilesInRange(Tile startTile, int range)
    {
        List<Tile> inRange = new List<Tile>();

        foreach (var kvp in grid)
        {
            int dx = Mathf.Abs(kvp.Key.x - startTile.gridPosition.x);
            int dy = Mathf.Abs(kvp.Key.y - startTile.gridPosition.y);
            int distance = Mathf.Max(dx, dy); // Chebyshev distance

            if (distance <= range && distance > 0)
            {
                inRange.Add(kvp.Value);
            }
        }
        return inRange;
    }

    public void ClearAllHighlights()
    {
        foreach (var tile in grid.Values)
        {
            tile.SetHighlight(false, Color.white);
        }
    }
}