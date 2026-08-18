using UnityEngine;

/// <summary>
/// Pure, state-free combat math shared between real unit combat (Unit.Attack)
/// and AI lookahead simulation (BoardState). Keeping the formula in one place
/// means simulated damage numbers can never drift from the real ones.
/// </summary>
public static class CombatMath
{
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
