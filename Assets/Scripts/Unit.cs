using UnityEngine;

public class Unit : MonoBehaviour
{
    [Header("Unit Profile")]
    public string unitName;
    public Player owner;
    public Tile currentTile;
    public City homeCity;

    [Header("Base Stats")]
    public int maxHealth = 10;
    public int currentHealth;
    public int attackPower = 2;
    public int defensePower = 2;
    public int moveRange = 1;
    public int attackRange = 1;

    [Header("State")]
    public bool hasMoved;
    public bool hasAttacked;
    public bool isAlive;

    [Header("Animation")]

    [SerializeField] private Renderer render;

    private void Start()
    {
        currentHealth = maxHealth;
        isAlive = true;
    }

    public void MoveTo(Tile targetTile)
    {
        if (currentTile != null) currentTile.currentUnit = null;

        currentTile = targetTile;
        targetTile.currentUnit = this;
        transform.position = targetTile.transform.position; // Add smoothing/animation here if desired

        hasMoved = true;
    }

    public void Attack(Unit defender)
    {
        if (hasAttacked) return;

        // Calculate and inflict damage to defender
        int attackerDamage = CalculateDamage(this, defender);
        defender.TakeDamage(attackerDamage);

        // Counterattack / Retaliation if defender survives and is within attack range
        if (defender.currentHealth > 0 && defender.attackRange >= attackRange)
        {
            int retaliationDamage = CalculateDamage(defender, this);
            TakeDamage(retaliationDamage);
        }

        hasAttacked = true;
        hasMoved = true;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private int CalculateDamage(Unit attacker, Unit defender)
    {
        // Polytopia Combat Formula:
        // Attack Force = Attacker Attack * (Attacker Current HP / Max HP)
        // Defense Force = Defender Defense * (Defender Current HP / Max HP)
        // Damage = (Attack Force / (Attack Force + Defense Force)) * Attacker Attack * 4.5
        float attackForce = attacker.attackPower * ((float)attacker.currentHealth / attacker.maxHealth);
        float defenseForce = defender.defensePower * ((float)defender.currentHealth / defender.maxHealth);
        float totalForce = attackForce + defenseForce;

        if (totalForce == 0) return 0;

        float rawDamage = (attackForce / totalForce) * attacker.attackPower * 4.5f;
        return Mathf.Max(1, Mathf.RoundToInt(rawDamage));
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
    }

    public void Deactivate()
    {
        Color color = render.material.color;
        color.a = 0.7f;
        render.material.color = color;
    }
}