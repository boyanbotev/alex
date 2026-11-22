using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A separate action strip above recruitment; partner selection uses the map.
public sealed class CityBondView : MonoBehaviour
{
    private City source, target;
    private Player viewer;
    private bool CanManage => source != null && source.owner == viewer;
    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Button action, cancel;
    [SerializeField] private TextMeshProUGUI actionLabel;
    [Tooltip("Show 'Home' and partner city names under the perk icons.")]
    [SerializeField] private bool showPerkCaptions = true;
    // Matches the map highlight used when choosing a city to share perks with.
    public static readonly Color ShareColor = new(.2f, .9f, .8f);
    private RectTransform slotRow;
    private readonly List<CityPerkSlot> slotPool = new();
    private Vector2 labelOffsetMin;


    private readonly List<Vector3> candidates = new();
    private RectTransform cityPanel;
    private Vector2 normalCitySize, normalPanelSize, normalPanelPosition;
    private bool compact;
    public bool Selecting { get; private set; }
    public event System.Action SelectionChanged;
    private CityBondManager Bonds => TurnManager.Instance.Bonds;

    private void Awake()
    {
        action.onClick.AddListener(Act);
        cancel.onClick.AddListener(Cancel);
        BlendIntoCityPanel();
    }

    // Perk sharing is a section of the city panel, not an inset card: span the full width
    // without a background so its content lines up with the city heading.
    private void BlendIntoCityPanel()
    {
        if (panel.TryGetComponent<Image>(out var background)) background.enabled = false;
        panel.anchorMin = new Vector2(0, panel.anchorMin.y);
        panel.anchorMax = new Vector2(1, panel.anchorMax.y);
        panel.sizeDelta = new Vector2(0, panel.sizeDelta.y);
        panel.anchoredPosition = new Vector2(0, panel.anchoredPosition.y);
        // Buttons keep a fixed width at the left rather than stretching across the screen.
        PlaceButton((RectTransform)action.transform, 16f, 360f);
        PlaceButton((RectTransform)cancel.transform, 16f + 360f + 16f, 180f);
    }

    private static void PlaceButton(RectTransform button, float x, float width)
    {
        button.anchorMin = button.anchorMax = button.pivot = Vector2.zero;
        button.anchoredPosition = new Vector2(x, 8f);
        button.sizeDelta = new Vector2(width, 47f);
    }
    public void ApplyStyle()
    {
        var style = UIStyle.Shared;
        if (style == null) return;
        style.Surface(panel, style.Gameplay);
        style.Button(cancel, style.Gameplay, false);
    }
    public void Show(City city)
    {
        RestoreLayout();
        source = city; target = null; Selecting = false;
        viewer = TurnManager.Instance.ActivePlayer;
        panel.gameObject.SetActive(true);

        Refresh();
    }
    public void Hide()
    {
        RestoreLayout();
        bool wasSelecting = Selecting;
        if (wasSelecting) GridManager.Instance?.ClearAllHighlights();
        Selecting = false; source = target = null;

        if (panel != null) panel.gameObject.SetActive(false);
        if (wasSelecting) SelectionChanged?.Invoke();
    }
    private void Update()
    {
        if (source == null || !panel.gameObject.activeInHierarchy) return;
        if (viewer != TurnManager.Instance.ActivePlayer || viewer.isAI ||
            viewer.visibleTiles == null || !viewer.visibleTiles.IsVisible(source.centerTile)) { Hide(); return; }
        if (Selecting && Input.GetKeyDown(KeyCode.Escape)) Cancel();
    }
    private void Act()
    {
        if (!CanManage) return;
        if (!Selecting)
        {
            Selecting = true; target = null;
            SelectionChanged?.Invoke();
    
            Highlight(); Refresh();
            FrameCandidates();
        }
        else if (target != null)
        {
            if (Bonds.TryCreate(TurnManager.Instance.ActivePlayer, source, target)) Cancel();
            else Refresh();
        }
    }
    // Clicks on cities that cannot share are consumed but ignored.
    public bool Select(Tile tile)
    {
        if (!Selecting) return false;
        City city = tile != null ? tile.city : null;
        if (city == null || !IsCandidate(city)) return true;
        target = city;
        Highlight(); Refresh();
        return true;
    }
    private bool IsCandidate(City city) =>
        viewer.visibleTiles != null && viewer.visibleTiles.IsVisible(city.centerTile) &&
        Bonds.CanCreate(viewer, source, city, out _);
    private bool HasCandidate()
    {
        foreach (var city in WorldPopulationManager.Instance.allCities)
            if (IsCandidate(city)) return true;
        return false;
    }
    private void Cancel()
    {
        RestoreLayout();
        Selecting = false; target = null;
        GridManager.Instance.ClearAllHighlights();
        SelectionChanged?.Invoke();

        Refresh();
    }
    private void Highlight()
    {
        GridManager.Instance.ClearAllHighlights();
        if (target != null && IsCandidate(target))
        {
            target.centerTile.SetHighlight(true, ShareColor);
            return;
        }
        foreach (var city in WorldPopulationManager.Instance.allCities)
            if (IsCandidate(city))
                city.centerTile.SetExpandingHighlight(true, ShareColor);
    }
    public void Refresh()
    {
        if (source == null) return;
        int cost = Mathf.Max(0, TurnManager.Instance.bondUpgradeCost);
        bool affordable = TurnManager.Instance.ActivePlayer.stars >= cost;
        // Sharing UI only appears when some visible city can actually share perks.
        action.gameObject.SetActive(CanManage && (Selecting || HasCandidate()));
        cancel.gameObject.SetActive(Selecting);
        actionLabel.text = Selecting ? $"Confirm sharing · {CurrencyUI.Amount(cost)}" : $"Share perks · {CurrencyUI.Amount(cost)}";
        bool valid = Selecting && target != null && IsCandidate(target);
        RefreshSlots(valid ? target : null);
        var text = new StringBuilder();
        text.Append($"Perk sharing {Bonds.Count(source)}/{TurnManager.Instance.maxBondsPerCity}");
        if (!Selecting) action.interactable = affordable;
        else
        {
            if (!valid) text.Append("\nChoose a highlighted city to share perks with.");
            else
            {
                text.Append($"\n{target.cityName} receives {PerkName(source)}.");
                if (!affordable) text.Append($"\nRequires {CurrencyUI.Amount(cost)}.");
            }
            action.interactable = valid && affordable;
        }
        label.text = text.ToString();
        if (Selecting) CompactLayout();
    }

    // Home perk first, then one slot per sharing bond, then any free sharing slots.
    private void RefreshSlots(City preview)
    {
        EnsureSlotRow();
        var style = UIStyle.Shared;
        var palette = style != null ? style.Gameplay : null;
        Color background = palette != null ? palette.secondaryPanel : new Color(.19f, .24f, .31f);
        Color muted = palette != null ? palette.mutedText : Color.gray;
        Color home = palette != null ? palette.accent : new Color(.72f, .61f, .38f);
        Color empty = new Color(muted.r, muted.g, muted.b, .35f);

        int count = 0;
        var homePerk = PerkOf(source);
        Slot(count++).Set(homePerk != null ? homePerk.icon : null, homePerk != null ? source.PerkLevel : 0,
            homePerk != null ? home : empty, background, muted, showPerkCaptions ? "Home" : null, false,
            homePerk != null
                ? () => ShowPerk(source, homePerk, "Home perk")
                : () => UIManager.Instance.ShowPerkPopup("No home perk", "", "Home perk",
                    "This city has no perk of its own.", ""));

        foreach (var bond in Bonds.All)
        {
            City partner = bond.Other(source);
            if (partner == null) continue;
            string name = PartnerName(partner);
            var perk = PerkOf(partner);
            string origin = bond.Active ? $"Borrowed from {name}"
                : $"Borrowed from {name} · suspended until the neuron route and alliance are restored";
            Slot(count++).Set(perk != null ? perk.icon : null, perk != null ? partner.PerkLevel : 0,
                perk != null ? ShareColor : empty, background, muted, showPerkCaptions ? name : null, !bond.Active,
                perk != null
                    ? () => ShowPerk(partner, perk, origin)
                    : () => UIManager.Instance.ShowPerkPopup("No perk", "", origin,
                        $"{name} has no perk to share, but receives this city's perk.", ""));
        }

        int slots = Mathf.Max(count, 1 + TurnManager.Instance.maxBondsPerCity);
        while (count < slots)
        {
            var previewPerk = PerkOf(preview);
            if (previewPerk != null)
            {
                City partner = preview;
                string origin = $"Preview: borrowed from {partner.cityName}";
                Slot(count++).Set(previewPerk.icon, partner.PerkLevel, ShareColor, background, muted,
                    showPerkCaptions ? partner.cityName : null, true, () => ShowPerk(partner, previewPerk, origin));
            }
            else Slot(count++).Set(null, 0, empty, background, muted, showPerkCaptions ? "Empty" : null, false,
                () => UIManager.Instance.ShowPerkPopup("Empty sharing slot", "", "Borrowed perk",
                    "Share perks with a connected city of your own or an ally to borrow its perk. Both cities receive each other's perk.", ""));
            preview = null;
        }
        for (int i = 0; i < slotPool.Count; i++) slotPool[i].gameObject.SetActive(i < count);

        // The status text sits to the right of the slot row.
        float rowWidth = count * SlotSize + (count - 1) * SlotGap;
        label.rectTransform.offsetMin = new Vector2(labelOffsetMin.x + rowWidth + SlotGap, labelOffsetMin.y);
    }

    private float SlotSize => showPerkCaptions ? 38f : 44f;
    private float SlotGap => showPerkCaptions ? 24f : 10f;
    private float RowHeight => SlotSize + (showPerkCaptions ? 16f : 0f);

    private void EnsureSlotRow()
    {
        if (slotRow != null) return;
        labelOffsetMin = label.rectTransform.offsetMin;
        slotRow = new GameObject("Perk Slots", typeof(RectTransform)).GetComponent<RectTransform>();
        slotRow.SetParent(panel, false);
        // Align with the label's original top-left inset.
        slotRow.anchorMin = slotRow.anchorMax = slotRow.pivot = new Vector2(0, 1);
        slotRow.anchoredPosition = new Vector2(labelOffsetMin.x, label.rectTransform.offsetMax.y);
        slotRow.sizeDelta = Vector2.zero;
    }

    private CityPerkSlot Slot(int index)
    {
        while (slotPool.Count <= index)
            slotPool.Add(CityPerkSlot.Create(slotRow, SlotSize, SlotSize + SlotGap - 4f));
        var slot = slotPool[index];
        ((RectTransform)slot.transform).anchoredPosition = new Vector2(index * (SlotSize + SlotGap), 0);
        return slot;
    }

    private string PartnerName(City partner) =>
        viewer.visibleTiles != null && viewer.visibleTiles.IsVisible(partner.centerTile) ? partner.cityName : "Unrevealed city";

    private static CityPerkData PerkOf(City city) =>
        city != null && city.data != null && city.data.perk != null && city.data.perk.kind != CityPerkKind.None
            ? city.data.perk : null;

    private static string PerkName(CityPerkData perk) =>
        string.IsNullOrEmpty(perk.perkName) ? perk.kind.ToString() : perk.perkName;
    private static string PerkName(City city) => PerkOf(city) is { } perk ? PerkName(perk) : "no perk";

    private void ShowPerk(City perkSource, CityPerkData perk, string origin) =>
        UIManager.Instance.ShowPerkPopup(PerkName(perk), $"Level {perkSource.PerkLevel}", origin,
            Effect(perk), Unlocks(perk, perkSource.PerkLevel));

    private static string Effect(CityPerkData perk) => perk.kind switch
    {
        CityPerkKind.Healing => $"+{perk.amount} healing when friendly/allied units rest here.",
        CityPerkKind.Adrenaline => $"+{perk.amount} movement for friendly/allied units starting here.",
        CityPerkKind.Fortification => $"+{perk.amount} defence for friendly/allied units here.",
        CityPerkKind.Dopamine => $"Units created here earn +{perk.amount} coins for severing enemy neurons.",
        _ => perk.description
    };

    // Unlocks come from the recruiting city's faction data, so each faction lists its own units.
    public static string UnitsUnlocked(Faction faction, CityPerkKind kind, int level)
    {
        if (faction == null || faction.availableUnits == null) return "";
        var names = new List<string>();
        foreach (var unit in faction.availableUnits)
            if (unit != null && unit.unitData != null && unit.unitData.requiredPerk == kind &&
                unit.unitData.requiredPerkLevel == level) names.Add(unit.unitData.name);
        return string.Join(", ", names);
    }

    private string Unlocks(CityPerkData perk, int perkLevel)
    {
        var faction = source.owner != null ? source.owner.faction : null;
        if (faction == null || faction.availableUnits == null) return "";
        var style = UIStyle.Shared;
        string muted = ColorUtility.ToHtmlStringRGB(style != null ? style.Gameplay.mutedText : Color.gray);
        var text = new StringBuilder();
        for (int level = 1; level <= 2; level++)
        {
            string names = UnitsUnlocked(faction, perk.kind, level);
            if (names.Length == 0) continue;
            string line = $"{(level == 1 ? "Unlocks" : "Level 2 unlocks")}: {names}";
            if (level > perkLevel) line = $"<color=#{muted}>{line}</color>";
            if (text.Length > 0) text.Append('\n');
            text.Append(line);
        }
        return text.ToString();
    }
    private void CompactLayout()
    {
        if (!compact)
        {
            cityPanel = panel.parent as RectTransform;
            if (cityPanel == null) return;
            normalCitySize = cityPanel.sizeDelta;
            normalPanelSize = panel.sizeDelta;
            normalPanelPosition = panel.anchoredPosition;
            compact = true;
        }
        // Keep the city heading, then fit the existing text and button row.
        panel.anchoredPosition = new Vector2(normalPanelPosition.x, -40f);
        panel.sizeDelta = new Vector2(normalPanelSize.x, -40f);
        float textHeight = label.GetPreferredValues(label.text, label.rectTransform.rect.width, Mathf.Infinity).y;
        cityPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40f + Mathf.Max(textHeight, RowHeight) + 85f);
    }
    private void RestoreLayout()
    {
        if (!compact) return;
        cityPanel.sizeDelta = normalCitySize;
        panel.sizeDelta = normalPanelSize;
        panel.anchoredPosition = normalPanelPosition;
        compact = false;
    }
    private void FrameCandidates()
    {
        Camera camera = Camera.main;
        if (camera == null || !camera.TryGetComponent<CameraController>(out var controller)) return;
        candidates.Clear();
        candidates.Add(source.centerTile.transform.position);
        foreach (var city in WorldPopulationManager.Instance.allCities)
            if (IsCandidate(city))
                candidates.Add(city.centerTile.transform.position);
        if (candidates.Count < 2 || cityPanel == null) return;
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        cityPanel.GetWorldCorners(corners);
        Canvas canvas = cityPanel.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        float panelTop = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[1]).y;
        controller.FramePoints(candidates, panelTop);
    }
}
