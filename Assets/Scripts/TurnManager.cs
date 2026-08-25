using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        List<Player> alivePlayers = players.FindAll(p => p.IsAlive());
        if (alivePlayers.Count == 1)
        {
            Debug.Log("GAME OVER . " + alivePlayers[0].name + " is the victor");
            return;
        }

        HealUnusedUnits(ActivePlayer);

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

        ResolvePendingCaptures(player);

        if (player.isAI)
            StartCoroutine(RunAITurn(player));

        Debug.Log($"Turn {turnNumber}: Start of {player.factionName}'s turn. Current Stars: {player.stars}");
    }

    void HealUnusedUnits(Player player)
    {
        foreach (var unit in player.units)
        {
            if (!unit.hasMoved && !unit.hasAttacked)
            {
                unit.Heal();
            }
        }
    }

    private void ResolvePendingCaptures(Player player)
    {
        foreach (City city in WorldPopulationManager.Instance.allCities)
        {
            if (!city.HasPendingCapture)
                continue;

            if (city.pendingCapturer.owner != player)
                continue;

            if (player.isAI)
            {
                city.ResolvePendingCapture(false);
            }
            else
            {
                city.ResolvePendingCapture(true);
            }
        }
    }

    private IEnumerator RunAITurn(Player player)
    {
        yield return ai.PlayTurn(player);
        EndTurn();
    }
}