using System;
using System.Collections.Generic;
using UnityEngine;

// Match-owned cache. Search once per source city and destination owner, not per city pair.
public sealed class NeuronNetwork
{
    public static event Action IncomeChanged;
    private readonly TurnManager turns;
    private readonly Dictionary<City, HashSet<City>> neighbours = new();
    private readonly HashSet<Tile> visited = new();
    private readonly List<Tile> queue = new();
    private bool dirty = true;

    public NeuronNetwork(TurnManager turns) { this.turns = turns; }

    public void Invalidate()
    {
        dirty = true;
        Rebuild();
        foreach (City city in neighbours.Keys) city.RefreshIncomeLabel();
        IncomeChanged?.Invoke();
    }

    public int GetIncome(City city)
    {
        Rebuild();
        return neighbours.TryGetValue(city, out var cities)
            ? cities.Count * Mathf.Max(0, turns.neuronStarsPerConnection) : 0;
    }

    public bool AreConnected(City a, City b)
    {
        Rebuild();
        return neighbours.TryGetValue(a, out var cities) && cities.Contains(b);
    }

    private void Rebuild()
    {
        if (!dirty) return;
        dirty = false;
        neighbours.Clear();
        if (GridManager.Instance == null) return;
        foreach (Tile tile in GridManager.Instance.grid.Values)
            if (tile.city != null && tile.city.owner != null)
                neighbours[tile.city] = new HashSet<City>();

        foreach (City source in neighbours.Keys)
        foreach (Player destinationOwner in turns.players)
        {
            if (turns.Diplomacy.IsAtWar(source.owner, destinationOwner)) continue;
            visited.Clear();
            queue.Clear();
            visited.Add(source.centerTile);
            queue.Add(source.centerTile);
            for (int i = 0; i < queue.Count; i++)
            {
                Tile current = queue[i];
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Tile next = GridManager.Instance.GetTileAt(current.gridPosition + new Vector2Int(dx, dy));
                    if (next == null) continue;
                    if (next.city != null)
                    {
                        // Require at least one segment; all city centres stop traversal, including neutral cities.
                        if (next.city != source && current != source.centerTile && next.city.owner == destinationOwner)
                            neighbours[source].Add(next.city);
                        continue;
                    }
                    if (!visited.Add(next)) continue;
                    Building segment = next.currentBuilding;
                    if (segment == null || segment.data == null || !segment.data.isNeuron || segment.owner == null) continue;
                    if (turns.Diplomacy.IsAtWar(source.owner, segment.owner) ||
                        turns.Diplomacy.IsAtWar(destinationOwner, segment.owner)) continue;
                    queue.Add(next);
                }
            }
        }
    }
}
