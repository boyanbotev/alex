using System.Collections.Generic;
using UnityEngine;

// Read-only counterfactual connectivity over explored tiles. Called once per
// player/segment per candidate generation, even when several units can reach it.
public sealed class NeuronRaidScorer
{
    private readonly List<Tile> queue = new();
    private readonly HashSet<Tile> visited = new();

    public float Evaluate(Player actor, Building segment, BoardState board, IReadOnlyList<City> cities, AIProfile profile)
    {
        bool startsWar = !board.IsAtWar(actor, segment.owner);
        float score = segment.DemolitionRefund * profile.neuronRaidRefundWeight;
        if (startsWar) score -= profile.neuronRaidWarPenalty;
        for (int i = 0; i < cities.Count; i++)
        {
            City a = cities[i];
            if (!Known(a, actor, board)) continue;
            for (int j = i + 1; j < cities.Count; j++)
            {
                City b = cities[j];
                if (!Known(b, actor, board)) continue;
                float weight = IncomeWeight(a, actor, segment.owner, board, profile) +
                    IncomeWeight(b, actor, segment.owner, board, profile);
                if (weight == 0f || !Connected(a, b, actor, null, null, board)) continue;
                if (!Connected(a, b, actor, segment, startsWar ? segment.owner : null, board))
                    score += weight * Mathf.Max(0, TurnManager.Instance.neuronStarsPerConnection);
            }
        }
        return score;
    }

    private static bool Known(City city, Player actor, BoardState board) => city != null &&
        board.GetOwner(city) != null && actor.visibleTiles != null && actor.visibleTiles.IsVisible(city.centerTile);

    private static float IncomeWeight(City city, Player actor, Player victim, BoardState board, AIProfile profile)
    {
        if (city.HasPendingCapture || board.HasPendingCityCapture(city)) return 0f;
        Player owner = board.GetOwner(city);
        if (owner == actor) return -profile.neuronRaidFriendlyLossWeight;
        if (owner == victim || board.IsAtWar(actor, owner)) return profile.neuronRaidIncomeWeight;
        return TurnManager.Instance.Diplomacy.GetRelation(actor, owner) == DiplomaticRelation.Allied
            ? -profile.neuronRaidFriendlyLossWeight : 0f;
    }

    private bool Connected(City a, City b, Player actor, Building removed, Player newEnemy, BoardState board)
    {
        Player ownerA = board.GetOwner(a), ownerB = board.GetOwner(b);
        if (AtWar(ownerA, ownerB, actor, newEnemy, board)) return false;
        queue.Clear(); visited.Clear();
        queue.Add(a.centerTile); visited.Add(a.centerTile);
        for (int i = 0; i < queue.Count; i++)
        {
            Tile current = queue[i];
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Tile next = GridManager.Instance.GetTileAt(current.gridPosition + new Vector2Int(dx, dy));
                if (next == null || !actor.visibleTiles.IsVisible(next)) continue;
                if (next.city != null)
                {
                    if (next.city == b && current != a.centerTile) return true;
                    continue;
                }
                if (!visited.Add(next)) continue;
                Building road = board.GetBuilding(next);
                if (road == null || road == removed || !road.IsPlacedNeuron || road.owner == null ||
                    AtWar(ownerA, road.owner, actor, newEnemy, board) ||
                    AtWar(ownerB, road.owner, actor, newEnemy, board)) continue;
                queue.Add(next);
            }
        }
        return false;
    }

    private static bool AtWar(Player a, Player b, Player actor, Player newEnemy, BoardState board) =>
        board.IsAtWar(a, b) || (newEnemy != null &&
            ((a == actor && b == newEnemy) || (b == actor && a == newEnemy)));
}
