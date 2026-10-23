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
    private TextMeshProUGUI descriptionText;

    private void PrepareHoverLayout()
    {
        if (descriptionText != null) return;

        // Retain the old popup's artwork and text styling, without its tech-screen controls.
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        var group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        descriptionText = Instantiate(skillsText, transform);
        descriptionText.name = "Description";
        Place(unitNameText, 20, 16, 310, 32);
        Place(costText, 350, 16, 130, 32);
        Place(healthText, 20, 60, 220, 26);
        Place(attackText, 260, 60, 220, 26);
        Place(defenseText, 20, 90, 220, 26);
        Place(moveRangeText, 260, 90, 220, 26);
        Place(attackRangeText, 20, 120, 460, 26);
    }

    private void Place(TextMeshProUGUI text, float x, float y, float width, float height)
    {
        RectTransform rect = text.rectTransform;
        rect.SetParent(transform, false);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition3D = new Vector3(x, -y, 0);
        rect.sizeDelta = new Vector2(width, height);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.fontSize = 18;
        text.enableAutoSizing = false;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
        text.gameObject.SetActive(true);
    }

    public void Populate(UnitData unit)
    {
        PrepareHoverLayout();
        unitNameText.text = unit.name;
        costText.text = CurrencyUI.Amount(unit.cost);
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

        skillsText.text = skillsText.text.Trim();
        descriptionText.text = unit.description;
        float descriptionHeight = string.IsNullOrWhiteSpace(unit.description)
            ? 0 : descriptionText.GetPreferredValues(unit.description, 460, Mathf.Infinity).y;
        Place(descriptionText, 20, 158, 460, descriptionHeight);
        float skillsY = 158 + descriptionHeight + (descriptionHeight > 0 ? 12 : 0);
        float skillsHeight = skillsText.GetPreferredValues(skillsText.text, 460, Mathf.Infinity).y;
        Place(skillsText, 20, skillsY, 460, skillsHeight);
        ((RectTransform)transform).sizeDelta = new Vector2(500, skillsY + skillsHeight + 20);
    }

    private static string SkillDescription(Skill skill) => skill switch
    {
        Skill.CavalryKiller => $"Cavalry Killer: +{TurnManager.Instance.combatSettings.cavalryKillerBonus} attack against units with base speed > 1",
        Skill.SpearWall => $"Spear Wall: +{TurnManager.Instance.combatSettings.spearWallBonus} defense against units with attack range 1",
        _ => skill.ToString()
    };
}
