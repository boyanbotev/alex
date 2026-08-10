using UnityEngine;

[CreateAssetMenu(fileName = "FactionUnit", menuName = "FactionUnit")]

public class FactionUnit : ScriptableObject
{
    public UnitData unitData;
    public Faction faction;
    public GameObject prefab;
}
