using UnityEngine;

[CreateAssetMenu(fileName = "NewTech", menuName = "Tech/Tech Data")]
public class TechData : ScriptableObject
{
    public string techName;
    [TextArea] public string description;

    public int cost;

    [Tooltip("Techs that must already be unlocked before this one can be researched.")]
    public TechData[] prerequisites;

    public bool PrerequisitesMet(PlayerTechState techState)
    {
        if (prerequisites == null) return true;

        foreach (TechData prereq in prerequisites)
        {
            if (prereq != null && !techState.IsUnlocked(prereq))
            {
                return false;
            }
        }

        return true;
    }
}
