using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GarrisonReplacementTests : TacticsTestFixture
{
    private City city;
    private Unit wounded;
    private Unit attacker;
    private FactionUnit recruit;
    private GarrisonReplacementPlanner planner;

    private void SetupDefense()
    {
        player.isAI = true;
        player.faction = Asset<Faction>();
        player.visibleTiles = new VisibilityState(20, 20);
        city = City(Tile(3, 3), player);
        player.cities.Add(city);
        wounded = Unit(player, city.centerTile);
        wounded.currentHealth = 2;
        wounded.data.moveRange = 2;
        wounded.homeCity = city;
        city.units.Add(wounded);
        Tile(2, 3); Tile(1, 3);
        attacker = Unit(enemy, Tile(4, 3));
        attacker.hasMoved = attacker.hasAttacked = true;
        attacker.isActive = false;
        recruit = Asset<FactionUnit>();
        recruit.unitData = Asset<UnitData>();
        recruit.unitData.defensePower = 4;
        recruit.unitData.cost = 3;
        recruit.prefab = Component<Unit>().gameObject;
        player.faction.availableUnits = new[] { recruit };
        planner = new GarrisonReplacementPlanner();
        profile.ownRolloutSteps = profile.enemyRolloutSteps = 1;
    }

    [Test]
    public void ReplacementCompetesWithHoldingEvenWithOneCandidatePerUnit()
    {
        SetupDefense();
        Assert.That(planner.TryPlan(player, profile, scorer, out var action), Is.True);
        Assert.That(action.moveTile.gridPosition, Is.EqualTo(new Vector2Int(1, 3)));
        Assert.That(action.recruit, Is.SameAs(recruit));
        var hold = new CandidateAction { unit = wounded, moveTile = city.centerTile, kind = ActionKind.DoNothing,
            score = scorer.ScoreMove(wounded, city.centerTile, city.centerTile, board) };
        Assert.That((float)Call(ai, "EvaluateWithLookahead", action, board),
            Is.GreaterThan((float)Call(ai, "EvaluateWithLookahead", hold, board)));
        Assert.That(board.Recruit, Is.Null);
        Assert.That(city.centerTile.currentUnit, Is.SameAs(wounded));
    }

    [TestCase("money")]
    [TestCase("capacity")]
    [TestCase("moved")]
    [TestCase("inactive")]
    [TestCase("blocked")]
    [TestCase("weaker")]
    [TestCase("healthy")]
    [TestCase("peace")]
    [TestCase("perk")]
    [TestCase("prefab")]
    public void InfeasibleOrUnhelpfulReplacementIsRejected(string reason)
    {
        SetupDefense();
        switch (reason)
        {
            case "money": player.stars = 2; break;
            case "capacity": city.units.Add(Unit(player, Tile(10, 3))); break;
            case "moved": wounded.hasMoved = true; break;
            case "inactive": wounded.isActive = false; break;
            case "blocked": grid.GetTileAt(new Vector2Int(2, 3)).terrainType = TerrainType.Mountain; break;
            case "weaker": recruit.unitData.defensePower = 0; recruit.unitData.maxHealth = 1; break;
            case "healthy": wounded.currentHealth = wounded.data.maxHealth; break;
            case "peace": turns.Diplomacy.MakePeace(player, enemy); break;
            case "perk": recruit.unitData.requiredPerk = CityPerkKind.Fortification; break;
            case "prefab": recruit.prefab = null; break;
        }
        Assert.That(planner.TryPlan(player, profile, scorer, out _), Is.False);
    }

    [Test]
    public void SimulatedRecruitIsAttackableInactiveAndFullyRolledBack()
    {
        SetupDefense();
        Assert.That(planner.TryPlan(player, profile, scorer, out var action), Is.True);
        int checkpoint = board.Checkpoint();
        ActionSimulator.Apply(board, action);
        Unit simulated = board.GetOccupant(city.centerTile);
        Assert.That(board.GetData(simulated), Is.SameAs(recruit.unitData));
        Assert.That(board.GetUnitOwner(simulated), Is.SameAs(player));
        Assert.That(board.IsActive(simulated), Is.False);
        Assert.That(board.HasMoved(simulated) && board.HasAttacked(simulated), Is.True);
        Assert.That(board.UnitAt(player, board.UnitCount(player) - 1), Is.SameAs(simulated));
        board.WithFreshTurn(enemy);
        generator.Generate(enemy, board);
        Assert.That(new List<CandidateAction>(generator.Candidates).Exists(a => a.target == simulated), Is.True);
        int expected = ActionSimulator.PredictDamage(attacker, simulated, board).Item1;
        ActionSimulator.Apply(board, new CandidateAction { unit = attacker, moveTile = attacker.currentTile,
            kind = ActionKind.Attack, target = simulated });
        Assert.That(board.GetHealth(simulated), Is.EqualTo(recruit.unitData.maxHealth - expected));
        Assert.That(recruit.prefab.GetComponent<Unit>().data, Is.Null);
        Assert.That(recruit.prefab.GetComponent<Unit>().owner, Is.Null);
        Assert.That(player.stars, Is.EqualTo(5));
        Assert.That(player.units.Count, Is.EqualTo(1));
        board.Rollback(checkpoint);
        Assert.That(board.Recruit, Is.Null);
        Assert.That(board.GetOccupant(city.centerTile), Is.SameAs(wounded));
        Assert.That(board.GetTile(wounded), Is.SameAs(city.centerTile));
    }

    [Test]
    public void KillingSimulatedRecruitAllowsMeleeCaptureAndRollback()
    {
        SetupDefense();
        planner.TryPlan(player, profile, scorer, out var action);
        ActionSimulator.Apply(board, action);
        Unit simulated = board.Recruit;
        board.WithDamage(simulated, 1);
        ActionSimulator.Apply(board, new CandidateAction { unit = attacker, moveTile = attacker.currentTile,
            kind = ActionKind.Attack, target = simulated });
        Assert.That(board.IsAlive(simulated), Is.False);
        Assert.That(board.GetOccupant(city.centerTile), Is.SameAs(attacker));
        Assert.That(board.GetPendingCityCapturer(city), Is.SameAs(attacker));
        board.Rollback(0);
        Assert.That(board.GetOccupant(city.centerTile), Is.SameAs(wounded));
    }

    [Test]
    public void ExecutionRevalidatesFundsBeforeMoving()
    {
        SetupDefense();
        planner.TryPlan(player, profile, scorer, out var action);
        player.stars = 0;
        var execution = (IEnumerator)Call(ai, "Execute", action, turns.Diplomacy.Revision);
        Assert.That(execution.MoveNext(), Is.False);
        Assert.That(city.centerTile.currentUnit, Is.SameAs(wounded));
        Assert.That(wounded.hasMoved, Is.False);
    }

    [Test]
    public void SimulatedRecruitBlocksEnemyMovementAndTakesSplashDamage()
    {
        SetupDefense();
        planner.TryPlan(player, profile, scorer, out var action);
        ActionSimulator.Apply(board, action);
        var destinations = new List<Tile>();
        grid.GetReachableMoveTiles(attacker.currentTile, enemy, 3, board.GetOccupant,
            destinations, board.IsAtWar, board.GetUnitOwner);
        Assert.That(destinations.Contains(grid.GetTileAt(new Vector2Int(2, 3))), Is.False);
        var primary = Unit(player, Tile(3, 4));
        attacker.data.splashRadius = 1;
        attacker.data.splashDamage = 2;
        ActionSimulator.Apply(board, new CandidateAction { unit = attacker, moveTile = attacker.currentTile,
            kind = ActionKind.Attack, target = primary });
        Assert.That(board.GetHealth(board.Recruit), Is.EqualTo(8));
        board.Rollback(0);
    }

    [Test]
    public void HealingCanMakeReplacementUnnecessary()
    {
        SetupDefense();
        wounded.data.defensePower = recruit.unitData.defensePower;
        wounded.currentHealth = 8;
        Assert.That(EconomyAI.FindReplacement(city, wounded, profile, out _), Is.Null);
    }

    [Test]
    public void PlayTurnSavesCityWithRiskyRetreatAndRechecksRemainingBudget()
    {
        SetupDefense();
        wounded.data.moveRange = 1;
        Tile retreat = grid.GetTileAt(new Vector2Int(2, 3));
        Assert.That(CityDefense.RemainingHealth(wounded.data, wounded.currentHealth, retreat, player, board), Is.Zero);
        profile.perUnitLookaheadCandidates = profile.maxShortlistSize = 1;
        profile.LookaheadFrameBudgetMs = 10000;
        FogOfWarManager previous = FogOfWarManager.Instance;
        FogOfWarManager.Instance = Component<FogOfWarManager>();
        Unit spawned = null;
        try
        {
            var turn = ai.PlayTurn(player, profile);
            Assert.That(turn.MoveNext(), Is.True);
            var execution = turn.Current as IEnumerator;
            Assert.That(execution, Is.Not.Null);
            Assert.That(execution.MoveNext(), Is.True);
            spawned = city.centerTile.currentUnit;
            Assert.That(spawned, Is.Not.Null.And.Not.SameAs(wounded));
            Assert.That(wounded.currentTile, Is.SameAs(retreat));
            var secondCity = City(Tile(3, 10), player);
            player.cities.Add(secondCity);
            var secondDefender = Unit(player, secondCity.centerTile);
            secondDefender.currentHealth = 2;
            Unit(enemy, Tile(4, 10));
            Assert.That(EconomyAI.FindReplacement(secondCity, secondDefender, profile, out _), Is.Null,
                "The first purchase must consume the shared budget.");
        }
        finally
        {
            if (spawned != null && spawned != wounded) Object.DestroyImmediate(spawned.gameObject);
            FogOfWarManager.Instance = previous;
        }
    }

    [Test]
    public void RetreatAndRecruitCompleteBeforeFirstYieldAndSpendOnlyOnce()
    {
        SetupDefense();
        planner.TryPlan(player, profile, scorer, out var action);
        FogOfWarManager previous = FogOfWarManager.Instance;
        FogOfWarManager.Instance = Component<FogOfWarManager>();
        Unit spawned = null;
        try
        {
            var execution = (IEnumerator)Call(ai, "Execute", action, turns.Diplomacy.Revision);
            Assert.That(execution.MoveNext(), Is.True);
            spawned = city.centerTile.currentUnit;
            Assert.That(spawned, Is.Not.Null.And.Not.SameAs(wounded));
            Assert.That(wounded.currentTile, Is.SameAs(action.moveTile));
            Assert.That(spawned.data, Is.SameAs(recruit.unitData));
            Assert.That(spawned.isActive, Is.False);
            Assert.That(player.stars, Is.EqualTo(2));
            Assert.That(city.units.Count, Is.EqualTo(2));
            Assert.That(execution.MoveNext(), Is.False);
            Assert.That(player.stars, Is.EqualTo(2));
        }
        finally
        {
            if (spawned != null && spawned != wounded) Object.DestroyImmediate(spawned.gameObject);
            FogOfWarManager.Instance = previous;
        }
    }

    [Test]
    public void EmptyThreatenedCityRecruitmentOutranksSafeCity()
    {
        SetupDefense();
        city.centerTile.currentUnit = null;
        var safe = City(Tile(15, 15), player);
        player.cities.Add(safe);
        var economy = Component<EconomyAI>();
        typeof(EconomyAI).GetField("controlledPlayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(economy, player);
        typeof(EconomyAI).GetField("profile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(economy, profile);
        var candidates = new List<EconomyCandidateAction>();
        Call(economy, "GenerateSpawnCandidates", candidates);
        Assert.That(candidates.Find(a => a.city == city).score, Is.GreaterThan(candidates.Find(a => a.city == safe).score));
    }
}
