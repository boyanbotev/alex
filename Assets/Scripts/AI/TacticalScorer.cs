using System.Collections.Generic;

/// <summary>Immediate tactical heuristics; reads the supplied board without mutating it.</summary>
public sealed class TacticalScorer
{
    private AIProfile profile;
    private IReadOnlyList<Player> players;
    private IReadOnlyList<City> cities;
    private readonly NeuronRaidScorer neuronRaids = new();
    private readonly Dictionary<(Player, Building), float> raidValues = new();
    private readonly List<Unit> splashTargets = new();

    public void BeginGeneration()
    {
        raidValues.Clear();
    }

    private float RaidValue(Unit unit, Building segment, BoardState board)
    {
        var key = (board.GetUnitOwner(unit), segment);
        if (!raidValues.TryGetValue(key, out float value))
        {
            value = neuronRaids.Evaluate(board.GetUnitOwner(unit), segment, board, cities, profile);
            raidValues[key] = value;
        }
        return value + unit.dopamineBonus * profile.neuronRaidRefundWeight;
    }

    public bool ShouldConsiderSever(Unit unit, Building segment, BoardState board) =>
        (board.IsAtWar(board.GetUnitOwner(unit), segment.owner) || profile.neuronRaidMayDeclareWar) &&
        RaidValue(unit, segment, board) > 0f;

    public float ScoreSever(Unit unit, Tile position, Building segment, BoardState board)
    {
        float value = RaidValue(unit, segment, board);
        int checkpoint = board.Checkpoint();
        try
        {
            // Include danger from the faction that this action would turn hostile.
            board.WithWar(board.GetUnitOwner(unit), segment.owner);
            return value + ScoreMove(unit, board.GetTile(unit), position, board);
        }
        finally
        {
            board.Rollback(checkpoint);
        }
    }

    public void Configure(AIProfile profile, IReadOnlyList<Player> players, IReadOnlyList<City> cities)
    {
        this.profile = profile;
        this.players = players;
        this.cities = cities;
    }

    public float ScoreAttack(Unit unit, Tile from, Unit target, BoardState board)
    {
        (int damage, int retaliation) = ActionSimulator.PredictDamage(unit, target, board);

        bool kills = board.GetHealth(target) - damage <= 0;

        float score = 0f;

        score += damage * profile.damageWeight;
        CombatMath.CollectSplashTargets(unit, target, board.GetTile(target), board, splashTargets);
        foreach (Unit splash in splashTargets)
        {
            int health = board.GetHealth(splash);
            score += System.Math.Min(health, board.GetData(unit).splashDamage) * profile.damageWeight;
            if (board.GetData(unit).splashDamage >= health) score += board.GetData(splash).cost * profile.killWeight;
        }
        splashTargets.Clear();

        if (kills)
        {
            score += board.GetData(target).cost * profile.killWeight;
        }

        bool canRetaliate = !kills && CanRetaliate(target, from, board);

        if (canRetaliate)
        {
            score -= retaliation * profile.retaliationWeight;
        }

        Tile finalPosition = kills && board.GetData(unit).attackRange == 1 ? board.GetTile(target) : from;
        score += ScorePosition(unit, finalPosition, board);

        Tile targetTile = board.GetTile(target);

        if (kills &&
            board.GetData(unit).attackRange == 1 &&
            targetTile != null &&
            targetTile.city != null &&
            board.CanCapture(board.GetUnitOwner(unit), board.GetOwner(targetTile.city)))
        {
            score += profile.cityCaptureWeight;
        }

        if (ExposesToLethalCounter(unit, from, kills ? target : null, board))
        {
            score -= board.GetData(unit).cost * profile.survivalWeight;
        }

        score += ScoreCityProgress(board.GetTile(unit), from, board.GetUnitOwner(unit), board);

        return score;
    }

    public float ScoreMove(Unit unit, Tile from, Tile to, BoardState board)
    {
        float score = 0f;

        score += ScoreCityProgress(from, to, board.GetUnitOwner(unit), board);

        if (to.city != null && board.CanCapture(board.GetUnitOwner(unit), board.GetOwner(to.city)))
        {
            score += profile.cityCaptureWeight;
        }

        score += ScorePosition(unit, to, board);

        if (ExposesToLethalCounter(unit, to, null, board))
        {
            score -= board.GetData(unit).cost * profile.survivalWeight;
        }

        return score;
    }

    private float ScoreCityProgress(Tile from, Tile to, Player owner, BoardState board)
    {
        int oldDistance = DistanceToNearestUncapturedCity(from, owner, board);
        int newDistance = DistanceToNearestUncapturedCity(to, owner, board);

        if (oldDistance == int.MaxValue || newDistance == int.MaxValue)
            return 0f;

        int improvement = oldDistance - newDistance;

        if (improvement <= 0)
            return 0f;

        return improvement * profile.cityProgressWeight;
    }

    private float ScorePosition(Unit unit, Tile tile, BoardState board)
    {
        float score = 0f;

        for (int p = 0; p < players.Count; p++)
        {
            Player enemy = players[p];
            if (!board.IsAtWar(board.GetUnitOwner(unit), enemy))
                continue;

            for (int i = 0; i < board.UnitCount(enemy); i++)
            {
                Unit enemyUnit = board.UnitAt(enemy, i);
                if (enemyUnit == null || !board.IsAlive(enemyUnit))
                    continue;

                int distance = Utils.GridDistance(
                    tile.gridPosition,
                    board.GetTile(enemyUnit).gridPosition
                );

                // being within attack range next turn is good
                if (distance <= board.GetData(unit).attackRange)
                {
                    score += profile.positionWeight;
                }

                // melee units benefit from moving toward enemies
                if (board.GetData(unit).attackRange == 1 &&
                    distance <= board.GetMoveRange(unit) + 1)
                {
                    score += profile.positionWeight * 0.5f;
                }
            }
        }

        return score;
    }

    public float ScoreCitySafety(CandidateAction action, BoardState board)
    {
        if (action.kind == ActionKind.DoNothing) return 0f;
        Player owner = board.GetUnitOwner(action.unit);
        City origin = board.GetTile(action.unit).city;
        City destination = action.moveTile.city;
        if (origin != null && board.GetOwner(origin) != owner) origin = null;
        if (destination != null && (destination == origin || board.GetOwner(destination) != owner)) destination = null;
        if (origin == null && destination == null) return 0f;
        float before = (origin == null ? 0f : CityDefense.Risk(origin, board)) +
            (destination == null ? 0f : CityDefense.Risk(destination, board));
        int checkpoint = board.Checkpoint();
        try
        {
            ActionSimulator.Apply(board, action);
            float after = (origin == null ? 0f : CityDefense.Risk(origin, board)) +
                (destination == null ? 0f : CityDefense.Risk(destination, board));
            return (before - after) * profile.cityCaptureWeight;
        }
        finally { board.Rollback(checkpoint); }
    }

    private bool ExposesToLethalCounter(Unit unit, Tile destination, Unit justKilled, BoardState board)
    {
        for (int p = 0; p < players.Count; p++)
        {
            Player enemy = players[p];
            if (!board.IsAtWar(board.GetUnitOwner(unit), enemy))
                continue;

            for (int i = 0; i < board.UnitCount(enemy); i++)
            {
                Unit enemyUnit = board.UnitAt(enemy, i);
                if (enemyUnit == null ||
                    !board.IsAlive(enemyUnit) ||
                    enemyUnit == justKilled)
                {
                    continue;
                }

                if (!CityDefense.CanAttackNextTurn(enemyUnit, destination, board))
                    continue;

                (int damage, int _) = ActionSimulator.PredictDamage(enemyUnit, unit, board);

                if (damage >= board.GetHealth(unit))
                    return true;
            }
        }

        return false;
    }

    private bool CanRetaliate(Unit defender, Tile attackerPosition, BoardState board)
    {
        int distance = Utils.GridDistance(
            board.GetTile(defender).gridPosition,
            attackerPosition.gridPosition
        );

        return distance <= board.GetData(defender).attackRange;
    }

    private int DistanceToNearestUncapturedCity(Tile from, Player owner, BoardState board)
    {
        int bestDistance = int.MaxValue;

        for (int c = 0; c < cities.Count; c++)
        {
            City city = cities[c];
            if (city == null || !board.CanCapture(owner, board.GetOwner(city)))
                continue;

            int distance = Utils.GridDistance(
                from.gridPosition,
                city.centerTile.gridPosition
            );

            if (distance < bestDistance)
                bestDistance = distance;
        }

        return bestDistance;
    }

}
