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

    [Header("Animation")]

    [SerializeField] private Renderer render;

    private void Start()
    {
        currentHealth = data.maxHealth;
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
        (int, int) damages = CalculateDamage(this, defender);
        int attackDamage = damages.Item1;
        int retaliationDamage = damages.Item2;

        defender.TakeDamage(damages.Item1);

        Debug.Log("attacker damage" + damages.Item1);

        if (defender.currentHealth > 0 && defender.data.attackRange >= data.attackRange)
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
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private (int, int) CalculateDamage(Unit attacker, Unit defender)
    {
        int attackDamage;
        int defenseDamage;
        float attackForce = attacker.data.attackPower * ((float)attacker.currentHealth / attacker.data.maxHealth);
        float defenseForce = defender.data.defensePower * ((float)defender.currentHealth / defender.data.maxHealth);
        float totalForce = attackForce + defenseForce;

        float rawDamage = (attackForce / totalForce) * attacker.data.attackPower * 4.5f;
        attackDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage));

        float rawDefence = (defenseForce / totalForce) * defender.data.defensePower * 4.5f;
        defenseDamage = Mathf.Max(1, Mathf.RoundToInt(rawDefence));

        return (attackDamage, defenseDamage);
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