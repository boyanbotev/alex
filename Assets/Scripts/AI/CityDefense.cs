using UnityEngine;

// Conservative next-turn estimates shared by tactics and recruitment. These ignore
// spent action flags (which refresh), but respect static attackers and city perks.
// Distance estimates intentionally ignore path obstacles and occupancy.
public static class CityDefense
{
    public static bool CanAttackNextTurn(Unit attacker, Tile tile, BoardState board)
    {
        if (attacker == null || !board.IsAlive(attacker)) return false;
        UnitData data = board.GetData(attacker);
        if (data.attackPower <= 0) return false;
        int distance = Utils.GridDistance(board.GetTile(attacker).gridPosition, tile.gridPosition);
        bool staticUnit = data.skills != null && System.Array.IndexOf(data.skills, Skill.Static) >= 0;
        return distance <= data.attackRange + (staticUnit ? 0 : board.GetMoveRange(attacker));
    }

    // Reaching an undefended city does not require attacking, even for static units.
    public static bool CanReachCityNextTurn(Unit unit, Tile tile, BoardState board) =>
        unit != null && board.IsAlive(unit) &&
        Utils.GridDistance(board.GetTile(unit).gridPosition, tile.gridPosition) <= board.GetMoveRange(unit);

    public static bool CanThreaten(Unit attacker, Tile tile, BoardState board) =>
        CanAttackNextTurn(attacker, tile, board) || CanReachCityNextTurn(attacker, tile, board);

    public static bool IsThreatened(Tile tile, Player owner, BoardState board)
    {
        foreach (Player enemy in TurnManager.Instance.players)
        {
            if (!board.IsAtWar(owner, enemy)) continue;
            for (int i = 0; i < board.UnitCount(enemy); i++)
                if (CanThreaten(board.UnitAt(enemy, i), tile, board)) return true;
        }
        return false;
    }

    public static int RemainingHealth(UnitData defender, int health, Tile tile, Player owner, BoardState board)
    {
        City territory = tile.territoryCity ?? tile.city;
        int defense = defender.defensePower;
        if (territory != null && !board.IsAtWar(owner, board.GetOwner(territory)) &&
            TurnManager.Instance.Bonds.Friendly(owner, board.GetOwner(territory)))
            defense += territory.PerkAmount(CityPerkKind.Fortification);
        foreach (Player enemy in TurnManager.Instance.players)
        {
            if (!board.IsAtWar(owner, enemy)) continue;
            for (int i = 0; i < board.UnitCount(enemy); i++)
            {
                Unit attacker = board.UnitAt(enemy, i);
                if (!CanAttackNextTurn(attacker, tile, board)) continue;
                UnitData data = board.GetData(attacker);
                var damage = CombatMath.CalculateDamage(data, board.GetHealth(attacker), defender, health, defense);
                health -= damage.attackDamage;
                if (health <= 0) return 0;
            }
        }
        return health;
    }

    public static float RecruitmentScore(FactionUnit recruit, City city, AIProfile profile)
    {
        if (!IsThreatened(city.centerTile, city.owner, BoardState.Live)) return 0f;
        UnitData data = recruit.unitData;
        int remaining = RemainingHealth(data, data.maxHealth, city.centerTile, city.owner, BoardState.Live);
        return profile.cityCaptureWeight * (1f + (float)remaining / data.maxHealth);
    }
}
