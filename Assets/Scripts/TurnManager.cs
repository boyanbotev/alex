using UnityEngine;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public List<Player> players = new List<Player>();
    public int activePlayerIndex = 0;
    public int turnNumber = 1;

    public Player ActivePlayer => players[activePlayerIndex];

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
        // Add star income
        int income = player.CalculateTurnIncome();
        player.AddStars(income);

        // Reset units for the new active player
        foreach (var unit in player.units)
        {
            unit.ResetTurn();
        }

        Debug.Log($"Turn {turnNumber}: Start of {player.factionName}'s turn. Current Stars: {player.stars}");
    }
}