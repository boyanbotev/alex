using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Tutorial", menuName = "Game/Tutorial")]
public sealed class Tutorial : ScriptableObject
{
    public TutorialStage[] stages = Array.Empty<TutorialStage>();
}

public enum TutorialCondition { OwnedCities, ConnectAllCities, StrongBonds, RecruitUnit, KillEnemyUnit, Instruction }

[Serializable]
public sealed class TutorialStage
{
    [TextArea(2, 6)] public string instruction;
    public TutorialCondition condition;
    public UnitData unitType;
    [Min(1)] public int targetCount = 1;

    public bool IsSatisfied(Player player, TurnManager turns)
    {
        switch (condition)
        {
            case TutorialCondition.OwnedCities:
                return player.cities.Count >= targetCount;
            case TutorialCondition.ConnectAllCities:
                if (player.cities.Count < 2) return false;
                for (int i = 1; i < player.cities.Count; i++)
                    if (!turns.Neurons.AreConnected(player.cities[0], player.cities[i])) return false;
                return true;
            case TutorialCondition.StrongBonds:
                int count = 0;
                foreach (CityBond bond in turns.Bonds.All)
                    if (bond.Active && (bond.a.owner == player || bond.b.owner == player)) count++;
                return count >= targetCount;
            default:
                return false;
        }
    }
}
