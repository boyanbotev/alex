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

        var offsets = new int[level.factions.Length];
        int neutralOffset = 0;
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = neutralOffset;
            neutralOffset += level.factions[i].startingCityNames.Length;
        }

        var available = new List<Tile>(land.Count);
        var positions = new Tile[level.CityCount];
        int bestCount = 0;
        // Retry greedy placement with a different first capital / neutral ordering.
        for (int attempt = 0; attempt < 32; attempt++)
        {
            available.Clear();
            available.AddRange(land);
            Array.Clear(positions, 0, positions.Length);
            for (int i = 0; i < available.Count; i++)
            {
                if (budget.ShouldYield()) { yield return null; budget.ShouldYield(); }
                int j = random.Next(i, available.Count);
                (available[i], available[j]) = (available[j], available[i]);
            }

            int placed = 0;
            // Reserve every capital first, then cluster extra cities, then place neutrals.
            for (int step = 0; step < positions.Length && available.Count > 0; step++)
            {
                int slot;
                Tile anchor = null;
                bool capital = step < offsets.Length;
                if (capital) slot = offsets[step];
                else
                {
                    slot = 0;
                    while (positions[slot] != null) slot++;
                    if (slot < neutralOffset)
                        for (int f = offsets.Length - 1; f >= 0; f--)
                            if (slot >= offsets[f]) { anchor = positions[offsets[f]]; break; }
                }

                int bestIndex = 0;
                float bestScore = float.NegativeInfinity;
                for (int i = 0; i < available.Count; i++)
                {
                    if (budget.ShouldYield()) { yield return null; budget.ShouldYield(); }
                    float score = 0;
                    Vector2Int p = available[i].gridPosition;
                    if (capital && step > 0)
                    {
                        score = float.PositiveInfinity;
                        for (int f = 0; f < step; f++)
                            score = Mathf.Min(score, (p - positions[offsets[f]].gridPosition).sqrMagnitude);
                    }
                    else if (anchor != null) score = -(p - anchor.gridPosition).sqrMagnitude;
                    else break;
                    if (score > bestScore) { bestScore = score; bestIndex = i; }
                }

                Tile selected = available[bestIndex];
                positions[slot] = selected;
                placed++;
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
            if (placed == positions.Length) { Positions = positions; yield break; }
            bestCount = Math.Max(bestCount, placed);
        }
        throw new InvalidOperationException($"Level '{level.name}': could only place {bestCount}/{level.CityCount} cities. " +
            "Increase grid size, reduce city spacing/margin, or change the terrain seed.");
    }
}
