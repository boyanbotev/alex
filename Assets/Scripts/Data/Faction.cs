using UnityEngine;

[CreateAssetMenu(fileName = "Faction", menuName = "Faction")]
public class Faction : ScriptableObject
{
    [Tooltip("Used for this faction's AI tactics and economy. If unset, uses TurnAI's fallback profile.")]
    public AIProfile aiProfile;
    public GameObject cityPrefab;
    public FactionUnit[] units = System.Array.Empty<FactionUnit>();
    public FactionUnit startingUnit;
    public FactionUnit[] availableUnits = System.Array.Empty<FactionUnit>();
    public BuildingData[] availableBuildings = System.Array.Empty<BuildingData>();
}
