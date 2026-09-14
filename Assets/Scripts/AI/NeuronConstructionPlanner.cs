using System.Collections.Generic;
using UnityEngine;

// Dijkstra searches count missing segments: existing roads cost zero, new roads one.
// Buffers are reused; searches are bounded by the profile's payback limit.
public sealed class NeuronConstructionPlanner
{
    private readonly Dictionary<Tile, int> distance = new();
    private readonly Dictionary<Tile, Tile> firstBuild = new();
    private readonly HashSet<City> targets = new();
    private readonly HashSet<Player> destinationOwners = new();
    private readonly SortedSet<(int cost, int order, Tile tile)> frontier = new(
        Comparer<(int cost, int order, Tile tile)>.Create((a, b) =>
        {
            int cost = a.cost.CompareTo(b.cost);
            return cost != 0 ? cost : a.order.CompareTo(b.order);
        }));
    private int sequence;

    public void GenerateCandidates(Player player, AIProfile profile, List<EconomyCandidateAction> results)
    {
        if (player.visibleTiles == null || profile.neuronConstructionWeight <= 0f ||
            profile.neuronMaxPaybackTurns <= 0f || TurnManager.Instance.neuronStarsPerConnection <= 0) return;
        foreach (BuildingData data in player.faction.availableBuildings)
        {
            if (data == null || !data.isNeuron || !player.techState.CanBuild(data) || data.cost < 0 ||
                data.cost > player.stars || data.buildingPrefab == null ||
                data.buildingPrefab.GetComponent<Building>() == null) continue;
            foreach (City source in player.cities)
            {
                if (source.owner != player || source.HasPendingCapture || !player.visibleTiles.IsVisible(source.centerTile)) continue;
                targets.Clear();
                destinationOwners.Clear();
                foreach (City city in WorldPopulationManager.Instance.allCities)
                {
                    if (city == source || city.owner == null || city.HasPendingCapture ||
                        !player.visibleTiles.IsVisible(city.centerTile) ||
                        TurnManager.Instance.Diplomacy.IsAtWar(player, city.owner) ||
                        TurnManager.Instance.Neurons.AreConnected(source, city)) continue;
                    targets.Add(city);
                    destinationOwners.Add(city.owner);
                }
                foreach (Player destinationOwner in destinationOwners)
                    Search(player, source, destinationOwner, data, profile, results);
            }
        }
    }

    private void Search(Player player, City source, Player destinationOwner, BuildingData data,
        AIProfile profile, List<EconomyCandidateAction> results)
    {
        int income = TurnManager.Instance.neuronStarsPerConnection * (destinationOwner == player ? 2 : 1);
        float budget = income * profile.neuronMaxPaybackTurns;
        distance.Clear();
        firstBuild.Clear();
        frontier.Clear();
        sequence = 0;
        distance[source.centerTile] = 0;
        firstBuild[source.centerTile] = null;
        frontier.Add((0, sequence++, source.centerTile));
        int remainingTargets = 0;
        foreach (City target in targets)
            if (target.owner == destinationOwner) remainingTargets++;
        while (frontier.Count > 0 && remainingTargets > 0)
        {
            var entry = frontier.Min;
            frontier.Remove(entry);
            Tile current = entry.tile;
            if (distance[current] != entry.cost) continue;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Tile next = GridManager.Instance.GetTileAt(current.gridPosition + new Vector2Int(dx, dy));
                if (next == null || !player.visibleTiles.IsVisible(next)) continue;
                if (next.city != null)
                {
                    if (next.city.owner == destinationOwner && targets.Contains(next.city) && firstBuild[current] != null)
                    {
                        // The first path reaching a city is cheapest because the frontier is cost ordered.
                        Tile build = firstBuild[current];
                        if (player.CanPlaceNeuron(data, build))
                        {
                            results.Add(new EconomyCandidateAction
                            {
                                kind = EconomyActionKind.PlaceNeuron, building = data, buildTile = build,
                                city = source, cost = data.cost,
                                score = profile.neuronConstructionWeight * income / Mathf.Max(1f, entry.cost * (float)data.cost)
                            });
                            targets.Remove(next.city);
                            remainingTargets--;
                        }
                    }
                    continue; // Never plan through another city, including unclaimed villages.
                }

                Building segment = next.currentBuilding;
                bool existing = segment != null;
                if (existing)
                {
                    if (!segment.IsPlacedNeuron || segment.owner == null) continue;
                }
                else
                {
                    if (!player.CanPlaceNeuronSite(data, next)) continue;
                    // Other players' roads permit transit but cannot anchor our next purchase.
                    // A planned empty tile will become our own segment before we extend it.
                    Building previous = current.currentBuilding;
                    if (previous != null && previous.owner != player && !player.HasNeuronBuildAnchor(next)) continue;
                }

                int cost = entry.cost + (existing ? 0 : 1);
                if (cost * (float)data.cost > budget || (distance.TryGetValue(next, out int best) && best <= cost)) continue;
                distance[next] = cost;
                firstBuild[next] = firstBuild[current] ?? (existing ? null : next);
                frontier.Add((cost, sequence++, next));
            }
        }
    }
}
