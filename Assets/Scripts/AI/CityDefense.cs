using UnityEngine;

// Conservative next-turn estimates shared by tactics and recruitment. These ignore
// spent action flags (which refresh), but respect static attackers and city perks.
public static class CityDefense
{
    public static bool CanThreaten(Unit attacker, Tile tile, BoardState board)
    {
        if (attacker == null || !board.IsAlive(attacker)) return false;
        UnitData data = board.GetData(attacker);
        int distance = Utils.GridDistance(board.GetTile(attacker).gridPosition, tile.gridPosition);
        int move = board.GetMoveRange(attacker);
        bool staticUnit = data.skills != null && System.Array.IndexOf(data.skills, Skill.Static) >= 0;
        return distance <= move || (data.attackPower > 0 &&
            distance <= data.attackRange + (staticUnit ? 0 : move));
    }

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
                if (!CanThreaten(attacker, tile, board) || board.GetData(attacker).attackPower <= 0) continue;
                UnitData data = board.GetData(attacker);
                if (data.skills != null && System.Array.IndexOf(data.skills, Skill.Static) >= 0 &&
                    Utils.GridDistance(board.GetTile(attacker).gridPosition, tile.gridPosition) > data.attackRange) continue;
                var damage = CombatMath.CalculateDamage(data.attackPower, board.GetHealth(attacker), data.maxHealth,
                    defense, health, defender.maxHealth);
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
