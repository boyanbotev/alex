using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pure, state-free combat math shared between real unit combat (Unit.Attack)
/// and AI lookahead simulation (BoardState). Keeping the formula in one place
/// means simulated damage numbers can never drift from the real ones.
/// </summary>
public static class CombatMath
{
    public static (int damage, int retaliation) PredictDamage(Unit attacker, Unit defender, BoardState board)
    {
        UnitData attackData = board.GetData(attacker);
        UnitData defenseData = board.GetData(defender);
        return CalculateDamage(
            attackData.attackPower, board.GetHealth(attacker), attackData.maxHealth,
            board.GetDefensePower(defender), board.GetHealth(defender), defenseData.maxHealth);
    }

    public static (int damage, int retaliation, bool advance) PredictAttack(Unit attacker, Unit defender, BoardState board)
    {
        UnitData attackData = board.GetData(attacker);
        UnitData defenseData = board.GetData(defender);
        var (damage, retaliation) = PredictDamage(attacker, defender, board);
        bool killed = damage >= board.GetHealth(defender);
        Tile target = board.GetTile(defender);
        if (killed || !Utils.IsWithinDistance(target.gridPosition,
            board.GetTile(attacker).gridPosition, defenseData.attackRange)) retaliation = 0;
        return (damage, retaliation, killed && attackData.attackRange == 1 && target.terrainType != TerrainType.Mountain);
    }

    // Reuse caller-owned buffers; only inspect tiles inside the splash radius.
    public static void CollectSplashTargets(Unit attacker, Unit primary, Tile center, BoardState board, List<Unit> targets)
    {
        targets.Clear();
        if (attacker.data.splashDamage <= 0 || attacker.data.splashRadius <= 0 || center == null) return;
        int radius = attacker.data.splashRadius;
        var origin = center.gridPosition;
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
        {
            if (x == 0 && y == 0) continue;
            if (!GridManager.Instance.grid.TryGetValue(origin + new Vector2Int(x, y), out Tile tile)) continue;
            Unit unit = board.GetOccupant(tile);
            if (unit != null && unit != primary && unit != attacker && board.IsAlive(unit) &&
                board.IsAtWar(board.GetUnitOwner(attacker), board.GetUnitOwner(unit))) targets.Add(unit);
        }
    }

    public static (int attackDamage, int defenseDamage) CalculateDamage(
        int attackerPower, int attackerHealth, int attackerMaxHealth,
        int defenderPower, int defenderHealth, int defenderMaxHealth)
    {
        float attackForce = attackerPower * ((float)attackerHealth / attackerMaxHealth);
        float defenseForce = defenderPower * ((float)defenderHealth / defenderMaxHealth);
        float totalForce = attackForce + defenseForce;

        float rawDamage = (attackForce / totalForce) * attackerPower * 4.5f;
        int attackDamage = Mathf.Max(1, Mathf.FloorToInt(rawDamage + 0.5f));

        float rawDefence = (defenseForce / totalForce) * defenderPower * 4.5f;
        int defenseDamage = Mathf.Max(1, Mathf.FloorToInt(rawDefence + 0.5f));

        return (attackDamage, defenseDamage);
    }
}
