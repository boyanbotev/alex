using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A lightweight, disposable overlay of hypothetical game state used for AI
/// lookahead. It only stores values that differ from the live scene - anything
/// not recorded here falls back to the real Unit/Tile/City. Every mutation
/// returns a NEW BoardState; nothing here ever touches a GameObject, so the AI
/// can branch into many hypothetical futures and throw most of them away with
/// zero risk to the actual game state.
/// </summary>
public class BoardState
{
    private readonly Dictionary<Unit, Tile> unitTile;
    private readonly Dictionary<Unit, int> unitHealth;
    private readonly Dictionary<Unit, bool> unitMoved;
    private readonly Dictionary<Unit, bool> unitAttacked;
    private readonly Dictionary<Unit, bool> unitActive;
    private readonly HashSet<Unit> unitDead;
    private readonly Dictionary<Tile, Unit> tileOccupant;
    private readonly Dictionary<City, Player> cityOwner;
    private readonly Dictionary<City, Unit> pendingCityCaptures;

    /// <summary>
    /// The "no overrides yet" state - reads always fall through to the real scene.
    /// </summary>
    public static readonly BoardState Live = new BoardState();

    public BoardState()
    {
        unitTile = new Dictionary<Unit, Tile>();
        unitHealth = new Dictionary<Unit, int>();
        unitMoved = new Dictionary<Unit, bool>();
        unitAttacked = new Dictionary<Unit, bool>();
        unitActive = new Dictionary<Unit, bool>();
        unitDead = new HashSet<Unit>();
        tileOccupant = new Dictionary<Tile, Unit>();
        cityOwner = new Dictionary<City, Player>();
        pendingCityCaptures = new Dictionary<City, Unit>();
    }

    private BoardState(BoardState src)
    {
        unitTile = new Dictionary<Unit, Tile>(src.unitTile);
        unitHealth = new Dictionary<Unit, int>(src.unitHealth);
        unitMoved = new Dictionary<Unit, bool>(src.unitMoved);
        unitAttacked = new Dictionary<Unit, bool>(src.unitAttacked);
        unitActive = new Dictionary<Unit, bool>(src.unitActive);
        unitDead = new HashSet<Unit>(src.unitDead);
        tileOccupant = new Dictionary<Tile, Unit>(src.tileOccupant);
        cityOwner = new Dictionary<City, Player>(src.cityOwner);
        pendingCityCaptures = new Dictionary<City, Unit>(src.pendingCityCaptures);
    }

    // ---------------- Readers ----------------

    public Tile GetTile(Unit u) => unitTile.TryGetValue(u, out var t) ? t : u.currentTile;
    public int GetHealth(Unit u) => unitHealth.TryGetValue(u, out var h) ? h : u.currentHealth;
    public bool HasMoved(Unit u) => unitMoved.TryGetValue(u, out var m) ? m : u.hasMoved;
    public bool HasAttacked(Unit u) => unitAttacked.TryGetValue(u, out var a) ? a : u.hasAttacked;
    public bool IsActive(Unit u) => unitActive.TryGetValue(u, out var a) ? a : u.isActive;
    public bool IsAlive(Unit u) => !unitDead.Contains(u) && u.isAlive;
    public Player GetOwner(City c) => cityOwner.TryGetValue(c, out var p) ? p : c.owner;
    public bool HasPendingCityCapture(City city)
    {
        return pendingCityCaptures.ContainsKey(city);
    }

    public Unit GetPendingCityCapturer(City city)
    {
        return pendingCityCaptures.TryGetValue(city, out var unit)
            ? unit
            : null;
    }

    public Unit GetOccupant(Tile t)
    {
        if (tileOccupant.TryGetValue(t, out var overridden))
            return overridden;

        Unit live = t.currentUnit;
        if (live == null)
            return null;

        // If this unit has been simulated as having moved away, or died,
        // the live occupant reference is stale for this hypothetical state.
        if (unitTile.TryGetValue(live, out var movedTo) && movedTo != t)
            return null;

        if (unitDead.Contains(live))
            return null;

        return live;
    }

    // ---------------- Mutators ----------------

    public BoardState WithMove(Unit unit, Tile from, Tile to)
    {
        BoardState s = new BoardState(this);
        s.tileOccupant[from] = null;
        s.tileOccupant[to] = unit;
        s.unitTile[unit] = to;
        s.unitMoved[unit] = true;

        s.RemovePendingCapturesForUnit(unit);

        if (unit.data.skills.Any(sk => sk == Skill.Static))
            s.unitActive[unit] = false;

        return s;
    }

    public BoardState WithPendingCityCapture(City city, Unit unit)
    {
        BoardState s = new BoardState(this);

        s.pendingCityCaptures[city] = unit;

        return s;
    }

    public BoardState WithoutPendingCityCapture(City city)
    {
        BoardState s = new BoardState(this);
        s.pendingCityCaptures.Remove(city);
        return s;
    }

    public BoardState WithCityClaim(City city, Player newOwner)
    {
        BoardState s = new BoardState(this);

        s.cityOwner[city] = newOwner;

        s.pendingCityCaptures.Remove(city);

        return s;
    }

    public BoardState WithDamage(Unit unit, int newHealth)
    {
        BoardState s = new BoardState(this);
        s.unitHealth[unit] = newHealth;

        if (newHealth <= 0)
        {
            s.unitDead.Add(unit);
            Tile tile = s.GetTile(unit);
            s.tileOccupant[tile] = null;

            s.RemovePendingCapturesForUnit(unit);
        }

        return s;
    }

    public BoardState WithAttacked(Unit unit)
    {
        BoardState s = new BoardState(this);
        s.unitAttacked[unit] = true;
        s.unitMoved[unit] = true;
        return s;
    }

    public BoardState WithDeactivated(Unit unit)
    {
        BoardState s = new BoardState(this);
        s.unitActive[unit] = false;
        return s;
    }

    private void RemovePendingCapturesForUnit(Unit unit)
    {
        List<City> citiesToRemove = pendingCityCaptures
            .Where(pair => pair.Value == unit)
            .Select(pair => pair.Key)
            .ToList();

        foreach (City city in citiesToRemove)
            pendingCityCaptures.Remove(city);
    }
}
