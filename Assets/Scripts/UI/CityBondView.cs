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


    private readonly List<Tile> route = new();
    public bool Selecting { get; private set; }
    private CityBondManager Bonds => TurnManager.Instance.Bonds;

    private void Awake()
    {
        action.onClick.AddListener(Act);
        cancel.onClick.AddListener(Cancel);
    }
    public void Show(City city)
    {
        source = city; target = null; Selecting = false;
        viewer = TurnManager.Instance.ActivePlayer;
        panel.gameObject.SetActive(true);

        Refresh();
    }
    public void Hide()
    {
        if (Selecting) GridManager.Instance?.ClearAllHighlights();
        Selecting = false; source = target = null;

        if (panel != null) panel.gameObject.SetActive(false);
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
            UIManager.Instance.SetRecruitmentVisible(false);
    
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

        Refresh();
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
        action.gameObject.SetActive(CanManage);
        cancel.gameObject.SetActive(Selecting);
        actionLabel.text = Selecting ? $"Confirm bond · {cost} stars" : $"Reinforce bond · {cost} stars";
        var text = new StringBuilder();
        text.AppendLine($"Bonds {Bonds.Count(source)}/{TurnManager.Instance.maxBondsPerCity}");
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
    private static string Perk(City city)
    {
        var perk = city.data != null ? city.data.perk : null;
        if (perk == null) return "None";
        string effect = perk.kind switch
        {
            CityPerkKind.Healing => $"+{perk.amount} healing when friendly/allied units rest here; archers" + (city.PerkLevel >= 2 ? " and catapults" : "; level 2 adds catapults"),
            CityPerkKind.Adrenaline => $"+{perk.amount} movement for friendly/allied units starting here; cavalry" + (city.PerkLevel >= 2 ? " and knights" : "; level 2 adds knights"),
            CityPerkKind.Fortification => $"+{perk.amount} defence for friendly/allied units here; defenders" + (city.PerkLevel >= 2 ? " and Colossus" : "; level 2 adds Colossus"),
            CityPerkKind.Dopamine => $"Units created here earn +{perk.amount} stars for severing enemy neurons",
            _ => perk.description
        };
        return $"{(string.IsNullOrEmpty(perk.perkName) ? perk.kind.ToString() : perk.perkName)} (level {city.PerkLevel}) — {effect}" +
            (perk.kind == CityPerkKind.Dopamine ? "" : ". Recruitment requires faction availability.");
    }
}
