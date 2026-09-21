using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A separate action strip above recruitment; partner selection uses the map.
public sealed class CityBondView : MonoBehaviour
{
    private City source, target;
    private RectTransform panel;
    private TextMeshProUGUI label;
    private Button action, cancel;
    private RectTransform spawnPanel;
    private Vector2 spawnSize;
    private readonly List<Tile> route = new();
    public bool Selecting { get; private set; }
    private CityBondManager Bonds => TurnManager.Instance.Bonds;

    public void Initialize(RectTransform spawn, TMP_FontAsset font)
    {
        spawnPanel = spawn;
        spawnSize = spawn.sizeDelta;
        panel = new GameObject("City bond actions", typeof(RectTransform), typeof(Image), typeof(UIInputBlocker)).GetComponent<RectTransform>();
        panel.SetParent(spawn, false);
        panel.anchorMin = new Vector2(0, 1);
        panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(.5f, 0);
        panel.anchoredPosition = new Vector2(0, 8);
        panel.sizeDelta = new Vector2(0, 190);
        panel.GetComponent<Image>().color = new Color(.08f, .12f, .17f, .97f);
        label = MakeLabel(panel, font);
        label.rectTransform.anchorMin = new Vector2(0, .32f);
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(16, 4);
        label.rectTransform.offsetMax = new Vector2(-16, -10);
        label.fontSize = 18;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12;
        label.fontSizeMax = 18;
        action = MakeButton("Reinforce bond", panel, font, new Vector2(0, 0), new Vector2(.68f, .3f));
        cancel = MakeButton("Cancel", panel, font, new Vector2(.68f, 0), new Vector2(1, .3f));
        action.onClick.AddListener(Act);
        cancel.onClick.AddListener(Cancel);
    }
    public void Show(City city)
    {
        source = city; target = null; Selecting = false;
        panel.gameObject.SetActive(true);
        Layout();
        Refresh();
    }
    public void Hide()
    {
        if (Selecting) GridManager.Instance?.ClearAllHighlights();
        Selecting = false; source = target = null;
        if (spawnPanel != null) spawnPanel.sizeDelta = spawnSize;
        if (panel != null) panel.gameObject.SetActive(false);
    }
    private void Update()
    {
        if (source == null || !panel.gameObject.activeInHierarchy) return;
        if (source.owner != TurnManager.Instance.ActivePlayer || source.owner.isAI) { Hide(); return; }
        if (Selecting && Input.GetKeyDown(KeyCode.Escape)) Cancel();
    }
    private void Act()
    {
        if (!Selecting)
        {
            Selecting = true; target = null;
            UIManager.Instance.SetRecruitmentVisible(false);
            Layout();
            Highlight(); Refresh();
        }
        else if (target != null)
        {
            if (Bonds.TryCreate(TurnManager.Instance.ActivePlayer, source, target)) Cancel();
            else Refresh();
        }
    }
    public bool Select(Tile tile)
    {
        if (!Selecting) return false;
        target = tile != null ? tile.city : null;
        if (target != null && !TurnManager.Instance.ActivePlayer.visibleTiles.IsVisible(target.centerTile)) target = null;
        Highlight(); Refresh();
        return true;
    }
    private void Cancel()
    {
        Selecting = false; target = null;
        GridManager.Instance.ClearAllHighlights();
        UIManager.Instance.SetRecruitmentVisible(true);
        Layout();
        Refresh();
    }
    private void Layout()
    {
        spawnPanel.sizeDelta = Selecting ? new Vector2(spawnSize.x, 190) : spawnSize;
        panel.anchorMin = new Vector2(0, Selecting ? 0 : 1);
        panel.anchorMax = new Vector2(1, Selecting ? 0 : 1);
        panel.anchoredPosition = new Vector2(0, Selecting ? 0 : 8);
        panel.sizeDelta = new Vector2(0, Selecting ? 190 : 130);
    }
    private void Highlight()
    {
        GridManager.Instance.ClearAllHighlights();
        Player actor = TurnManager.Instance.ActivePlayer;
        foreach (var city in WorldPopulationManager.Instance.allCities)
            if (actor.visibleTiles.IsVisible(city.centerTile) && Bonds.CanCreate(actor, source, city, out _))
                city.centerTile.SetHighlight(true, new Color(.2f, .9f, .8f));
        if (target != null && Bonds.CanCreate(actor, source, target, out _) &&
            TurnManager.Instance.Neurons.TryGetRoute(source, target, route))
            foreach (var tile in route)
                if (actor.visibleTiles.IsVisible(tile)) tile.SetHighlight(true, new Color(1f, .75f, .2f));
    }
    public void Refresh()
    {
        if (source == null) return;
        int cost = Mathf.Max(0, TurnManager.Instance.bondUpgradeCost);
        cancel.gameObject.SetActive(Selecting);
        action.GetComponentInChildren<TextMeshProUGUI>().text = Selecting ? $"Confirm bond · {cost} stars" : $"Reinforce bond · {cost} stars";
        var text = new StringBuilder();
        text.AppendLine($"{source.cityName} · Bonds {Bonds.Count(source)}/{TurnManager.Instance.maxBondsPerCity}");
        text.AppendLine($"Native perk: {Perk(source)}");
        if (!Selecting)
        {
            foreach (var bond in Bonds.All)
            {
                City partner = bond.Other(source);
                if (partner != null) text.AppendLine($"{partner.cityName}: {Perk(partner)}{(bond.Active ? "" : " (suspended)")}");
            }
            action.interactable = Bonds.Count(source) < TurnManager.Instance.maxBondsPerCity && !source.HasPendingCapture;
        }
        else
        {
            string reason = null;
            bool valid = target != null && Bonds.CanCreate(TurnManager.Instance.ActivePlayer, source, target, out reason);
            if (target == null) text.Append("Choose a highlighted connected city.");
            else if (!valid) text.Append(reason);
            else
            {
                text.AppendLine($"{source.cityName} receives: {Perk(target)}");
                text.AppendLine($"{target.cityName} receives: {Perk(source)}");
                text.Append("One slot in each city. Matching perks do not stack.");
            }
            bool affordable = TurnManager.Instance.ActivePlayer.stars >= cost;
            if (valid && !affordable) text.Append($"\nRequires {cost} stars.");
            action.interactable = valid && affordable;
        }
        label.text = text.ToString();
    }
    private static string Perk(City city) => city.data != null && city.data.perk != null ? city.data.perk.Summary : "None";
    private static TextMeshProUGUI MakeLabel(Transform parent, TMP_FontAsset font)
    {
        var text = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false); text.font = font; text.color = Color.white; text.raycastTarget = false;
        return text;
    }
    private static Button MakeButton(string title, Transform parent, TMP_FontAsset font, Vector2 min, Vector2 max)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = new Vector2(8, 8); rect.offsetMax = new Vector2(-8, -4);
        go.GetComponent<Image>().color = new Color(.16f, .4f, .46f);
        var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
        var text = MakeLabel(rect, font); text.text = title; text.fontSize = 18; text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero; text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }
}
