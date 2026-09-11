/// <summary>Applies hypothetical actions without changing live units, tiles, or cities.</summary>
public static class ActionSimulator
{
    public static void Apply(BoardState board, CandidateAction action)
    {
        Unit unit = action.unit;
        // Revalidate before applying any part of a queued attack, including movement.
        if (action.kind == ActionKind.Attack &&
            (action.target == null || !board.IsAlive(action.target) ||
             !InteractionRules.CanAttack(unit.owner, action.target.owner)))
            return;

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
                InteractionRules.CanCapture(unit.owner, board.GetOwner(to.city)))
            {
                board.WithPendingCityCapture(to.city, unit);
            }
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

        board.WithDamage(target, newTargetHealth);
        board.WithAttacked(unit);

        bool meleeAttack = unit.data.attackRange == 1;

        if (killed)
        {
            if (meleeAttack)
            {
                board.WithMove(unit, to, targetTile);

                if (targetTile.city != null &&
                    InteractionRules.CanCapture(unit.owner, board.GetOwner(targetTile.city)))
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
            defender.data.defensePower, board.GetHealth(defender), defender.data.maxHealth
        );
    }

}
