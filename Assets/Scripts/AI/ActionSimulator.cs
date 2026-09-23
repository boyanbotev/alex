using System.Collections.Generic;

/// <summary>Applies hypothetical actions without changing live units, tiles, or cities.</summary>
public static class ActionSimulator
{
    private static readonly List<Unit> splashTargets = new();
    public static void Apply(BoardState board, CandidateAction action)
    {
        Unit unit = action.unit;
        // Revalidate before applying any part of a queued attack, including movement.
        if (action.kind == ActionKind.Attack &&
            (action.target == null || !board.IsAlive(action.target) ||
             !board.IsAtWar(unit.owner, action.target.owner)))
            return;
        if (action.kind == ActionKind.SeverNeuron && !board.CanSeverNeuron(unit, action.moveTile, action.neuron)) return;

        Tile from = board.GetTile(unit);
        Tile to = action.moveTile;

        if (action.kind == ActionKind.DoNothing)
        {
            board.WithDeactivated(unit);
            return;
        }

        if (to != from)
        {
            board.WithMove(unit, from, to);

            if (to.city != null &&
                board.CanCapture(unit.owner, board.GetOwner(to.city)))
            {
                board.WithPendingCityCapture(to.city, unit);
            }
        }

        if (action.kind == ActionKind.SeverNeuron)
        {
            board.WithWar(unit.owner, action.neuron.owner);
            board.WithRemovedNeuron(action.neuron);
            board.WithAttacked(unit);
            board.WithDeactivated(unit);
            return;
        }

        if (action.kind != ActionKind.Attack ||
            action.target == null ||
            !board.IsAlive(action.target))
        {
            return;
        }

        Unit target = action.target;
        Tile targetTile = board.GetTile(target);

        (int damage, int retaliation) =
            PredictDamage(unit, target, board);

        int newTargetHealth =
            board.GetHealth(target) - damage;

        bool killed = newTargetHealth <= 0;

        CombatMath.CollectSplashTargets(unit, target, targetTile, board, splashTargets);
        board.WithDamage(target, newTargetHealth);
        foreach (Unit splash in splashTargets)
            board.WithDamage(splash, board.GetHealth(splash) - unit.data.splashDamage);
        splashTargets.Clear();
        board.WithAttacked(unit);

        bool meleeAttack = unit.data.attackRange == 1;

        if (killed)
        {
            if (meleeAttack)
            {
                board.WithMove(unit, to, targetTile);

                if (targetTile.city != null &&
                    board.CanCapture(unit.owner, board.GetOwner(targetTile.city)))
                {
                    board.WithPendingCityCapture(targetTile.city, unit);
                }
            }
        }
        else if (Utils.IsWithinDistance(
                     targetTile.gridPosition,
                     to.gridPosition,
                     target.data.attackRange))
        {
            int newAttackerHealth =
                board.GetHealth(unit) - retaliation;

            board.WithDamage(unit, newAttackerHealth);
        }
    }

    public static (int, int) PredictDamage(Unit attacker, Unit defender, BoardState board)
    {
        return CombatMath.CalculateDamage(
            attacker.data.attackPower, board.GetHealth(attacker), attacker.data.maxHealth,
            board.GetDefensePower(defender), board.GetHealth(defender), defender.data.maxHealth
        );
    }

}
