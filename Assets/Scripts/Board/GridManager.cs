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
        System.Func<Tile, Unit> getOccupant, List<Tile> results, System.Func<Player, Player, bool> isAtWar = null,
        System.Func<Unit, Player> getOwner = null)
    {
        results.Clear();
        moveQueue.Clear();
        moveVisited.Clear();
        if (start == null || range <= 0) return;
        if (start.terrainType == TerrainType.Forest) range = 1;

        Level settings = GridGenerator.Instance != null ? GridGenerator.Instance.level : null;
        bool blockCorners = settings == null || settings.blockDiagonalsBetweenEnemies;
        bool blockTerrainCorners = settings == null || settings.blockDiagonalsBetweenImpassableTiles;
        moveQueue.Add((start, 0));
        moveVisited.Add(start);

        for (int i = 0; i < moveQueue.Count; i++)
        {
            (Tile current, int distance) = moveQueue[i];
            if (distance >= range) continue;
            // Forest is a valid destination, but cannot be crossed in one move.
            // This only limits movement; attack availability is handled separately.
            if (current != start && current.terrainType == TerrainType.Forest) continue;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector2Int position = current.gridPosition;
                    Tile next = GetTileAt(position + new Vector2Int(dx, dy));
                    if (next == null || next.terrainType == TerrainType.Mountain || moveVisited.Contains(next)) continue;
                    if (blockTerrainCorners && dx != 0 && dy != 0
                        && GetTileAt(position + new Vector2Int(dx, 0))?.terrainType == TerrainType.Mountain
                        && GetTileAt(position + new Vector2Int(0, dy))?.terrainType == TerrainType.Mountain)
                        continue;
                    Unit occupant = getOccupant(next);
                    if (occupant != null && !InteractionRules.CanPassThrough(owner, getOwner != null ? getOwner(occupant) : occupant.owner)) continue;

                    if (blockCorners && dx != 0 && dy != 0
                        && IsEnemyAt(position + new Vector2Int(dx, 0), owner, getOccupant, isAtWar, getOwner)
                        && IsEnemyAt(position + new Vector2Int(0, dy), owner, getOccupant, isAtWar, getOwner))
                        continue;

                    moveVisited.Add(next);
                    moveQueue.Add((next, distance + 1));
                    if (occupant == null) results.Add(next);
                }
            }
        }
    }

    private bool IsEnemyAt(Vector2Int position, Player owner, System.Func<Tile, Unit> getOccupant,
        System.Func<Player, Player, bool> isAtWar, System.Func<Unit, Player> getOwner)
    {
        Tile tile = GetTileAt(position);
        Unit occupant = tile != null ? getOccupant(tile) : null;
        if (occupant == null) return false;
        Player occupantOwner = getOwner != null ? getOwner(occupant) : occupant.owner;
        return isAtWar != null ? isAtWar(owner, occupantOwner) : InteractionRules.CanAttack(owner, occupantOwner);
    }

    public void ClearAllHighlights()
    {
        foreach (var tile in grid.Values)
        {
            tile.SetHighlight(false, Color.white);
        }
    }
}
