using System.Collections;
using UnityEngine;

public class TurnAI : MonoBehaviour
{
    [SerializeField] TacticsAI tacticsAI;
    [SerializeField] EconomyAI economyAI;
    [Tooltip("Fallback for factions without an AI profile.")]
    [SerializeField] AIProfile profile;

    public IEnumerator PlayTurn(Player player)
    {
        AIProfile turnProfile = player.faction != null && player.faction.aiProfile != null
            ? player.faction.aiProfile : profile;
        if (turnProfile == null)
        {
            Debug.LogError($"No AI profile assigned for {player.name} or TurnAI's fallback.", this);
            yield break;
        }

        yield return tacticsAI.PlayTurn(player, turnProfile);
        economyAI.HandleEconomy(player, turnProfile);
    }
}
