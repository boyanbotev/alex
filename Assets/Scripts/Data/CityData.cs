using UnityEngine;

[CreateAssetMenu(fileName = "City", menuName = "Game/City")]
public sealed class CityData : ScriptableObject
{
    public string cityName;
    public CityPerkData perk;
}
