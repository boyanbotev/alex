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
    private readonly Dictionary<City, bool> threatenedCities = new();

    public void BeginGeneration()
    {
        raidValues.Clear();
        threatenedCities.Clear();
    }

    private float RaidValue(Unit unit, Building segment, BoardState board)
    {
        var key = (unit.owner, segment);
        if (!raidValues.TryGetValue(key, out float value))
        {
            value = neuronRaids.Evaluate(unit.owner, segment, board, cities, profile);
            raidValues[key] = value;
        }
        return value + unit.dopamineBonus * profile.neuronRaidRefundWeight;
    }

    public bool ShouldConsiderSever(Unit unit, Building segment, BoardState board) =>
        (board.IsAtWar(unit.owner, segment.owner) || profile.neuronRaidMayDeclareWar) &&
        RaidValue(unit, segment, board) > 0f;

    public float ScoreSever(Unit unit, Tile position, Building segment, BoardState board)
    {
        float value = RaidValue(unit, segment, board);
        int checkpoint = board.Checkpoint();
        bool changesWar = !board.IsAtWar(unit.owner, segment.owner);
        try
        {
            // Include danger from the faction that this action would turn hostile.
            board.WithWar(unit.owner, segment.owner);
            if (changesWar) threatenedCities.Clear();
            return value + ScoreMove(unit, board.GetTile(unit), position, board);
        }
        finally
        {
            board.Rollback(checkpoint);
            if (changesWar) threatenedCities.Clear();
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
            score += System.Math.Min(health, unit.data.splashDamage) * profile.damageWeight;
            if (unit.data.splashDamage >= health) score += splash.data.cost * profile.killWeight;
        }
        splashTargets.Clear();

        if (kills)
        {
            score += target.data.cost * profile.killWeight;
        }

        bool canRetaliate = !kills && CanRetaliate(target, from, board);

        if (canRetaliate)
        {
            score -= retaliation * profile.retaliationWeight;
        }

        Tile finalPosition = kills && unit.data.attackRange == 1 ? board.GetTile(target) : from;
        score += ScorePosition(unit, finalPosition, board);

        Tile targetTile = board.GetTile(target);

        if (kills &&
            unit.data.attackRange == 1 &&
            targetTile != null &&
            targetTile.city != null &&
            board.CanCapture(unit.owner, board.GetOwner(targetTile.city)))
        {
            score += profile.cityCaptureWeight;
        }

        if (ExposesToLethalCounter(unit, from, kills ? target : null, board))
        {
            score -= unit.data.cost * profile.survivalWeight;
        }

        score += ScoreCityProgress(board.GetTile(unit), from, unit.owner, board);

        return score;
    }

    public float ScoreMove(Unit unit, Tile from, Tile to, BoardState board)
    {
        float score = 0f;

        score += ScoreCityProgress(from, to, unit.owner, board);

        if (to.city != null && board.CanCapture(unit.owner, board.GetOwner(to.city)))
        {
            score += profile.cityCaptureWeight;
        }

        score += ScorePosition(unit, to, board);

        if (ExposesToLethalCounter(unit, to, null, board))
        {
            score -= unit.data.cost * profile.survivalWeight;
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

        // Give urgent garrison moves/staying put a chance to survive immediate
        // pruning, even with one candidate per unit. Lookahead still judges combat.
        if (tile.city != null && board.GetOwner(tile.city) == unit.owner &&
            IsCityThreatened(tile.city, board))
            score += profile.cityCaptureWeight;

        for (int p = 0; p < players.Count; p++)
        {
            Player enemy = players[p];
            if (!board.IsAtWar(unit.owner, enemy))
                continue;

            foreach (Unit enemyUnit in enemy.units)
            {
                if (enemyUnit == null || !board.IsAlive(enemyUnit))
                    continue;

                int distance = Utils.GridDistance(
                    tile.gridPosition,
                    board.GetTile(enemyUnit).gridPosition
                );

                // being within attack range next turn is good
                if (distance <= unit.data.attackRange)
                {
                    score += profile.positionWeight;
                }

                // melee units benefit from moving toward enemies
                if (unit.data.attackRange == 1 &&
                    distance <= board.GetMoveRange(unit) + 1)
                {
                    score += profile.positionWeight * 0.5f;
                }
            }
        }

        return score;
    }

    private bool IsCityThreatened(City city, BoardState board)
    {
        if (threatenedCities.TryGetValue(city, out bool threatened)) return threatened;
        Player owner = board.GetOwner(city);
        for (int p = 0; p < players.Count; p++)
        {
            Player enemy = players[p];
            if (!board.IsAtWar(owner, enemy)) continue;
            foreach (Unit unit in enemy.units)
            {
                if (unit == null || !board.IsAlive(unit)) continue;
                // Deliberately conservative range estimate; spent actions refresh
                // next turn and must not hide an approaching attacker.
                int distance = Utils.GridDistance(board.GetTile(unit).gridPosition, city.centerTile.gridPosition);
                if (distance <= board.GetMoveRange(unit) + unit.data.attackRange)
                {
                    threatenedCities[city] = true;
                    return true;
                }
            }
        }
        threatenedCities[city] = false;
        return false;
    }

    private bool ExposesToLethalCounter(Unit unit, Tile destination, Unit justKilled, BoardState board)
    {
        for (int p = 0; p < players.Count; p++)
        {
            Player enemy = players[p];
            if (!board.IsAtWar(unit.owner, enemy))
                continue;

            foreach (Unit enemyUnit in enemy.units)
            {
                if (enemyUnit == null ||
                    !board.IsAlive(enemyUnit) ||
                    enemyUnit == justKilled)
                {
                    continue;
                }

                if (!CanReachAndAttack(enemyUnit, destination, board))
                    continue;

                (int damage, int _) = ActionSimulator.PredictDamage(enemyUnit, unit, board);

                if (damage >= board.GetHealth(unit))
                    return true;
            }
        }

        return false;
    }

    private bool CanReachAndAttack(Unit enemy, Tile targetTile, BoardState board)
    {
        int distance = Utils.GridDistance(
            board.GetTile(enemy).gridPosition,
            targetTile.gridPosition
        );

        return distance <= board.GetMoveRange(enemy) + enemy.data.attackRange;
    }

    private bool CanRetaliate(Unit defender, Tile attackerPosition, BoardState board)
    {
        int distance = Utils.GridDistance(
            board.GetTile(defender).gridPosition,
            attackerPosition.gridPosition
        );

        return distance <= defender.data.attackRange;
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
