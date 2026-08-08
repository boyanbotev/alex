using UnityEngine;

[CreateAssetMenu(fileName = "UnitData", menuName = "UnitData")]
public class UnitData : ScriptableObject
{
    public int cost = 2;
    public int maxHealth = 10;
    public int attackPower = 2;
    public  int defensePower = 2;
    public int moveRange = 1;
    public int attackRange = 1;
}
