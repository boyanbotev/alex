using System.Collections.Generic;

/// <summary>Applies hypothetical actions without changing live units, tiles, or cities.</summary>
public static class ActionSimulator
{
    private static readonly List<Unit> splashTargets = new();
    public static void Apply(BoardState board, CandidateAction action)
    {
        Unit unit = action.unit;
        if (action.kind == ActionKind.ReplaceGarrison)
        {
            board.WithMove(unit, board.GetTile(unit), action.moveTile);
            board.WithRecruit(action.recruitCity, action.recruit);
            return;
        }
        // Revalidate before applying any part of a queued attack, including movement.
        if (action.kind == ActionKind.Attack &&
            (action.target == null || !board.IsAlive(action.target) ||
             !board.IsAtWar(board.GetUnitOwner(unit), board.GetUnitOwner(action.target))))
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
                board.CanCapture(board.GetUnitOwner(unit), board.GetOwner(to.city)))
            {
                board.WithPendingCityCapture(to.city, unit);
            }
        }

        if (action.kind == ActionKind.SeverNeuron)
        {
            board.WithWar(board.GetUnitOwner(unit), action.neuron.owner);
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
            board.WithDamage(splash, board.GetHealth(splash) - board.GetData(unit).splashDamage);
        splashTargets.Clear();
        board.WithAttacked(unit);

        bool meleeAttack = board.GetData(unit).attackRange == 1;

        if (killed)
        {
            if (meleeAttack)
            {
                board.WithMove(unit, to, targetTile);

                if (targetTile.city != null &&
                    board.CanCapture(board.GetUnitOwner(unit), board.GetOwner(targetTile.city)))
                {
                    board.WithPendingCityCapture(targetTile.city, unit);
                }
            }
        }
        else if (Utils.IsWithinDistance(
                     targetTile.gridPosition,
                     to.gridPosition,
                     board.GetData(target).attackRange))
        {
            int newAttackerHealth =
                board.GetHealth(unit) - retaliation;

            board.WithDamage(unit, newAttackerHealth);
        }
    }

    public static (int, int) PredictDamage(Unit attacker, Unit defender, BoardState board)
    {
        return CombatMath.CalculateDamage(
            board.GetData(attacker).attackPower, board.GetHealth(attacker), board.GetData(attacker).maxHealth,
            board.GetDefensePower(defender), board.GetHealth(defender), board.GetData(defender).maxHealth
        );
    }

}
