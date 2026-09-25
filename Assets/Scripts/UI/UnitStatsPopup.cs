using TMPro;
using UnityEngine;

public class UnitStatsPopup : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI unitNameText;
    [SerializeField] TextMeshProUGUI costText;
    [SerializeField] TextMeshProUGUI healthText;
    [SerializeField] TextMeshProUGUI attackText;
    [SerializeField] TextMeshProUGUI defenseText;
    [SerializeField] TextMeshProUGUI moveRangeText;
    [SerializeField] TextMeshProUGUI attackRangeText;
    [SerializeField] TextMeshProUGUI skillsText;

    public void Populate(UnitData unit)
    {
        unitNameText.text = unit.name;
        costText.text = unit.cost + " stars";
        healthText.text = "HP: " + unit.maxHealth;
        attackText.text = "Attack: " + unit.attackPower;
        defenseText.text = "Defense: " + unit.defensePower;
        moveRangeText.text = "Move: " + unit.moveRange;
        attackRangeText.text = "Range: " + unit.attackRange;

        skillsText.text = (unit.skills != null && unit.skills.Length > 0)
            ? string.Join("\n", System.Array.ConvertAll(unit.skills, SkillDescription))
            : "";
        if (unit.splashDamage > 0 && unit.splashRadius > 0)
            skillsText.text += $"\nSplash: {unit.splashDamage} damage, radius {unit.splashRadius}";
    }

    private static string SkillDescription(Skill skill) => skill switch
    {
        Skill.CavalryKiller => $"Cavalry Killer: +{TurnManager.Instance.combatSettings.cavalryKillerBonus} attack against units with base speed > 1",
        Skill.SpearWall => $"Spear Wall: +{TurnManager.Instance.combatSettings.spearWallBonus} defense against units with attack range 1",
        _ => skill.ToString()
    };
}
