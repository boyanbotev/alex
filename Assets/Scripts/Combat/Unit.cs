using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    [Header("Unit Profile")]
    public string unitName;
    public Player owner;
    public Tile currentTile;
    public City homeCity;
    [Min(0)] public int dopamineBonus;

    [Header("Base Stats")]
    public int currentHealth;
    public UnitData data;

    [Header("State")]
    public bool hasMoved;
    public bool hasAttacked;
    public bool isAlive;
    public bool isActive;
    public bool hasCaptured;
    private readonly List<Unit> splashTargets = new();
    private static readonly List<Tile> moveTiles = new(64);
    private static readonly System.Func<Tile, Unit> GetLiveOccupant = tile => tile.currentUnit;
    private bool CanAct => isAlive && isActive && data != null && owner != null &&
        TurnManager.Instance != null && TurnManager.Instance.ActivePlayer == owner &&
        currentTile != null && currentTile.currentUnit == this;
    private bool IsStatic => data.skills != null && System.Array.IndexOf(data.skills, Skill.Static) >= 0;

    [Header("Animation")]

    [SerializeField] private Renderer render;
    [SerializeField] HealthUI healthUI;

    private void Start()
    {
        currentHealth = data.maxHealth;
        healthUI.Set(currentHealth);
        isAlive = true;
    }

    public bool CanMoveTo(Tile targetTile)
    {
        if (!CanAct || hasMoved || hasAttacked || targetTile == null || targetTile == currentTile ||
            targetTile.currentUnit != null || targetTile.terrainType == TerrainType.Mountain) return false;
        GridManager.Instance.GetReachableMoveTiles(currentTile, owner, BoardState.Live.GetMoveRange(this),
            GetLiveOccupant, moveTiles);
        bool reachable = moveTiles.Contains(targetTile);
        moveTiles.Clear();
        return reachable;
    }

    public bool MoveTo(Tile targetTile)
    {
        if (!CanMoveTo(targetTile)) return false;
        ApplyMove(targetTile);
        return true;
    }

    // Also used for melee advancement, which is part of the attack rather than a second move.
    private void ApplyMove(Tile targetTile)
    {
        if (currentTile.city != null && currentTile.city.pendingCapturer == this)
            currentTile.city.ClearPendingCapture();
        currentTile.currentUnit = null;

        currentTile = targetTile;
        targetTile.currentUnit = this;
        transform.position = targetTile.transform.position;

        hasMoved = true;

        FogOfWarManager.Instance.Reveal(owner, targetTile, 1); // unit sight range

        if (targetTile.city != null && InteractionRules.CanCapture(owner, targetTile.city.owner))
        {
            targetTile.city.SetPendingCapture(this);
        }

        if (IsStatic)
        {
            isActive = false;
        }
    }

    public bool CanAttack(Unit defender, Tile from = null)
    {
        from = from ?? currentTile;
        return CanAct && !hasAttacked && defender != null && defender.isAlive &&
            defender.currentTile != null && defender.currentTile.currentUnit == defender &&
            InteractionRules.CanAttack(owner, defender.owner) &&
            Utils.IsWithinDistance(from.gridPosition, defender.currentTile.gridPosition, data.attackRange) &&
            (from == currentTile || (!IsStatic && CanMoveTo(from)));
    }

    public bool Attack(Unit defender, Tile from = null)
    {
        if (!CanAttack(defender, from)) return false;
        if (from != null && from != currentTile) ApplyMove(from);

        Tile targetTile = defender.currentTile;
        var (attackDamage, retaliationDamage, advance) = CombatMath.PredictAttack(this, defender, BoardState.Live);

        CombatMath.CollectSplashTargets(this, defender, defender.currentTile, BoardState.Live, splashTargets);
        defender.TakeDamage(attackDamage);
        foreach (Unit target in splashTargets) target.TakeDamage(data.splashDamage);
        splashTargets.Clear();

        if (retaliationDamage > 0)
        {
            TakeDamage(retaliationDamage);
        }

        hasAttacked = true;
        hasMoved = true;
        if (advance) ApplyMove(targetTile);
        return true;
    }

    public bool TryReplaceGarrison(City city, FactionUnit recruit, Tile destination)
    {
        if (city == null || city.owner != owner || currentTile != city.centerTile ||
            recruit == null || recruit.unitData == null || destination == null || destination.city != null ||
            !city.CanSpawnUnit(recruit, recruit.unitData.cost, this) || !CanMoveTo(destination)) return false;

        ApplyMove(destination);
        if (city.SpawnUnit(recruit, recruit.unitData.cost)) return true;

        // Restore occupancy and action state if recruitment fails after vacating.
        ApplyMove(city.centerTile);
        hasMoved = false;
        isActive = true;
        return false;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        healthUI?.Set(currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public bool CanSeverNeuron(Building segment, Tile from = null)
    {
        from = from ?? currentTile;
        return CanAct && BoardState.Live.CanSeverNeuron(this, from, segment) &&
            (from == currentTile || CanMoveTo(from));
    }

    public bool TrySeverNeuron(Building segment, Tile from = null)
    {
        if (!CanSeverNeuron(segment, from)) return false;
        if (from != null && from != currentTile) ApplyMove(from);
        hasAttacked = true;
        // Match the existing attack rule: attacking also consumes movement.
        hasMoved = true;
        TurnManager.Instance.Diplomacy.DeclareWar(owner, segment.owner);
        segment.RemoveNeuron(owner);
        if (dopamineBonus > 0) owner.AddStars(dopamineBonus);
        Deactivate();
        return true;
    }

    public void Heal()
    {
        if (hasAttacked || hasMoved || hasCaptured) return;

        City city = currentTile.territoryCity ?? currentTile.city;
        int healthRecoup = 2;
        if (city != null && TurnManager.Instance.Bonds.Friendly(owner, city.owner))
            healthRecoup += city.PerkAmount(CityPerkKind.Healing);

        currentHealth = Mathf.Min(data.maxHealth, currentHealth + healthRecoup);
        healthUI?.Set(currentHealth);
    }

    public (int damage, int retaliation) PredictAttackDamage(Unit defender)
    {
        var (damage, retaliation, _) = CombatMath.PredictAttack(this, defender, BoardState.Live);
        return (damage, retaliation);
    }

    private void Die()
    {
        isAlive = false;
        if (currentTile != null)
        {
            if (currentTile.city && currentTile.city.pendingCapturer != null)
            {
                currentTile.city.ClearPendingCapture();
            }

            currentTile.currentUnit = null;
        }

        if (owner != null) owner.units.Remove(this);
        if (homeCity != null) homeCity.units.Remove(this);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }

    public void ResetTurn()
    {
        hasMoved = false;
        hasAttacked = false;
        hasCaptured = false;
        Activate();
    }

    public void Activate()
    {
        Color color = render.material.color;
        color.a = 1f;
        render.material.color = color;
        isActive = true;
    }

    public void Deactivate()
    {
        if (render != null)
        {
            Color color = render.material.color;
            color.a = 0.7f;
            render.material.color = color;
        }
        isActive = false;
    }
}
