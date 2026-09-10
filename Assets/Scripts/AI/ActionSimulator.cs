/// <summary>Applies hypothetical actions without changing live units, tiles, or cities.</summary>
public static class ActionSimulator
{
    public static void Apply(BoardState board, CandidateAction action)
    {
        Unit unit = action.unit;

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
                board.GetOwner(to.city) != unit.owner)
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
                    board.GetOwner(targetTile.city) != unit.owner)
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
