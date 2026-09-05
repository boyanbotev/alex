using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Lightweight mutable overlay of hypothetical game state used by AI lookahead.
/// Values not overridden here fall back to the live scene.
///
/// Simulation is done with:
///     int checkpoint = board.Checkpoint();
///     board.WithMove(...);
///     ...
///     board.Rollback(checkpoint);
///
/// This avoids cloning the board for every simulated action.
/// </summary>
public class BoardState
{
    private readonly Dictionary<Unit, Tile> unitTile = new();
    private readonly Dictionary<Unit, int> unitHealth = new();
    private readonly Dictionary<Unit, bool> unitMoved = new();
    private readonly Dictionary<Unit, bool> unitAttacked = new();
    private readonly Dictionary<Unit, bool> unitActive = new();
    private readonly HashSet<Unit> unitDead = new();
    private readonly Dictionary<Tile, Unit> tileOccupant = new();
    private readonly Dictionary<City, Player> cityOwner = new();
    private readonly Dictionary<City, Unit> pendingCityCaptures = new();

    private readonly List<Undo> undoLog = new();

    public static readonly BoardState Live = new();

    private enum UndoType
    {
        UnitTile,
        UnitHealth,
        UnitMoved,
        UnitAttacked,
        UnitActive,
        UnitDead,
        TileOccupant,
        CityOwner,
        PendingCapture
    }

    private struct Undo
    {
        public UndoType type;

        public Unit unit;
        public Tile tile;
        public City city;

        public Tile oldTile;
        public Unit oldUnit;
        public Player oldOwner;

        public int oldHealth;
        public bool oldBool;
        public bool wasPresent;
    }

    // ---------------------------------------------------------------------
    // Checkpoint / rollback
    // ---------------------------------------------------------------------

    public int Checkpoint()
    {
        return undoLog.Count;
    }

    public void Rollback(int checkpoint)
    {
        for (int i = undoLog.Count - 1; i >= checkpoint; i--)
        {
            Undo u = undoLog[i];

            switch (u.type)
            {
                case UndoType.UnitTile:
                    Restore(unitTile, u.unit, u.oldTile, u.wasPresent);
                    break;

                case UndoType.UnitHealth:
                    Restore(unitHealth, u.unit, u.oldHealth, u.wasPresent);
                    break;

                case UndoType.UnitMoved:
                    Restore(unitMoved, u.unit, u.oldBool, u.wasPresent);
                    break;

                case UndoType.UnitAttacked:
                    Restore(unitAttacked, u.unit, u.oldBool, u.wasPresent);
                    break;

                case UndoType.UnitActive:
                    Restore(unitActive, u.unit, u.oldBool, u.wasPresent);
                    break;

                case UndoType.UnitDead:
                    if (u.oldBool)
                        unitDead.Add(u.unit);
                    else
                        unitDead.Remove(u.unit);
                    break;

                case UndoType.TileOccupant:
                    Restore(tileOccupant, u.tile, u.oldUnit, u.wasPresent);
                    break;

                case UndoType.CityOwner:
                    Restore(cityOwner, u.city, u.oldOwner, u.wasPresent);
                    break;

                case UndoType.PendingCapture:
                    Restore(pendingCityCaptures, u.city, u.oldUnit, u.wasPresent);
                    break;
            }
        }

        undoLog.RemoveRange(checkpoint, undoLog.Count - checkpoint);
    }

    private static void Restore<TKey, TValue>(
        Dictionary<TKey, TValue> dictionary,
        TKey key,
        TValue oldValue,
        bool wasPresent)
    {
        if (wasPresent)
            dictionary[key] = oldValue;
        else
            dictionary.Remove(key);
    }

    // ---------------------------------------------------------------------
    // Readers
    // ---------------------------------------------------------------------

    public Tile GetTile(Unit unit) =>
        unitTile.TryGetValue(unit, out Tile tile)
            ? tile
            : unit.currentTile;

    public int GetHealth(Unit unit) =>
        unitHealth.TryGetValue(unit, out int health)
            ? health
            : unit.currentHealth;

    public bool HasMoved(Unit unit) =>
        unitMoved.TryGetValue(unit, out bool moved)
            ? moved
            : unit.hasMoved;

    public bool HasAttacked(Unit unit) =>
        unitAttacked.TryGetValue(unit, out bool attacked)
            ? attacked
            : unit.hasAttacked;

    public bool IsActive(Unit unit) =>
        unitActive.TryGetValue(unit, out bool active)
            ? active
            : unit.isActive;

    public bool IsAlive(Unit unit) =>
        !unitDead.Contains(unit) && unit.isAlive;

    public Player GetOwner(City city) =>
        cityOwner.TryGetValue(city, out Player owner)
            ? owner
            : city.owner;

    public bool HasPendingCityCapture(City city) =>
        pendingCityCaptures.ContainsKey(city);

    public Unit GetPendingCityCapturer(City city) =>
        pendingCityCaptures.TryGetValue(city, out Unit unit)
            ? unit
            : null;

    public Unit GetOccupant(Tile tile)
    {
        if (tileOccupant.TryGetValue(tile, out Unit occupant))
            return occupant;

        Unit live = tile.currentUnit;

        if (live == null)
            return null;

        if (unitTile.TryGetValue(live, out Tile movedTo) && movedTo != tile)
            return null;

        if (unitDead.Contains(live))
            return null;

        return live;
    }

    // ---------------------------------------------------------------------
    // Mutators
    // ---------------------------------------------------------------------

    public void WithMove(Unit unit, Tile from, Tile to)
    {
        SetTileOccupant(from, null);
        SetTileOccupant(to, unit);
        SetUnitTile(unit, to);
        SetUnitMoved(unit, true);

        RemovePendingCapturesForUnit(unit);

        if (HasSkill(unit, Skill.Static))
            SetUnitActive(unit, false);
    }

    public void WithPendingCityCapture(City city, Unit unit)
    {
        SetPendingCapture(city, unit);
    }

    public void WithoutPendingCityCapture(City city)
    {
        RemovePendingCapture(city);
    }

    public void WithCityClaim(City city, Player owner)
    {
        SetCityOwner(city, owner);
        RemovePendingCapture(city);
    }

    public void WithDamage(Unit unit, int newHealth)
    {
        SetUnitHealth(unit, newHealth);

        if (newHealth <= 0)
        {
            SetUnitDead(unit, true);
            SetTileOccupant(GetTile(unit), null);
            RemovePendingCapturesForUnit(unit);
        }
    }

    public void WithAttacked(Unit unit)
    {
        SetUnitAttacked(unit, true);
        SetUnitMoved(unit, true);
    }

    public void WithDeactivated(Unit unit)
    {
        SetUnitActive(unit, false);
    }

    // ---------------------------------------------------------------------
    // Setters + undo recording
    // ---------------------------------------------------------------------

    private void SetUnitTile(Unit unit, Tile value)
    {
        unitTile.TryGetValue(unit, out Tile oldValue);
        bool present = unitTile.ContainsKey(unit);

        if (present && oldValue == value)
            return;

        undoLog.Add(new Undo
        {
            type = UndoType.UnitTile,
            unit = unit,
            oldTile = oldValue,
            wasPresent = present
        });

        unitTile[unit] = value;
    }

    private void SetUnitHealth(Unit unit, int value)
    {
        bool present = unitHealth.TryGetValue(unit, out int oldValue);

        if (present && oldValue == value)
            return;

        undoLog.Add(new Undo
        {
            type = UndoType.UnitHealth,
            unit = unit,
            oldHealth = oldValue,
            wasPresent = present
        });

        unitHealth[unit] = value;
    }

    private void SetUnitMoved(Unit unit, bool value)
    {
        bool present = unitMoved.TryGetValue(unit, out bool oldValue);

        if (present && oldValue == value)
            return;

        undoLog.Add(new Undo
        {
            type = UndoType.UnitMoved,
            unit = unit,
            oldBool = oldValue,
            wasPresent = present
        });

        unitMoved[unit] = value;
    }

    private void SetUnitAttacked(Unit unit, bool value)
    {
        bool present = unitAttacked.TryGetValue(unit, out bool oldValue);

        if (present && oldValue == value)
            return;

        undoLog.Add(new Undo
        {
            type = UndoType.UnitAttacked,
            unit = unit,
            oldBool = oldValue,
            wasPresent = present
        });

        unitAttacked[unit] = value;
    }

    private void SetUnitActive(Unit unit, bool value)
    {
        bool present = unitActive.TryGetValue(unit, out bool oldValue);

        if (present && oldValue == value)
            return;

        undoLog.Add(new Undo
        {
            type = UndoType.UnitActive,
            unit = unit,
            oldBool = oldValue,
            wasPresent = present
        });

        unitActive[unit] = value;
    }

    private void SetUnitDead(Unit unit, bool value)
    {
        bool present = unitDead.Contains(unit);

        if (present == value)
            return;

        undoLog.Add(new Undo
        {
            type = UndoType.UnitDead,
            unit = unit,
            oldBool = present
        });

        if (value)
            unitDead.Add(unit);
        else
            unitDead.Remove(unit);
    }

    private void SetTileOccupant(Tile tile, Unit value)
    {
        bool present = tileOccupant.TryGetValue(tile, out Unit oldValue);

        if (present && oldValue == value)
            return;

        undoLog.Add(new Undo
        {
            type = UndoType.TileOccupant,
            tile = tile,
            oldUnit = oldValue,
            wasPresent = present
        });

        tileOccupant[tile] = value;
    }

    private void SetCityOwner(City city, Player value)
    {
        bool present = cityOwner.TryGetValue(city, out Player oldValue);

        if (present && oldValue == value)
            return;

        undoLog.Add(new Undo
        {
            type = UndoType.CityOwner,
            city = city,
            oldOwner = oldValue,
            wasPresent = present
        });

        cityOwner[city] = value;
    }

    private void SetPendingCapture(City city, Unit unit)
    {
        bool present = pendingCityCaptures.TryGetValue(city, out Unit oldValue);

        if (present && oldValue == unit)
            return;

        undoLog.Add(new Undo
        {
            type = UndoType.PendingCapture,
            city = city,
            oldUnit = oldValue,
            wasPresent = present
        });

        pendingCityCaptures[city] = unit;
    }

    private void RemovePendingCapture(City city)
    {
        if (!pendingCityCaptures.TryGetValue(city, out Unit oldValue))
            return;

        undoLog.Add(new Undo
        {
            type = UndoType.PendingCapture,
            city = city,
            oldUnit = oldValue,
            wasPresent = true
        });

        pendingCityCaptures.Remove(city);
    }

    private void RemovePendingCapturesForUnit(Unit unit)
    {
        foreach (City city in WorldPopulationManager.Instance.allCities)
        {
            if (city == null)
                continue;

            if (pendingCityCaptures.TryGetValue(city, out Unit capturer) &&
                capturer == unit)
            {
                RemovePendingCapture(city);
            }
        }
    }

    private static bool HasSkill(Unit unit, Skill skill)
    {
        if (unit.data.skills == null)
            return false;

        int skillsCount = unit.data.skills.Count();

        for (int i = 0; i < skillsCount; i++)
        {
            if (unit.data.skills[i] == skill)
                return true;
        }

        return false;
    }
}