using UnityEngine;

public enum CityPerkKind { None = -1, Healing = 0, Dopamine = 1, Adrenaline = 2, Fortification = 3 }

[CreateAssetMenu(fileName = "City Perk", menuName = "Game/City Perk")]
public sealed class CityPerkData : ScriptableObject
{
    public string perkName;
    [TextArea] public string description;
    public Sprite icon;
    public string IconTag => icon != null ? $"<sprite=\"CityPerk{kind}\" index=0> " : "";
    public CityPerkKind kind;
    [Min(0)] public int amount = 2;
    public string Summary => $"{(string.IsNullOrEmpty(perkName) ? kind.ToString() : perkName)} (+{amount})";
}
