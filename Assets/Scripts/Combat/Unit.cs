using System.Linq;
using UnityEngine;

public class Unit : MonoBehaviour
{
    [Header("Unit Profile")]
    public string unitName;
    public Player owner;
    public Tile currentTile;
    public City homeCity;

    [Header("Base Stats")]
    public int currentHealth;
    public UnitData data;

    [Header("State")]
    public bool hasMoved;
    public bool hasAttacked;
    public bool isAlive;
    public bool isActive;

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
        if (!isActive) return;
        if (currentTile != null) currentTile.currentUnit = null;

        currentTile = targetTile;
        targetTile.currentUnit = this;
        transform.position = targetTile.transform.position;

        hasMoved = true;
        if (data.skills.Any(s => s == Skill.Static))
        {
            isActive = false;
        }
    }

    public void Attack(Unit defender)
    {
        if (hasAttacked || !isActive) return;

        (int, int) damages = CalculateDamage(this, defender);
        int attackDamage = damages.Item1;
        int retaliationDamage = damages.Item2;

        defender.TakeDamage(attackDamage);

        Debug.Log("attacker damage" + attackDamage);

        if (defender.currentHealth > 0 && Utils.IsWithinDistance(defender.currentTile.gridPosition, currentTile.gridPosition, defender.data.attackRange))
        {
            TakeDamage(retaliationDamage);
            Debug.Log("retaliation damage " + retaliationDamage);
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

    public (int, int) CalculateDamage(Unit attacker, Unit defender)
    {
        return CombatMath.CalculateDamage(
            attacker.data.attackPower, attacker.currentHealth, attacker.data.maxHealth,
            defender.data.defensePower, defender.currentHealth, defender.data.maxHealth
        );
    }

    private void Die()
    {
        isAlive = false;
        if (currentTile != null) currentTile.currentUnit = null;
        if (owner != null) owner.units.Remove(this);
        if (homeCity != null) homeCity.units.Remove(this);
        Destroy(gameObject);
    }

    public void ResetTurn()
    {
        hasMoved = false;
        hasAttacked = false;
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
        Color color = render.material.color;
        color.a = 0.7f;
        render.material.color = color;
        isActive = false;
    }
}
