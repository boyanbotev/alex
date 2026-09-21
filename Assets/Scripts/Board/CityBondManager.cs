using System.Collections.Generic;
using UnityEngine;

public sealed class CityBond
{
    public readonly City a, b;
    internal readonly Player ownerA, ownerB;
    public readonly List<Tile> route = new();
    public bool Active { get; internal set; }
    public City Other(City city) => city == a ? b : city == b ? a : null;
    public CityBond(City a, City b) { this.a = a; this.b = b; ownerA = a.owner; ownerB = b.owner; }
}

// Match-owned relationships. Perks never propagate beyond a direct partner.
public sealed class CityBondManager
{
    private readonly TurnManager turns;
    private readonly List<CityBond> bonds = new();
    private readonly HashSet<Tile> reinforced = new();
    public IReadOnlyList<CityBond> All => bonds;
    public CityBondManager(TurnManager turns) { this.turns = turns; }
    public bool IsReinforced(Tile tile) => reinforced.Contains(tile);
    public int Count(City city)
    {
        int count = 0;
        foreach (var bond in bonds) if (bond.Other(city) != null) count++;
        return count;
    }
    public bool Friendly(Player a, Player b) => a != null && b != null &&
        (a == b || turns.Diplomacy.GetRelation(a, b) == DiplomaticRelation.Allied);

    public bool CanCreate(Player actor, City a, City b, out string reason)
    {
        reason = null;
        if (actor == null || actor != turns.ActivePlayer || a == null || b == null || a == b || a.owner != actor)
            reason = "Select another city from your own city.";
        else if (!Friendly(a.owner, b.owner)) reason = "Choose your own or an allied city.";
        else if (a.HasPendingCapture || b.HasPendingCapture) reason = "A city is under siege.";
        else if (bonds.Exists(x => x.Other(a) == b)) reason = "These cities already share a bond.";
        else if (Count(a) >= turns.maxBondsPerCity || Count(b) >= turns.maxBondsPerCity) reason = "A city has no free bond slots.";
        else if (!turns.Neurons.AreConnected(a, b)) reason = "Build a neuron connection first.";
        return reason == null;
    }

    public bool TryCreate(Player actor, City a, City b)
    {
        if (!CanCreate(actor, a, b, out _) || turns.bondUpgradeCost < 0) return false;
        var bond = new CityBond(a, b);
        if (!turns.Neurons.TryGetRoute(a, b, bond.route) || !actor.SpendStars(turns.bondUpgradeCost)) return false;
        bonds.Add(bond);
        Refresh();
        return true;
    }

    public int GetAmount(City city, CityPerkKind kind)
    {
        int amount = NativeAmount(city, kind);
        foreach (var bond in bonds)
            if (bond.Active) amount = Mathf.Max(amount, NativeAmount(bond.Other(city), kind));
        return amount;
    }
    private static int NativeAmount(City city, CityPerkKind kind) =>
        city != null && city.data != null && city.data.perk != null && city.data.perk.kind == kind
            ? Mathf.Max(0, city.data.perk.amount) : 0;

    public void RemoveCity(City city)
    {
        bonds.RemoveAll(x => x.a == city || x.b == city);
        Refresh();
    }

    public void Refresh()
    {
        var changed = new HashSet<Tile>(reinforced);
        reinforced.Clear();
        bonds.RemoveAll(x => x.a == null || x.b == null || x.a.owner != x.ownerA || x.b.owner != x.ownerB);
        foreach (var bond in bonds)
        {
            bond.Active = Friendly(bond.a.owner, bond.b.owner);
            if (bond.Active && !RouteIntact(bond))
                bond.Active = turns.Neurons.TryGetRoute(bond.a, bond.b, bond.route);
            if (bond.Active) foreach (var tile in bond.route) reinforced.Add(tile);
        }
        changed.UnionWith(reinforced);
        foreach (var tile in changed)
        {
            if (tile == null || tile.currentBuilding == null) continue;
            var visual = tile.currentBuilding.GetComponent<NeuronSegmentVisual>();
            if (visual != null) visual.Refresh();
        }
    }
    private static bool RouteIntact(CityBond bond)
    {
        if (bond.route.Count == 0) return false;
        foreach (var tile in bond.route)
            if (tile == null || tile.city != null || tile.currentBuilding == null ||
                !tile.currentBuilding.IsPlacedNeuron || tile.currentBuilding.owner == null) return false;
        return true;
    }
}
