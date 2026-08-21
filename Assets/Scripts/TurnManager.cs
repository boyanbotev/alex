using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public List<Player> players = new List<Player>();
    public int activePlayerIndex = 0;
    public int turnNumber = 1;

    public Player ActivePlayer => players[activePlayerIndex];
    public TurnAI ai;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        StartTurn(ActivePlayer);
    }

    public void EndTurn()
    {
        activePlayerIndex = (activePlayerIndex + 1) % players.Count;

        if (activePlayerIndex == 0)
        {
            turnNumber++;
        }

        StartTurn(ActivePlayer);
    }

    private void StartTurn(Player player)
    {
        int income = player.CalculateTurnIncome();
        player.AddStars(income);

        foreach (var unit in player.units)
            unit.ResetTurn();

        if (player.isAI)
            StartCoroutine(RunAITurn(player));

        Debug.Log($"Turn {turnNumber}: Start of {player.factionName}'s turn. Current Stars: {player.stars}");
    }

    private IEnumerator RunAITurn(Player player)
    {
        yield return ai.PlayTurn(player);
        EndTurn();
    }
}