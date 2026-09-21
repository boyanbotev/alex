using UnityEngine;

public enum CityPerkKind { Healing, Dopamine, Adrenaline }

[CreateAssetMenu(fileName = "City Perk", menuName = "Game/City Perk")]
public sealed class CityPerkData : ScriptableObject
{
    public string perkName;
    [TextArea] public string description;
    public Sprite icon;
    public CityPerkKind kind;
    [Min(0)] public int amount = 2;
    public string Summary => $"{(string.IsNullOrEmpty(perkName) ? kind.ToString() : perkName)} (+{amount})";
}
