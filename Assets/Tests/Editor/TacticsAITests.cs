using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

public class TacticsAITests : TacticsTestFixture
{
    [Test]
    public void FreshSimulatedTurnRestoresFlagsWithoutRevivingDeadUnitsOrChangingLiveUnits()
    {
        var unit = Unit(enemy, Tile(0));
        unit.hasMoved = unit.hasAttacked = true;
        unit.isActive = false;
        var dead = Unit(enemy, Tile(1));
        board.WithDamage(dead, 0);
        enemy.units.Add(null);
        int checkpoint = board.Checkpoint();
        board.WithFreshTurn(enemy);
        Assert.That(board.HasMoved(unit), Is.False);
        Assert.That(board.HasAttacked(unit), Is.False);
        Assert.That(board.IsActive(unit), Is.True);
        Assert.That(board.IsAlive(dead), Is.False);
        Assert.That(unit.hasMoved && unit.hasAttacked && !unit.isActive, Is.True);
        board.Rollback(checkpoint);
        Assert.That(board.HasMoved(unit) && board.HasAttacked(unit) && !board.IsActive(unit), Is.True);
    }

    [Test]
    public void LookaheadSeesCityCaptureByAnOpponentWhoSpentTheirPreviousTurn()
    {
        var idle = Unit(player, Tile(10));
        var threat = Unit(enemy, Tile(0));
        City(Tile(1), player);
        threat.hasMoved = threat.hasAttacked = true;
        threat.isActive = false;
        profile.ownRolloutSteps = 0;
        profile.enemyRolloutSteps = 1;
        var action = new CandidateAction { unit = idle, moveTile = idle.currentTile, kind = ActionKind.DoNothing };
        float score = (float)Call(ai, "EvaluateWithLookahead", action, board);
        Assert.That(score, Is.LessThanOrEqualTo(-profile.cityCaptureWeight));
        Assert.That(board.Checkpoint(), Is.Zero);
        Assert.That(board.HasMoved(threat) && board.HasAttacked(threat) && !board.IsActive(threat), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ThreatenedCityDefenseSurvivesSingleCandidatePruningAndGlobalCap(bool alreadyOnCity)
    {
        // Five earlier units used to exclude this defender from the evaluation cap.
        for (int i = 0; i < 5; i++) Unit(player, Tile(10 + i * 3));
        var city = City(Tile(0), player);
        var defender = Unit(player, alreadyOnCity ? city.centerTile : Tile(-1));
        defender.data.defensePower = 100;
        defender.data.attackPower = 0;
        var threat = Unit(enemy, Tile(1));
        threat.hasMoved = threat.hasAttacked = true;
        threat.isActive = false;
        profile.perUnitLookaheadCandidates = 1;
        profile.ownRolloutSteps = 1;
        profile.enemyRolloutSteps = 1;
        generator.Generate(player, board);
        var shortlist = new List<CandidateAction>();
        generator.SelectShortlist(1, shortlist);
        Assert.That(shortlist[0].unit, Is.SameAs(defender));
        Assert.That(shortlist[0].moveTile, Is.SameAs(city.centerTile));
        float defenseScore = (float)Call(ai, "EvaluateWithLookahead", shortlist[0], board);
        var abandon = new CandidateAction {
            unit = defender, moveTile = defender.currentTile, kind = ActionKind.DoNothing
        };
        if (!alreadyOnCity)
            Assert.That(defenseScore, Is.GreaterThan((float)Call(ai, "EvaluateWithLookahead", abandon, board)));
    }

    [Test]
    public void SafeOrPeacefulCityDoesNotReceiveDefenseBonus()
    {
        var unit = Unit(player, Tile(0));
        var city = City(Tile(1), player);
        var threat = Unit(enemy, Tile(2));
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, city.centerTile, board), Is.Zero);
        turns.Diplomacy.DeclareWar(player, enemy);
        scorer.BeginGeneration();
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, city.centerTile, board), Is.GreaterThanOrEqualTo(profile.cityCaptureWeight));
        board.WithDamage(threat, 0);
        scorer.BeginGeneration();
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, city.centerTile, board), Is.Zero);
    }

    [Test]
    public void SplashMatchesLiveCombatAndSkipsPrimaryFriendlyPeacefulAndDistantUnits()
    {
        var attacker = Unit(player, Tile(0));
        var primary = Unit(enemy, Tile(3));
        var adjacent = Unit(enemy, Tile(4));
        var diagonal = Unit(enemy, Tile(4, 1));
        var friendly = Unit(player, Tile(3, 1));
        var distant = Unit(enemy, Tile(5));
        var peacefulPlayer = Component<Player>(); turns.players.Add(peacefulPlayer);
        var ally = Component<Player>(); turns.players.Add(ally);
        turns.Diplomacy.MakePeace(player, peacefulPlayer);
        var peaceful = Unit(peacefulPlayer, Tile(2, 1));
        turns.Diplomacy.MakePeace(player, ally); turns.Diplomacy.MakeAlliance(player, ally);
        var allied = Unit(ally, Tile(2, -1));
        attacker.data.attackRange = 3; attacker.data.splashDamage = 2; attacker.data.splashRadius = 1;
        adjacent.data.defensePower = 100;
        int direct = attacker.PredictAttackDamage(primary).damage;
        int checkpoint = board.Checkpoint();
        ActionSimulator.Apply(board, new CandidateAction {
            unit = attacker, target = primary, moveTile = attacker.currentTile, kind = ActionKind.Attack
        });
        Assert.That(board.GetHealth(primary), Is.EqualTo(10 - direct));
        Assert.That(board.GetHealth(adjacent), Is.EqualTo(8));
        Assert.That(board.GetHealth(diagonal), Is.EqualTo(8));
        foreach (var untouched in new[] { attacker, friendly, peaceful, allied, distant })
            Assert.That(board.GetHealth(untouched), Is.EqualTo(10));
        board.Rollback(checkpoint);
        Assert.That(board.GetHealth(adjacent), Is.EqualTo(10));
        attacker.Attack(primary);
        Assert.That(primary.currentHealth, Is.EqualTo(10 - direct));
        Assert.That(adjacent.currentHealth, Is.EqualTo(8));
        Assert.That(diagonal.currentHealth, Is.EqualTo(8));
        foreach (var untouched in new[] { attacker, friendly, peaceful, allied, distant })
            Assert.That(untouched.currentHealth, Is.EqualTo(10));
        attacker.Attack(primary);
        Assert.That(adjacent.currentHealth, Is.EqualTo(8));
    }

    [Test]
    public void SplashUsesSimulatedPositionsAndKillsEvenWhenPrimaryDiesThenRollsBack()
    {
        var attacker = Unit(player, Tile(0));
        var primary = Unit(enemy, Tile(3)); primary.currentHealth = 1;
        var splash = Unit(enemy, Tile(6)); splash.currentHealth = 2;
        var destination = Tile(4, 1);
        attacker.data.attackRange = 3; attacker.data.splashDamage = 2; attacker.data.splashRadius = 1;
        int checkpoint = board.Checkpoint();
        board.WithMove(splash, splash.currentTile, destination);
        ActionSimulator.Apply(board, new CandidateAction {
            unit = attacker, target = primary, moveTile = attacker.currentTile, kind = ActionKind.Attack
        });
        Assert.That(board.IsAlive(primary), Is.False);
        Assert.That(board.IsAlive(splash), Is.False);
        Assert.That(board.GetOccupant(destination), Is.Null);
        board.Rollback(checkpoint);
        Assert.That(board.IsAlive(primary), Is.True);
        Assert.That(board.IsAlive(splash), Is.True);
        Assert.That(board.GetHealth(splash), Is.EqualTo(2));
        Assert.That(board.GetTile(splash), Is.SameAs(splash.currentTile));
    }

    [TestCase(0, 1)]
    [TestCase(2, 0)]
    [TestCase(0, 0)]
    public void ZeroSplashDamageOrRadiusDisablesSplash(int damage, int radius)
    {
        var attacker = Unit(player, Tile(0)); var primary = Unit(enemy, Tile(3));
        Unit(enemy, Tile(4));
        Assert.That(attacker.data.splashDamage, Is.Zero);
        Assert.That(attacker.data.splashRadius, Is.Zero);
        attacker.data.splashDamage = damage; attacker.data.splashRadius = radius;
        var targets = new List<Unit>();
        CombatMath.CollectSplashTargets(attacker, primary, primary.currentTile, board, targets);
        Assert.That(targets, Is.Empty);
    }

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
