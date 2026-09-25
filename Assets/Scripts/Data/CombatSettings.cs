using UnityEngine;

[CreateAssetMenu(fileName = "CombatSettings", menuName = "Combat Settings")]
public class CombatSettings : ScriptableObject
{
    [Min(0)] public int cavalryKillerBonus = 2;
    [Min(0)] public int spearWallBonus = 1;
}
