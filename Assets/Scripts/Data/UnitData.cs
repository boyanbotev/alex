using UnityEngine;

public enum Skill
{
    Static
}

[CreateAssetMenu(fileName = "UnitData", menuName = "UnitData")]
public class UnitData : ScriptableObject
{
    [Tooltip("Original unit type for counter matching when these are faction-specific stats. Leave empty for a base unit.")]
    public UnitData counterType;
    public UnitData CounterType => counterType != null ? counterType : this;
    public int cost = 2;
    public int maxHealth = 10;
    public int attackPower = 2;
    public  int defensePower = 2;
    public int moveRange = 1;
    public int attackRange = 1;
    public Skill[] skills = System.Array.Empty<Skill>();
    public Counter[] counters = System.Array.Empty<Counter>();
    public TechData requiredTech;
}
