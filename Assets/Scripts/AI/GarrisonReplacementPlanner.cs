using System.Collections.Generic;
using UnityEngine;

// Only the best replacement is added to root lookahead. Rollouts never spend money
// or recursively recruit, keeping the extra search and hypothetical state bounded.
public sealed class GarrisonReplacementPlanner
{
    private readonly List<Tile> retreats = new();

    public bool TryPlan(Player player, AIProfile profile, TacticalScorer scorer, out CandidateAction best)
    {
        best = default;
        float bestScore = float.NegativeInfinity;
        BoardState board = BoardState.Live;
        foreach (City city in player.cities)
        {
            Unit defender = city.centerTile.currentUnit;
            if (defender == null || defender.owner != player || !defender.isAlive || !defender.isActive ||
                defender.hasMoved || defender.currentHealth >= defender.data.maxHealth ||
                !CityDefense.IsThreatened(city.centerTile, player, board)) continue;
            FactionUnit recruit = EconomyAI.FindReplacement(city, defender, profile, out float improvement);
            if (recruit == null) continue;
            GridManager.Instance.GetReachableMoveTiles(defender.currentTile, player, board.GetMoveRange(defender),
                board.GetOccupant, retreats, board.IsAtWar, board.GetUnitOwner);
            foreach (Tile retreat in retreats)
            {
                // Do not replace a garrison by sacrificing the wounded unit or
                // tying up another city center needed for recruitment.
                if (retreat.city != null || CityDefense.RemainingHealth(defender.data, defender.currentHealth,
                    retreat, player, board) <= 0) continue;
                float score = profile.cityCaptureWeight + improvement * profile.cityCaptureWeight - recruit.unitData.cost +
                    scorer.ScoreMove(defender, defender.currentTile, retreat, board);
                if (score <= bestScore) continue;
                bestScore = score;
                best = new CandidateAction { kind = ActionKind.ReplaceGarrison, unit = defender, moveTile = retreat,
                    recruitCity = city, recruit = recruit, score = score };
            }
        }
        return best.unit != null;
    }

}
