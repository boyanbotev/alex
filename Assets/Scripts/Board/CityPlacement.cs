using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plans all positions before instantiation, so failure never creates a partial roster.
public sealed class CityPlacement
{
    // Faction cities in Level order, followed by neutral cities.
    public Tile[] Positions { get; private set; }

    public IEnumerator Generate(Level level, IEnumerable<Tile> tiles, System.Random random, GenerationBudget budget)
    {
        var land = new List<Tile>();
        foreach (Tile tile in tiles)
        {
            if (budget.ShouldYield()) { yield return null; budget.ShouldYield(); }
            Vector2Int p = tile.gridPosition;
            if ((tile.terrainType == TerrainType.Field || tile.terrainType == TerrainType.Forest) &&
                p.x >= level.minMargin && p.x < level.width - level.minMargin &&
                p.y >= level.minMargin && p.y < level.height - level.minMargin)
                land.Add(tile);
        }

        var available = new List<Tile>(land.Count);
        var sites = new List<Tile>(level.CityCount);
        int bestCount = 0;
        // Scatter sites independently of ownership, retrying if spacing prevents a full roster.
        for (int attempt = 0; attempt < 32; attempt++)
        {
            available.Clear();
            available.AddRange(land);
            sites.Clear();
            while (sites.Count < level.CityCount && available.Count > 0)
            {
                Tile selected = available[random.Next(available.Count)];
                sites.Add(selected);
                for (int i = available.Count - 1; i >= 0; i--)
                {
                    if (budget.ShouldYield()) { yield return null; budget.ShouldYield(); }
                    Vector2Int delta = available[i].gridPosition - selected.gridPosition;
                    if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) < level.minCityDistance)
                    {
                        available[i] = available[available.Count - 1];
                        available.RemoveAt(available.Count - 1);
                    }
                }
            }
            if (sites.Count == level.CityCount)
            {
                yield return AssignFactions(level, sites, budget);
                yield break;
            }
            bestCount = Math.Max(bestCount, sites.Count);
        }
        throw new InvalidOperationException($"Level '{level.name}': could only place {bestCount}/{level.CityCount} cities. " +
            "Increase grid size, reduce city spacing/margin, or change the terrain seed.");
    }

    private IEnumerator AssignFactions(Level level, List<Tile> sites, GenerationBudget budget)
    {
        var positions = new Tile[sites.Count];
        var offsets = new int[level.factions.Length];
        int neutralOffset = 0;
        for (int f = 0; f < offsets.Length; f++)
        {
            offsets[f] = neutralOffset;
            neutralOffset += level.factions[f].startingCities.Length;
        }

        // Assign each local group before choosing the next separated capital.
        int faction = 0;
        for (int slot = 0; slot < positions.Length; slot++)
        {
            while (faction + 1 < offsets.Length && slot >= offsets[faction + 1]) faction++;
            bool owned = slot < neutralOffset;
            bool capital = owned && slot == offsets[faction];
            Tile anchor = owned && !capital ? positions[offsets[faction]] : null;

            int bestIndex = 0;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < sites.Count; i++)
            {
                if (budget.ShouldYield()) { yield return null; budget.ShouldYield(); }
                float score;
                Vector2Int p = sites[i].gridPosition;
                if (capital && faction > 0)
                {
                    score = float.PositiveInfinity;
                    for (int f = 0; f < faction; f++)
                        score = Mathf.Min(score, (p - positions[offsets[f]].gridPosition).sqrMagnitude);
                }
                else if (anchor != null) score = -(p - anchor.gridPosition).sqrMagnitude;
                else break;
                if (score > bestScore) { bestScore = score; bestIndex = i; }
            }
            positions[slot] = sites[bestIndex];
            sites[bestIndex] = sites[sites.Count - 1];
            sites.RemoveAt(sites.Count - 1);
        }
        Positions = positions;
    }
}
