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
        int cx = startTile.gridPosition.x;
        int cy = startTile.gridPosition.y;
        int side = range * 2 + 1;

        List<Tile> inRange = new List<Tile>(side * side - 1);

        for (int x = cx - range; x <= cx + range; x++)
        {
            for (int y = cy - range; y <= cy + range; y++)
            {
                if (x == cx && y == cy) continue;

                if (grid.TryGetValue(new Vector2Int(x, y), out Tile tile))
                {
                    inRange.Add(tile);
                }
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