using System.Collections.Generic;
using System.Linq;
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

    [Header("Animation")]

    [SerializeField] private Renderer render;
    [SerializeField] HealthUI healthUI;

    private void Start()
    {
        currentHealth = data.maxHealth;
        healthUI.Set(currentHealth);
        isAlive = true;
    }

    public void MoveTo(Tile targetTile)
    {
        if (!isActive || targetTile == null || targetTile.terrainType == TerrainType.Mountain) return;
        if (currentTile != null) currentTile.currentUnit = null;

        currentTile = targetTile;
        targetTile.currentUnit = this;
        transform.position = targetTile.transform.position;

        hasMoved = true;

        FogOfWarManager.Instance.Reveal(owner, targetTile, 1); // unit sight range

        if (targetTile.city != null && InteractionRules.CanCapture(owner, targetTile.city.owner))
        {
            targetTile.city.SetPendingCapture(this);
        }

        if (data.skills.Any(s => s == Skill.Static))
        {
            isActive = false;
        }
    }

    public void Attack(Unit defender)
    {
        if (hasAttacked || !isActive || defender == null || !defender.isAlive
            || !InteractionRules.CanAttack(owner, defender.owner)) return;

        (int, int) damages = PredictAttackDamage(defender);
        int attackDamage = damages.Item1;
        int retaliationDamage = damages.Item2;

        defender.TakeDamage(attackDamage);

        if (retaliationDamage > 0)
        {
            TakeDamage(retaliationDamage);
        }

        hasAttacked = true;
        hasMoved = true;
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

    public bool CanSeverNeuron(Building segment)
    {
        if (!isAlive || !isActive || hasAttacked || data == null || data.attackPower <= 0 ||
            owner == null || TurnManager.Instance == null || TurnManager.Instance.ActivePlayer != owner ||
            segment == null || !segment.IsPlacedNeuron || segment.owner == null || segment.owner == owner ||
            currentTile != segment.tile || currentTile.currentUnit != this ||
            owner.visibleTiles == null || !owner.visibleTiles.IsVisible(currentTile)) return false;
        return TurnManager.Instance.Diplomacy.GetRelation(owner, segment.owner) != DiplomaticRelation.Allied;
    }

    public bool TrySeverNeuron(Building segment)
    {
        if (!CanSeverNeuron(segment)) return false;
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

    public (int, int) CalculateDamage(Unit attacker, Unit defender)
    {
        return CombatMath.CalculateDamage(
            attacker.data.attackPower, attacker.currentHealth, attacker.data.maxHealth,
            defender.data.defensePower, defender.currentHealth, defender.data.maxHealth
        );
    }

    public (int damage, int retaliation) PredictAttackDamage(Unit defender)
    {
        var (damage, retaliation) = CalculateDamage(this, defender);
        if (damage >= defender.currentHealth || !Utils.IsWithinDistance(
            defender.currentTile.gridPosition, currentTile.gridPosition, defender.data.attackRange))
            retaliation = 0;
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
        Destroy(gameObject);
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
