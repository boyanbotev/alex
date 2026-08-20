using System.Collections.Generic;
using UnityEngine;

// Plain serializable class rather than a MonoBehaviour, so it can just be a
// field on your Player class regardless of whether Player itself is a
// MonoBehaviour or a plain object. See the note at the bottom of this file
// for the one line you need to add to Player.
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
        Debug.Log($"{player.factionName} researched {tech.techName}!");
        return true;
    }

    public bool CanBuild(BuildingData building)
    {
        return building.requiredTech == null || IsUnlocked(building.requiredTech);
    }
}
