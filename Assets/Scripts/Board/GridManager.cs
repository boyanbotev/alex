using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;
    public Dictionary<Vector2Int, Tile> grid = new Dictionary<Vector2Int, Tile>();
    private readonly List<(Tile tile, int distance)> moveQueue = new(64);
    private readonly HashSet<Tile> moveVisited = new();

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

    // Reuses search buffers; results are cleared and filled with empty destinations only.
    public void GetReachableMoveTiles(Tile start, Player owner, int range,
        System.Func<Tile, Unit> getOccupant, List<Tile> results, System.Func<Player, Player, bool> isAtWar = null)
    {
        results.Clear();
        moveQueue.Clear();
        moveVisited.Clear();
        if (start == null || range <= 0) return;

        Level settings = GridGenerator.Instance != null ? GridGenerator.Instance.level : null;
        bool blockCorners = settings == null || settings.blockDiagonalsBetweenEnemies;
        moveQueue.Add((start, 0));
        moveVisited.Add(start);

        for (int i = 0; i < moveQueue.Count; i++)
        {
            (Tile current, int distance) = moveQueue[i];
            if (distance >= range) continue;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector2Int position = current.gridPosition;
                    Tile next = GetTileAt(position + new Vector2Int(dx, dy));
                    if (next == null || moveVisited.Contains(next)) continue;
                    Unit occupant = getOccupant(next);
                    if (occupant != null && !InteractionRules.CanPassThrough(owner, occupant.owner)) continue;

                    if (blockCorners && dx != 0 && dy != 0
                        && IsEnemyAt(position + new Vector2Int(dx, 0), owner, getOccupant, isAtWar)
                        && IsEnemyAt(position + new Vector2Int(0, dy), owner, getOccupant, isAtWar))
                        continue;

                    moveVisited.Add(next);
                    moveQueue.Add((next, distance + 1));
                    if (occupant == null) results.Add(next);
                }
            }
        }
    }

    private bool IsEnemyAt(Vector2Int position, Player owner, System.Func<Tile, Unit> getOccupant,
        System.Func<Player, Player, bool> isAtWar)
    {
        Tile tile = GetTileAt(position);
        Unit occupant = tile != null ? getOccupant(tile) : null;
        return occupant != null && (isAtWar != null ? isAtWar(owner, occupant.owner) : InteractionRules.CanAttack(owner, occupant.owner));
    }

    public void ClearAllHighlights()
    {
        foreach (var tile in grid.Values)
        {
            tile.SetHighlight(false, Color.white);
        }
    }
}
