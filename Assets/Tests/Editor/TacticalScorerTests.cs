using NUnit.Framework;

public class TacticalScorerTests : TacticsTestFixture
{
    [TestCase(true, 1, 2, true)]
    [TestCase(true, 2, 2, false)]
    [TestCase(false, 4, 2, true)]
    [TestCase(false, 5, 2, false)]
    [TestCase(false, 1, 0, false)]
    public void TacticsAndCityDefenseAgreeOnAttackThreats(bool isStatic, int distance, int power, bool threatens)
    {
        ClearWeights();
        profile.survivalWeight = 4;
        var defender = Unit(player, Tile(0));
        defender.currentHealth = 1;
        var attacker = Unit(enemy, Tile(distance));
        attacker.data.moveRange = 3;
        attacker.data.attackRange = 1;
        attacker.data.attackPower = power;
        attacker.data.skills = isStatic ? new[] { Skill.Static } : System.Array.Empty<Skill>();
        // Threat estimates concern the next turn, even when this turn is spent.
        attacker.hasMoved = attacker.hasAttacked = true;
        attacker.isActive = false;

        Assert.That(CityDefense.CanAttackNextTurn(attacker, defender.currentTile, board), Is.EqualTo(threatens));
        Assert.That(scorer.ScoreMove(defender, defender.currentTile, defender.currentTile, board),
            Is.EqualTo(threatens ? -8 : 0));
        Assert.That(CityDefense.RemainingHealth(defender.data, 1, defender.currentTile, player, board),
            Is.EqualTo(threatens ? 0 : 1));
    }

    [TestCase(true, 2)]
    [TestCase(false, 0)]
    public void CaptureReachDoesNotRequireAnAttack(bool isStatic, int power)
    {
        var city = City(Tile(0), player);
        var attacker = Unit(enemy, Tile(2));
        attacker.data.moveRange = 2;
        attacker.data.attackRange = 1;
        attacker.data.attackPower = power;
        attacker.data.skills = isStatic ? new[] { Skill.Static } : System.Array.Empty<Skill>();
        Assert.That(CityDefense.CanAttackNextTurn(attacker, city.centerTile, board), Is.False);
        Assert.That(CityDefense.CanReachCityNextTurn(attacker, city.centerTile, board), Is.True);
        Assert.That(CityDefense.IsThreatened(city.centerTile, player, board), Is.True);
    }

    [Test]
    public void AttackThreatUsesSimulatedPositionAndDeathAndRollsBack()
    {
        var target = Tile(0);
        var nearby = Tile(1);
        var attacker = Unit(enemy, Tile(3));
        attacker.data.skills = new[] { Skill.Static };
        Assert.That(CityDefense.CanAttackNextTurn(attacker, target, board), Is.False);
        board.WithMove(attacker, attacker.currentTile, nearby);
        Assert.That(CityDefense.CanAttackNextTurn(attacker, target, board), Is.True);
        board.WithDamage(attacker, 0);
        Assert.That(CityDefense.CanAttackNextTurn(attacker, target, board), Is.False);
        Assert.That(CityDefense.CanReachCityNextTurn(attacker, target, board), Is.False);
        board.Rollback(0);
        Assert.That(CityDefense.CanAttackNextTurn(attacker, target, board), Is.False);
    }

    private void ClearWeights()
    {
        profile.cityCaptureWeight = profile.cityProgressWeight = profile.damageWeight = 0;
        profile.killWeight = profile.retaliationWeight = profile.survivalWeight = profile.positionWeight = 0;
    }

    [Test]
    public void KillingMeleeAttackOffCityDoesNotKeepGarrisonBonus()
    {
        ClearWeights();
        profile.cityCaptureWeight = 40;
        var defender = Unit(player, Tile(0));
        City(defender.currentTile, player);
        var target = Unit(enemy, Tile(1));
        target.currentHealth = 1;
        Unit(enemy, Tile(-1));
        Assert.That(scorer.ScoreMove(defender, defender.currentTile, defender.currentTile, board), Is.EqualTo(40));
        Assert.That(scorer.ScoreAttack(defender, defender.currentTile, target, board), Is.Zero);
        defender.data.attackRange = 2;
        Assert.That(scorer.ScoreAttack(defender, defender.currentTile, target, board), Is.EqualTo(40));
    }

    [Test]
    public void AttackScoresDamageRetaliationAndKillBonus()
    {
        ClearWeights();
        var attacker = Unit(player, Tile(0));
        var target = Unit(enemy, Tile(1));
        profile.damageWeight = 1;
        Assert.That(scorer.ScoreAttack(attacker, attacker.currentTile, target, board), Is.EqualTo(5));
        profile.retaliationWeight = 2;
        Assert.That(scorer.ScoreAttack(attacker, attacker.currentTile, target, board), Is.EqualTo(-5));
        board.WithDamage(target, 1);
        profile.killWeight = 3;
        Assert.That(scorer.ScoreAttack(attacker, attacker.currentTile, target, board), Is.EqualTo(14));
        Assert.That(target.currentHealth, Is.EqualTo(10));
    }

    [Test]
    public void CityProgressAndCaptureUseSimulatedOwnership()
    {
        ClearWeights();
        var unit = Unit(player, Tile(0));
        var closer = Tile(1);
        var city = City(Tile(2), enemy);
        profile.cityProgressWeight = 3;
        profile.cityCaptureWeight = 40;
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, closer, board), Is.EqualTo(3));
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, city.centerTile, board), Is.EqualTo(46));
        board.WithCityClaim(city, player);
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, city.centerTile, board), Is.Zero);
    }

    [Test]
    public void LethalCounterPenaltyUsesSimulatedHealthAndAliveState()
    {
        ClearWeights();
        var unit = Unit(player, Tile(0));
        var opponent = Unit(enemy, Tile(1));
        profile.survivalWeight = 4;
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, unit.currentTile, board), Is.Zero);
        board.WithDamage(unit, 1);
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, unit.currentTile, board), Is.EqualTo(-8));
        board.WithDamage(opponent, 0);
        Assert.That(scorer.ScoreMove(unit, unit.currentTile, unit.currentTile, board), Is.Zero);
    }
}
