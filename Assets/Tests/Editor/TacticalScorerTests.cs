using NUnit.Framework;

public class TacticalScorerTests : TacticsTestFixture
{
    private void ClearWeights()
    {
        profile.cityCaptureWeight = profile.cityProgressWeight = profile.damageWeight = 0;
        profile.killWeight = profile.retaliationWeight = profile.survivalWeight = profile.positionWeight = 0;
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
