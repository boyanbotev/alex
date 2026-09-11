/// <summary>
/// Ownership and diplomatic permissions shared by live gameplay and AI simulation.
/// Callers still check range, visibility, occupancy and action availability.
/// Pass simulated owners when evaluating a hypothetical board.
/// </summary>
public static class InteractionRules
{
    public static bool CanAttack(Player attacker, Player defender) =>
        TurnManager.Instance.Diplomacy.IsAtWar(attacker, defender);

    public static bool CanCapture(Player capturer, Player cityOwner) =>
        capturer != null && (cityOwner == null || CanAttack(capturer, cityOwner));

    // Passage through occupied tiles is currently restricted to your own units.
    // Peace/alliance does not yet grant passage; no unit can end on an occupied tile.
    public static bool CanPassThrough(Player mover, Player occupant) =>
        mover != null && mover == occupant;
}
