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
            target.centerTile.SetHighlight(true, new Color(.2f, .9f, .8f));
            return;
        }
        foreach (var city in WorldPopulationManager.Instance.allCities)
            if (IsCandidate(city))
                city.centerTile.SetExpandingHighlight(true, new Color(.2f, .9f, .8f));
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
        var text = new StringBuilder();
        text.AppendLine($"Perk sharing {Bonds.Count(source)}/{TurnManager.Instance.maxBondsPerCity}");
        text.AppendLine($"Native perk: {Perk(source)}");
        if (!Selecting)
        {
            foreach (var bond in Bonds.All)
            {
                City partner = bond.Other(source);
                if (partner == null) continue;
                string partnerName = viewer.visibleTiles != null && viewer.visibleTiles.IsVisible(partner.centerTile)
                    ? partner.cityName : "Unrevealed city";
                text.AppendLine($"Shared from {partnerName} ({(bond.Active ? "active" : "suspended")}): {Perk(partner)}");
            }
            action.interactable = affordable;
        }
        else
        {
            bool valid = target != null && IsCandidate(target);
            if (!valid) text.Append("Choose a highlighted city to share perks with.");
            else
            {
                text.AppendLine($"{source.cityName} receives: {Perk(target)}");
                text.AppendLine($"{target.cityName} receives: {Perk(source)}");
            }
            if (valid && !affordable) text.Append($"\nRequires {CurrencyUI.Amount(cost)}.");
            action.interactable = valid && affordable;
        }
        label.text = text.ToString();
        if (Selecting) CompactLayout();
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
        cityPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40f + textHeight + 85f);
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
    private static string Perk(City city)
    {
        var perk = city.data != null ? city.data.perk : null;
        if (perk == null) return "None";
        string effect = perk.kind switch
        {
            CityPerkKind.Healing => $"+{perk.amount} healing when friendly/allied units rest here; archers" + (city.PerkLevel >= 2 ? " and catapults" : "; level 2 adds catapults"),
            CityPerkKind.Adrenaline => $"+{perk.amount} movement for friendly/allied units starting here; cavalry" + (city.PerkLevel >= 2 ? " and knights" : "; level 2 adds knights"),
            CityPerkKind.Fortification => $"+{perk.amount} defence for friendly/allied units here; defenders" + (city.PerkLevel >= 2 ? " and Colossus" : "; level 2 adds Colossus"),
            CityPerkKind.Dopamine => $"Units created here earn +{perk.amount} coins for severing enemy neurons",
            _ => perk.description
        };
        return $"{perk.IconTag}{(string.IsNullOrEmpty(perk.perkName) ? perk.kind.ToString() : perk.perkName)} (level {city.PerkLevel}) — {effect}" +
            (perk.kind == CityPerkKind.Dopamine ? "" : ". Recruitment requires faction availability.");
    }
}
