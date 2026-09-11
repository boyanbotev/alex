using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

public class DiplomacyIntegrationTests : TacticsTestFixture
{
    // Seed scenarios without adding an unrestricted production relation setter.
    private void SetRelation(DiplomaticRelation relation)
    {
        var state = turns.Diplomacy;
        if (relation == DiplomaticRelation.Peace)
        {
            state.MakePeace(player, enemy);
            return;
        }
        var matrix = (DiplomaticRelation[,])typeof(DiplomacyState)
            .GetField("relations", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state);
        matrix[0, 1] = matrix[1, 0] = relation;
    }

    [TestCase(DiplomaticRelation.Peace)]
    [TestCase(DiplomaticRelation.Allied)]
    public void NonEnemiesAreExcludedFromAttacksAndLiveAttackIsRejected(DiplomaticRelation relation)
    {
        var attacker = Unit(player, Tile(0));
        var target = Unit(enemy, Tile(1));
        Assert.That(Candidates(attacker).Exists(a => a.target == target), Is.True);
        SetRelation(relation);
        Assert.That(Candidates(attacker).Exists(a => a.target == target), Is.False);
        attacker.Attack(target);
        Assert.That(target.currentHealth, Is.EqualTo(10));
        Assert.That(attacker.hasAttacked, Is.False);
    }

    [Test]
    public void StaleSimulatedAttackDoesNotMoveOrDamageAfterPeace()
    {
        var attacker = Unit(player, Tile(0));
        var move = Tile(1);
        var target = Unit(enemy, Tile(2));
        var action = new CandidateAction
        {
            unit = attacker, target = target, moveTile = move, kind = ActionKind.Attack
        };
        SetRelation(DiplomaticRelation.Peace);
        Simulate(action);
        Assert.That(board.GetTile(attacker), Is.SameAs(attacker.currentTile));
        Assert.That(board.GetHealth(target), Is.EqualTo(10));
        Assert.That(board.HasAttacked(attacker), Is.False);
    }

    [TestCase(DiplomaticRelation.Peace)]
    [TestCase(DiplomaticRelation.Allied)]
    public void NonEnemyCitiesAreNotCaptureTargetsButUnownedCitiesRemainClaimable(DiplomaticRelation relation)
    {
        var unit = Unit(player, Tile(0));
        var city = City(Tile(1), enemy);
        SetRelation(relation);
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, city.centerTile, board), Is.Zero);
        Simulate(new CandidateAction { unit = unit, moveTile = city.centerTile, kind = ActionKind.MoveOnly });
        Assert.That(board.HasPendingCityCapture(city), Is.False);

        int checkpoint = board.Checkpoint();
        board.WithCityClaim(city, null);
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, city.centerTile, board), Is.GreaterThan(0));
        Assert.That(InteractionRules.CanCapture(player, board.GetOwner(city)), Is.True);
        board.Rollback(checkpoint);
        Assert.That(InteractionRules.CanCapture(player, board.GetOwner(city)), Is.False);
        Assert.That(InteractionRules.CanCapture(player, null), Is.True);
        Assert.That(InteractionRules.CanCapture(player, player), Is.False);
    }

    [Test]
    public void PendingAndDirectCaptureAreRejectedAfterPeace()
    {
        var unit = Unit(player, Tile(0));
        var city = City(unit.currentTile, enemy);
        city.SetPendingCapture(unit);
        Assert.That(city.HasPendingCapture, Is.True);
        SetRelation(DiplomaticRelation.Peace);
        Assert.That(city.HasPendingCapture, Is.False);
        Assert.That(city.ResolvePendingCapture(false), Is.False);
        city.SetPendingCapture(unit);
        Assert.That(city.pendingCapturer, Is.Null);
        city.Capture(unit);
        Assert.That(city.owner, Is.SameAs(enemy));
        Assert.That(unit.hasCaptured, Is.False);
    }

    [TestCase(DiplomaticRelation.Peace)]
    [TestCase(DiplomaticRelation.Allied)]
    public void NonEnemiesBlockPassageButNotDiagonalCorners(DiplomaticRelation relation)
    {
        var unit = Unit(player, Tile(0, 0));
        var east = Unit(enemy, Tile(1, 0));
        Unit(enemy, Tile(0, 1));
        var diagonal = Tile(1, 1);
        var result = new List<Tile>();
        grid.GetReachableMoveTiles(unit.currentTile, player, 1, board.GetOccupant, result);
        Assert.That(result.Contains(diagonal), Is.False);
        SetRelation(relation);
        grid.GetReachableMoveTiles(unit.currentTile, player, 1, board.GetOccupant, result);
        Assert.That(result.Contains(diagonal), Is.True);
        Assert.That(result.Contains(east.currentTile), Is.False);
        Assert.That(InteractionRules.CanPassThrough(player, enemy), Is.False);
    }

    [TestCase(DiplomaticRelation.Peace)]
    [TestCase(DiplomaticRelation.Allied)]
    public void NonEnemiesGiveNoTacticalPositionOrThreatScore(DiplomaticRelation relation)
    {
        var unit = Unit(player, Tile(0));
        Unit(enemy, Tile(1));
        board.WithDamage(unit, 1);
        SetRelation(relation);
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, unit.currentTile, board), Is.Zero);
    }

    [TestCase(DiplomaticRelation.Peace)]
    [TestCase(DiplomaticRelation.Allied)]
    public void EconomyAndSelectionIgnoreNonEnemies(DiplomaticRelation relation)
    {
        var unit = Unit(player, Tile(0));
        var target = Unit(enemy, Tile(1));
        var home = City(unit.currentTile, player);
        City(target.currentTile, enemy);
        player.visibleTiles = new VisibilityState(2, 1);
        player.visibleTiles.SetVisible(target.currentTile.gridPosition);
        var selectionQuery = typeof(SelectionController).GetMethod("HasInRangeEnemy",
            BindingFlags.Static | BindingFlags.NonPublic);
        var economy = Component<EconomyAI>();
        typeof(EconomyAI).GetField("controlledPlayer", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(economy, player);
        var nearby = new List<Unit>();
        Call(economy, "GetNearbyEnemies", home, nearby);
        Assert.That(nearby.Count, Is.EqualTo(1));
        Assert.That(selectionQuery.Invoke(null, new object[] { unit }), Is.EqualTo(true));
        Assert.That(Call(economy, "HasUncapturedCityNearby", home), Is.EqualTo(true));
        SetRelation(relation);
        Call(economy, "GetNearbyEnemies", home, nearby);
        Assert.That(nearby, Is.Empty);
        Assert.That(selectionQuery.Invoke(null, new object[] { unit }), Is.EqualTo(false));
        Assert.That(Call(economy, "HasUncapturedCityNearby", home), Is.EqualTo(false));
    }
}
