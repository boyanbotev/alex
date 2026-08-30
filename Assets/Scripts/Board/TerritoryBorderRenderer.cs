using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TerritoryBorderManager : MonoBehaviour
{
    public static TerritoryBorderManager Instance;

    [Header("Visuals")]
    [Tooltip("Unlit/Transparent material with an alpha-cutout dash texture, Wrap Mode = Repeat")]
    [SerializeField] private Material dashedBorderMaterial;
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private float yOffset = 0.05f;

    // Keyed by Player for owned territory, or by City for a neutral village's
    // own outline. Grouping key type is intentionally loose (object) so both
    // share the same clear/rebuild machinery.
    private readonly Dictionary<object, List<LineRenderer>> linesByGroup = new Dictionary<object, List<LineRenderer>>();

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>Rebuilds every given player's territory border plus every neutral city's own border.</summary>
    public void RebuildAllBorders(IEnumerable<Player> players)
    {
        foreach (Player player in players)
        {
            RebuildBorder(player);
        }
    }

    /// <summary>Rebuilds the single outline around ALL of a player's cities' combined territory.</summary>
    public void RebuildBorder(Player player)
    {
        if (player == null) return;

        List<Tile> territoryTiles = GridManager.Instance.grid.Values
            .Where(t => t.territoryCity != null && t.territoryCity.owner == player)
            .ToList();

        RebuildGroup(player, territoryTiles, player.factionColor);
    }

    /// <summary>Removes a player's territory outline entirely (e.g. player eliminated).</summary>
    public void RemovePlayer(Player player) => RemoveGroup(player);

    private void RebuildGroup(object groupKey, List<Tile> territoryTiles, Color color)
    {
        ClearLines(groupKey);
        if (territoryTiles.Count == 0) return;

        HashSet<Tile> tileSet = new HashSet<Tile>(territoryTiles);
        float tileSize = GetTileSize(territoryTiles[0]);
        List<(Vector3 a, Vector3 b)> edges = GetBorderEdges(tileSet, tileSize);
        List<List<Vector3>> loops = ChainEdgesIntoLoops(edges);

        foreach (var loop in loops)
        {
            DrawLoop(groupKey, loop, color);
        }
    }

    private float GetTileSize(Tile anyTile)
    {
        foreach (var dir in Directions)
        {
            if (GridManager.Instance.grid.TryGetValue(anyTile.gridPosition + dir, out Tile neighbor))
            {
                return Vector3.Distance(anyTile.transform.position, neighbor.transform.position);
            }
        }
        return 1f; // fallback for a fully isolated single-tile map
    }

    private List<(Vector3, Vector3)> GetBorderEdges(HashSet<Tile> territoryTiles, float tileSize)
    {
        var edges = new List<(Vector3, Vector3)>();
        float half = tileSize * 0.5f;

        foreach (Tile tile in territoryTiles)
        {
            Vector3 center = tile.transform.position;

            foreach (var dir in Directions)
            {
                GridManager.Instance.grid.TryGetValue(tile.gridPosition + dir, out Tile neighbor);
                bool isBorder = neighbor == null || !territoryTiles.Contains(neighbor);
                if (!isBorder) continue;

                Vector3 dirWorld = new Vector3(dir.x, 0f, dir.y);
                Vector3 perpWorld = new Vector3(-dirWorld.z, 0f, dirWorld.x);

                Vector3 edgeCenter = center + dirWorld * half;
                Vector3 p1 = edgeCenter - perpWorld * half;
                Vector3 p2 = edgeCenter + perpWorld * half;

                edges.Add((p1, p2));
            }
        }
        return edges;
    }

    // Stitches loose edge segments into ordered closed loops by matching
    // endpoints that sit at (nearly) the same world position.
    private List<List<Vector3>> ChainEdgesIntoLoops(List<(Vector3 a, Vector3 b)> edges)
    {
        Vector3Int Key(Vector3 v) => Vector3Int.RoundToInt(v * 100f); // ~1cm snap tolerance

        var remaining = new List<(Vector3 a, Vector3 b)>(edges);
        var loops = new List<List<Vector3>>();

        while (remaining.Count > 0)
        {
            var loop = new List<Vector3>();
            var current = remaining[0];
            remaining.RemoveAt(0);
            loop.Add(current.a);
            loop.Add(current.b);
            Vector3 head = current.b;

            bool extended = true;
            while (extended)
            {
                extended = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    var (a, b) = remaining[i];
                    if (Key(a) == Key(head))
                    {
                        loop.Add(b);
                        head = b;
                        remaining.RemoveAt(i);
                        extended = true;
                        break;
                    }
                    if (Key(b) == Key(head))
                    {
                        loop.Add(a);
                        head = a;
                        remaining.RemoveAt(i);
                        extended = true;
                        break;
                    }
                }
            }
            loops.Add(loop);
        }
        return loops;
    }

    private void DrawLoop(object groupKey, List<Vector3> points, Color color)
    {
        GameObject lineObj = new GameObject("BorderLine");
        lineObj.transform.SetParent(transform, false);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = dashedBorderMaterial;
        lr.widthMultiplier = lineWidth;
        lr.loop = true;
        lr.useWorldSpace = true;
        lr.textureMode = LineTextureMode.Tile;
        lr.numCornerVertices = 0; // keep corners sharp/blocky, matching the reference look
        lr.numCapVertices = 0;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingLayerName = "bg";

        Vector3[] raised = points.Select(p => p + Vector3.up * yOffset).ToArray();
        lr.positionCount = raised.Length;
        lr.SetPositions(raised);

        if (!linesByGroup.TryGetValue(groupKey, out var list))
        {
            list = new List<LineRenderer>();
            linesByGroup[groupKey] = list;
        }
        list.Add(lr);
    }

    private void ClearLines(object groupKey)
    {
        if (!linesByGroup.TryGetValue(groupKey, out var lines)) return;

        foreach (var lr in lines)
        {
            if (lr != null) Destroy(lr.gameObject);
        }
        lines.Clear();
    }

    private void RemoveGroup(object groupKey)
    {
        ClearLines(groupKey);
        linesByGroup.Remove(groupKey);
    }
}