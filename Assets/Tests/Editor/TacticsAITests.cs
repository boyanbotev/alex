using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

public class TacticsAITests : TacticsTestFixture
{
    [TestCase(1)]
    [TestCase(2)]
    public void KillingAttackAdvancesOnlyMeleeAndRegistersCapture(int range)
    {
        var attacker = Unit(player, Tile(0));
        var target = Unit(enemy, Tile(1));
        var city = City(target.currentTile, enemy);
        attacker.data.attackRange = range;
        target.currentHealth = 1;
        Simulate(new CandidateAction { unit = attacker, moveTile = attacker.currentTile, target = target, kind = ActionKind.Attack });
        Assert.That(board.IsAlive(target), Is.False);
        Assert.That(board.GetTile(attacker), Is.SameAs(range == 1 ? target.currentTile : attacker.currentTile));
        Assert.That(board.GetOccupant(target.currentTile), Is.SameAs(range == 1 ? attacker : null));
        Assert.That(board.HasPendingCityCapture(city), Is.EqualTo(range == 1));
        Assert.That(board.GetHealth(attacker), Is.EqualTo(10));
        Assert.That(board.HasAttacked(attacker), Is.True);
        Assert.That(board.HasMoved(attacker), Is.True);
        Assert.That(target.isAlive, Is.True);
        Assert.That(target.currentHealth, Is.EqualTo(1));
    }

    [TestCase(1, 5)]
    [TestCase(2, 10)]
    public void SurvivingDefenderRetaliatesOnlyWithinRange(int distance, int attackerHealth)
    {
        var attacker = Unit(player, Tile(0));
        var target = Unit(enemy, Tile(distance));
        attacker.data.attackRange = 2;
        Simulate(new CandidateAction { unit = attacker, moveTile = attacker.currentTile, target = target, kind = ActionKind.Attack });
        Assert.That(board.GetHealth(target), Is.EqualTo(5));
        Assert.That(board.GetHealth(attacker), Is.EqualTo(attackerHealth));
    }

    [Test]
    public void StaticUnitCannotGenerateMoveAndAttack()
    {
        var unit = Unit(player, Tile(0));
        var destination = Tile(1);
        Unit(enemy, Tile(2));
        unit.data.skills = new[] { Skill.Static };
        var candidates = Candidates(unit);
        Assert.That(candidates.Exists(a => a.kind == ActionKind.MoveOnly && a.moveTile == destination), Is.True);
        Assert.That(candidates.Exists(a => a.kind == ActionKind.Attack), Is.False);
        unit.data.skills = System.Array.Empty<Skill>();
        Assert.That(Candidates(unit).Exists(a => a.kind == ActionKind.Attack && a.moveTile == destination), Is.True);
    }

    [Test]
    public void UnavailableUnitsAreExcluded()
    {
        var dead = Unit(player, Tile(0));
        dead.isAlive = false;
        var inactive = Unit(player, Tile(1));
        inactive.isActive = false;
        var spent = Unit(player, Tile(2));
        spent.hasMoved = spent.hasAttacked = true;
        player.units.Add(null);
        generator.Generate(player, board);
        var shortlist = new List<CandidateAction>();
        generator.SelectShortlist(2, shortlist);
        Assert.That(shortlist, Is.Empty);
    }

    [Test]
    public void ShortlistKeepsBestCandidatesPerUnitInDescendingOrder()
    {
        var unit = Unit(player, Tile(0));
        City(Tile(1), enemy);
        Unit(enemy, Tile(2));
        generator.Generate(player, board);
        var shortlist = new List<CandidateAction>();
        generator.SelectShortlist(2, shortlist);
        var expected = Candidates(unit);
        expected.Sort((a, b) => b.score.CompareTo(a.score));
        Assert.That(shortlist.Count, Is.EqualTo(2));
        Assert.That(shortlist[0].score, Is.EqualTo(expected[0].score));
        Assert.That(shortlist[1].score, Is.EqualTo(expected[1].score));
    }

    [Test]
    public void LookaheadRestoresBoardIncludingPreexistingOverrides()
    {
        var unit = Unit(player, Tile(0));
        var target = Unit(enemy, Tile(1));
        board.WithDamage(unit, 8);
        int checkpoint = board.Checkpoint();
        var action = new CandidateAction { unit = unit, moveTile = unit.currentTile, target = target, kind = ActionKind.Attack };
        float first = (float)Call(ai, "EvaluateWithLookahead", action, board);
        float second = (float)Call(ai, "EvaluateWithLookahead", action, board);
        Assert.That(second, Is.EqualTo(first));
        Assert.That(board.Checkpoint(), Is.EqualTo(checkpoint));
        Assert.That(board.GetHealth(unit), Is.EqualTo(8));
        Assert.That(board.GetHealth(target), Is.EqualTo(10));
        Assert.That(board.HasAttacked(unit), Is.False);
        Assert.That(board.IsActive(unit), Is.True);
    }

    [Test]
    public void SingleCandidateIsExecutedOnce()
    {
        var unit = Unit(player, Tile(0));
        unit.hasMoved = true;
        var turn = ai.PlayTurn(player, profile);
        Assert.That(turn.MoveNext(), Is.True);
        Assert.That(turn.Current, Is.InstanceOf<IEnumerator>());
        // Emulate completion of the yielded DoNothing action without its renderer.
        unit.isActive = false;
        Assert.That(turn.MoveNext(), Is.False, "The turn must not yield execution of the same action again.");
    }

    [Test]
    public void ShortlistPreservesTiesAndSurvivesLaterGeneration()
    {
        var first = Unit(player, Tile(0));
        var second = Unit(player, Tile(4));
        var shortlist = new List<CandidateAction>();
        generator.Generate(player, board);
        generator.SelectShortlist(2, shortlist);
        Assert.That(shortlist.Count, Is.EqualTo(2));
        Assert.That(shortlist[0].unit, Is.SameAs(first));
        Assert.That(shortlist[1].unit, Is.SameAs(second));
        generator.Generate(enemy, board);
        Assert.That(generator.Candidates, Is.Empty);
        Assert.That(shortlist.Count, Is.EqualTo(2));
        Assert.That(shortlist[0].unit, Is.SameAs(first));
    }

    [Test]
    public void LookaheadRollsBackEvenWhenScoringThrows()
    {
        var unit = Unit(player, Tile(0));
        // Deliberately malformed enemy forces scoring to fail after applying the move.
        var invalid = Component<Unit>();
        invalid.isAlive = true;
        enemy.units.Add(invalid);
        var destination = Tile(1);
        var action = new CandidateAction { unit = unit, moveTile = destination, kind = ActionKind.MoveOnly };
        Assert.Throws<System.Reflection.TargetInvocationException>(() => Call(ai, "EvaluateWithLookahead", action, board));
        Assert.That(board.Checkpoint(), Is.Zero);
        Assert.That(board.GetTile(unit), Is.SameAs(unit.currentTile));
        Assert.That(board.GetOccupant(destination), Is.Null);
        Assert.That(board.HasMoved(unit), Is.False);
    }

    [Test]
    public void DoNothingOnlyDeactivatesSimulatedUnit()
    {
        var unit = Unit(player, Tile(0));
        Simulate(new CandidateAction { unit = unit, moveTile = unit.currentTile, kind = ActionKind.DoNothing });
        Assert.That(board.IsActive(unit), Is.False);
        Assert.That(board.HasMoved(unit), Is.False);
        Assert.That(board.HasAttacked(unit), Is.False);
        Assert.That(unit.isActive, Is.True);
    }

    [Test]
    public void MoveIntoEnemyCityRegistersPendingCaptureWithoutClaiming()
    {
        var unit = Unit(player, Tile(0));
        var city = City(Tile(1), enemy);
        Simulate(new CandidateAction { unit = unit, moveTile = city.centerTile, kind = ActionKind.MoveOnly });
        Assert.That(board.GetPendingCityCapturer(city), Is.SameAs(unit));
        Assert.That(board.GetOwner(city), Is.SameAs(enemy));
        Assert.That(city.pendingCapturer, Is.Null);
    }
}
