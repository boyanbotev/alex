using System;
using System.Collections.Generic;

public enum DiplomaticRelation
{
    Peace,
    War,
    Allied
}

/// <summary>
/// Match relations, independent of ownership and action permissions.
/// Captures a fixed roster; indices remain stable if the source list is reordered.
/// Relation changes are intentionally a later step.
/// </summary>
public sealed class DiplomacyState
{
    private readonly Dictionary<Player, int> playerIndices;
    private readonly DiplomaticRelation[,] relations;

    public DiplomacyState(IReadOnlyList<Player> players)
    {
        if (players == null) throw new ArgumentNullException(nameof(players));

        playerIndices = new Dictionary<Player, int>(players.Count);
        relations = new DiplomaticRelation[players.Count, players.Count];

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null || playerIndices.ContainsKey(players[i]))
                throw new ArgumentException("Diplomacy requires unique, non-null players.", nameof(players));

            playerIndices.Add(players[i], i);
            for (int j = 0; j < players.Count; j++)
                relations[i, j] = i == j ? DiplomaticRelation.Peace : DiplomaticRelation.War;
        }
    }

    /// <summary>Self is Peace. Null/unowned entities have no diplomatic relation.</summary>
    public DiplomaticRelation GetRelation(Player a, Player b) =>
        relations[GetIndex(a), GetIndex(b)];

    /// <summary>Unowned entities are not enemies; claiming them is a separate rule.</summary>
    public bool IsAtWar(Player a, Player b) =>
        a != null && b != null && GetRelation(a, b) == DiplomaticRelation.War;

    private int GetIndex(Player player)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        if (!playerIndices.TryGetValue(player, out int index))
            throw new ArgumentException("Player is not in this match's diplomacy roster.", nameof(player));
        return index;
    }
}
