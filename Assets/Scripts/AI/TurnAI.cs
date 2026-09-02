using System.Collections;
using UnityEngine;

public class TurnAI : MonoBehaviour
{
    [SerializeField] TacticsAI tacticsAI;
    [SerializeField] EconomyAI economyAI;
    [SerializeField] AIProfile profile;

    public IEnumerator PlayTurn(Player player)
    {
        yield return tacticsAI.PlayTurn(player, profile);
        economyAI.HandleEconomy(player, profile);
    }
}
