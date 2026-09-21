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
/// </summary>
public sealed class DiplomacyState
{
    private readonly Dictionary<Player, int> playerIndices;
    private readonly DiplomaticRelation[,] relations;
    public event Action<Player, Player, DiplomaticRelation> RelationChanged;
    public int Revision { get; private set; }

    public bool DeclareWar(Player a, Player b) => SetRelation(a, b, DiplomaticRelation.War);

    // Applies agreed peace. Negotiation/acceptance belongs to the caller.
    public bool MakePeace(Player a, Player b) => SetRelation(a, b, DiplomaticRelation.Peace);
    public bool MakeAlliance(Player a, Player b) => SetRelation(a, b, DiplomaticRelation.Allied);

    private bool SetRelation(Player a, Player b, DiplomaticRelation relation)
    {
        int i = GetIndex(a);
        int j = GetIndex(b);
        if (i == j) throw new ArgumentException("A player cannot change relations with itself.");
        if (relations[i, j] == relation) return false;
        relations[i, j] = relations[j, i] = relation;
        Revision++;
        RelationChanged?.Invoke(a, b, relation);
        return true;
    }

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
