using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Two reusable, non-interactive indicators for a held attack preview.</summary>
public sealed class CombatPreviewUI : MonoBehaviour
{
    private sealed class Indicator
    {
        public RectTransform root;
        public TextMeshProUGUI text;
        public RawImage skull;
        public TextMeshProUGUI defense;
        public Unit unit;
        public Vector3 offset;
    }

    private Indicator attackerIndicator;
    private Indicator defenderIndicator;
    private Camera worldCamera;

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        // No GraphicRaycaster: previews must never intercept clicks or camera drags.
        attackerIndicator = CreateIndicator("Attacker");
        defenderIndicator = CreateIndicator("Defender");
    }

    private Indicator CreateIndicator(string label)
    {
        var root = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.sizeDelta = new Vector2(90f, 40f);
        var text = new GameObject("Damage", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(root, false);
        text.rectTransform.sizeDelta = root.sizeDelta;
        text.fontSize = 30f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.3f, 0.3f);
        text.outlineColor = Color.black;
        text.outlineWidth = 0.25f;
        text.raycastTarget = false;
        var skull = new GameObject("Skull", typeof(RectTransform)).AddComponent<RawImage>();
        skull.transform.SetParent(root, false);
        skull.rectTransform.sizeDelta = new Vector2(36f, 36f);
        skull.raycastTarget = false;
        var defense = new GameObject("Fortification", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        defense.transform.SetParent(root, false);
        defense.rectTransform.sizeDelta = new Vector2(150f, 24f);
        defense.rectTransform.anchoredPosition = new Vector2(0, 32f);
        defense.fontSize = 18f;
        defense.alignment = TextAlignmentOptions.Center;
        defense.color = new Color(.4f, .85f, 1f);
        defense.outlineColor = Color.black;
        defense.outlineWidth = .25f;
        defense.raycastTarget = false;
        return new Indicator { root = root, text = text, skull = skull, defense = defense };
    }

    public void Show(Unit attacker, Unit defender, Texture2D skull)
    {
        worldCamera = Camera.main;
        var (damage, retaliation) = attacker.PredictAttackDamage(defender);
        SetIndicator(attackerIndicator, attacker, retaliation, skull);
        SetIndicator(defenderIndicator, defender, damage, skull);
        int bonus = BoardState.Live.GetTerritoryPerk(defender, CityPerkKind.Fortification);
        attackerIndicator.defense.gameObject.SetActive(false);
        defenderIndicator.defense.gameObject.SetActive(bonus > 0);
        defenderIndicator.defense.text = $"+{bonus} DEF";
        gameObject.SetActive(true);
        LateUpdate();
    }

    private static void SetIndicator(Indicator indicator, Unit unit, int damage, Texture2D skull)
    {
        indicator.unit = unit;
        var renderer = unit.GetComponentInChildren<Renderer>();
        indicator.offset = renderer != null
            ? new Vector3(0f, renderer.bounds.max.y - unit.transform.position.y, 0f)
            : Vector3.up;
        bool lethal = damage >= unit.currentHealth;
        indicator.text.gameObject.SetActive(!lethal || skull == null);
        indicator.text.text = lethal ? "X" : damage == 0 ? "0" : "−" + damage;
        indicator.text.color = damage == 0 ? Color.white : new Color(1f, 0.3f, 0.3f);
        indicator.skull.texture = skull;
        indicator.skull.gameObject.SetActive(lethal && skull != null);
        if (skull != null)
            indicator.skull.rectTransform.sizeDelta = new Vector2(36f * skull.width / skull.height, 36f);
    }

    private void LateUpdate()
    {
        if (worldCamera == null || attackerIndicator.unit == null || defenderIndicator.unit == null ||
            !attackerIndicator.unit.isAlive || !defenderIndicator.unit.isAlive)
        {
            Hide();
            return;
        }
        Position(attackerIndicator);
        Position(defenderIndicator);
    }

    private void Position(Indicator indicator)
    {
        Vector3 screen = worldCamera.WorldToScreenPoint(indicator.unit.transform.position + indicator.offset);
        indicator.root.gameObject.SetActive(screen.z > 0f);
        indicator.root.position = new Vector3(screen.x, screen.y + 28f, 0f);
    }

    public void Hide() => gameObject.SetActive(false);
}
