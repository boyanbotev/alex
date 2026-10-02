using System;
using System.Collections.Generic;
using UnityEngine;

// Match-owned cache. Search once per source city; filter endpoints by diplomacy.
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
        turns.Bonds.Refresh();
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

    // Returns intermediate tiles, including city centres used for transit.
    public bool TryGetRoute(City a, City b, List<Tile> route)
    {
        route.Clear();
        if (!AreConnected(a, b)) return false;
        var parents = new Dictionary<Tile, Tile>();
        queue.Clear();
        queue.Add(a.centerTile);
        parents[a.centerTile] = null;
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
                    if (current.city != null) continue;
                    if (next.city == b)
                    {
                        for (Tile tile = current; tile != a.centerTile; tile = parents[tile]) route.Add(tile);
                        route.Reverse();
                        return true;
                    }
                }
                if (parents.ContainsKey(next) || (next.city == null && (next.currentBuilding == null ||
                    !next.currentBuilding.IsPlacedNeuron || next.currentBuilding.owner == null))) continue;
                parents[next] = current;
                queue.Add(next);
            }
        }
        return false;
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
        {
            HashSet<City> connected = neighbours[source];
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
                        // Cities join neuron branches, but adjacent cities need a segment between them.
                        if (current.city != null) continue;
                        Player destinationOwner = next.city.owner;
                        if (next.city != source && destinationOwner != null &&
                            turns.players.Contains(destinationOwner) && !turns.Diplomacy.IsAtWar(source.owner, destinationOwner))
                            connected.Add(next.city);
                    }
                    if (!visited.Add(next)) continue;
                    Building segment = next.currentBuilding;
                    if (next.city == null && (segment == null || !segment.IsPlacedNeuron || segment.owner == null)) continue;
                    queue.Add(next);
                }
            }
        }
    }
}
