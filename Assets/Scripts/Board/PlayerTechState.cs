using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerTechState
{
    private readonly HashSet<TechData> unlockedTechs = new HashSet<TechData>();

    public bool IsUnlocked(TechData tech)
    {
        return tech != null && unlockedTechs.Contains(tech);
    }

    public bool CanResearch(TechData tech)
    {
        if (tech == null || IsUnlocked(tech)) return false;
        return tech.PrerequisitesMet(this);
    }

    public bool TryResearch(TechData tech, Player player)
    {
        if (!CanResearch(tech)) return false;
        if (!player.SpendStars(tech.cost)) return false;

        unlockedTechs.Add(tech);
        return true;
    }

    public bool CanBuild(BuildingData building)
    {
        return building.requiredTech == null || IsUnlocked(building.requiredTech);
    }

    public bool CanSpawn(UnitData unit)
    {
        return unit.requiredTech == null || IsUnlocked(unit.requiredTech);
    }
}
