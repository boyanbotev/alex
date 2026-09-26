using System.Collections.Generic;

// Only the best coordinated action enters root lookahead. Rollouts never recruit.
public sealed class GarrisonReplacementPlanner
{
    private readonly CandidateGenerator fallback = new();

    public bool TryPlan(Player player, AIProfile profile, TacticalScorer scorer, out CandidateAction best,
        IReadOnlyList<CandidateAction> candidates = null)
    {
        best = default;
        float bestScore = float.NegativeInfinity;
        BoardState board = BoardState.Live;
        if (player.faction?.availableUnits == null) return false;
        if (candidates == null)
        {
            fallback.Configure(GridManager.Instance, scorer);
            fallback.Generate(player, board);
            candidates = fallback.Candidates;
        }
        foreach (CandidateAction outgoing in candidates)
        {
            if (outgoing.kind != ActionKind.MoveOnly && outgoing.kind != ActionKind.Attack) continue;
            Unit defender = outgoing.unit;
            City city = board.GetTile(defender).city;
            if (city == null || board.GetOwner(city) != player || defender.hasAttacked) continue;
            int health = board.GetHealth(defender);
            if (!defender.hasMoved && !defender.hasCaptured)
                health = System.Math.Min(defender.data.maxHealth, health + 2 + city.PerkAmount(CityPerkKind.Healing));
            float current = (float)CityDefense.RemainingHealth(defender.data, health, city.centerTile, player, board) / defender.data.maxHealth;
            float ordinarySafety = scorer.ScoreCitySafety(outgoing, board);
            foreach (FactionUnit recruit in player.faction.availableUnits)
            {
                if (recruit?.unitData == null || !city.CanSpawnUnit(recruit, recruit.unitData.cost, defender)) continue;
                int checkpoint = board.Checkpoint();
                bool eligible;
                try
                {
                    ActionSimulator.Apply(board, outgoing);
                    eligible = board.IsAlive(defender) && board.GetOccupant(city.centerTile) == null;
                    if (eligible)
                    {
                        int remaining = CityDefense.RemainingHealth(recruit.unitData, recruit.unitData.maxHealth,
                            city.centerTile, player, board);
                        eligible = remaining > 0 && (float)remaining / recruit.unitData.maxHealth >= current;
                    }
                }
                finally { board.Rollback(checkpoint); }
                if (!eligible) continue;
                CandidateAction combined = outgoing;
                combined.outgoingKind = outgoing.kind;
                combined.kind = ActionKind.ReplaceGarrison;
                combined.recruitCity = city;
                combined.recruit = recruit;
                combined.score = outgoing.score - ordinarySafety + scorer.ScoreCitySafety(combined, board) - recruit.unitData.cost;
                if (combined.score <= bestScore) continue;
                bestScore = combined.score;
                best = combined;
            }
        }
        return best.unit != null;
    }
}
