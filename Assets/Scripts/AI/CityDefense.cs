using UnityEngine;
using System.Collections.Generic;

// Conservative next-turn estimates shared by tactics and recruitment. These ignore
// spent action flags (which refresh), but respect static attackers and city perks.
// Attack estimates are conservative; walk-in threats use legal movement paths.
public static class CityDefense
{
    private static readonly List<Tile> reachable = new();
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
    public static bool CanReachCityNextTurn(Unit unit, Tile tile, BoardState board)
    {
        if (unit == null || !board.IsAlive(unit) || board.GetOccupant(tile) != null ||
            Utils.GridDistance(board.GetTile(unit).gridPosition, tile.gridPosition) > board.GetMoveRange(unit)) return false;
        GridManager.Instance.GetReachableMoveTiles(board.GetTile(unit), board.GetUnitOwner(unit), board.GetMoveRange(unit),
            board.GetOccupant, reachable, board.IsAtWar, board.GetUnitOwner);
        return reachable.Contains(tile);
    }

    public static int AssessGuard(City city, BoardState board)
    {
        Unit guard = board.GetOccupant(city.centerTile);
        return guard == null ? 0 : RemainingHealth(board.GetData(guard), board.GetHealth(guard),
            city.centerTile, board.GetOwner(city), board);
    }

    // Only an exposed city or a guard predicted to fall needs special defense.
    public static float Risk(City city, BoardState board)
    {
        Unit guard = board.GetOccupant(city.centerTile);
        if (guard == null) return CanEnemyEnter(city, board) ? 1f : 0f;
        if (board.GetUnitOwner(guard) != board.GetOwner(city)) return 1f;
        return AssessGuard(city, board) <= 0 ? 1f : 0f;
    }

    public static bool CanEnemyEnter(City city, BoardState board)
    {
        Tile tile = city.centerTile;
        if (board.GetOccupant(tile) != null) return false;
        Player owner = board.GetOwner(city);
        foreach (Player enemy in TurnManager.Instance.players)
        {
            if (!board.IsAtWar(owner, enemy)) continue;
            for (int i = 0; i < board.UnitCount(enemy); i++)
                if (CanReachCityNextTurn(board.UnitAt(enemy, i), tile, board)) return true;
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
                var damage = CombatMath.CalculateDamage(data, board.GetHealth(attacker), defender, health, defense,
                    TurnManager.Instance.combatSettings);
                health -= damage.attackDamage;
                if (health <= 0) return 0;
            }
        }
        return health;
    }

    public static float RecruitmentScore(FactionUnit recruit, City city, AIProfile profile)
    {
        if (!CanEnemyEnter(city, BoardState.Live)) return 0f;
        UnitData data = recruit.unitData;
        int remaining = RemainingHealth(data, data.maxHealth, city.centerTile, city.owner, BoardState.Live);
        return profile.cityCaptureWeight * (1f + (float)remaining / data.maxHealth);
    }
}
