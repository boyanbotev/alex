using NUnit.Framework;
using UnityEngine;

[CreateAssetMenu(fileName = "Faction", menuName = "Faction")]
public class Faction : ScriptableObject
{
    public GameObject cityPrefab;
    public FactionUnit[] units;
    public FactionUnit startingUnit;
    public FactionUnit[] availableUnits;
}

