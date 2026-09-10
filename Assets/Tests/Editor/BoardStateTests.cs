using NUnit.Framework;

public class BoardStateTests : TacticsTestFixture
{
    [Test]
    public void ReadersFallBackToLiveState()
    {
        var tile = Tile(0);
        var unit = Unit(player, tile);
        var city = City(tile, enemy);
        unit.hasMoved = unit.hasAttacked = true;
        unit.isActive = false;
        Assert.That(board.GetTile(unit), Is.SameAs(tile));
        Assert.That(board.GetOccupant(tile), Is.SameAs(unit));
        Assert.That(board.GetHealth(unit), Is.EqualTo(10));
        Assert.That(board.HasMoved(unit), Is.True);
        Assert.That(board.HasAttacked(unit), Is.True);
        Assert.That(board.IsActive(unit), Is.False);
        Assert.That(board.IsAlive(unit), Is.True);
        Assert.That(board.GetOwner(city), Is.SameAs(enemy));
    }

    [Test]
    public void MoveAndRollbackRestoreOccupancyWithoutChangingLiveObjects()
    {
        var from = Tile(0);
        var to = Tile(1);
        var unit = Unit(player, from);
        int checkpoint = board.Checkpoint();
        board.WithMove(unit, from, to);
        Assert.That(board.GetOccupant(from), Is.Null);
        Assert.That(board.GetOccupant(to), Is.SameAs(unit));
        Assert.That(board.GetTile(unit), Is.SameAs(to));
        Assert.That(board.HasMoved(unit), Is.True);
        Assert.That(unit.currentTile, Is.SameAs(from));
        Assert.That(from.currentUnit, Is.SameAs(unit));
        Assert.That(to.currentUnit, Is.Null);
        Assert.That(unit.hasMoved, Is.False);
        board.Rollback(checkpoint);
        Assert.That(board.GetTile(unit), Is.SameAs(from));
        Assert.That(board.GetOccupant(from), Is.SameAs(unit));
        Assert.That(board.GetOccupant(to), Is.Null);
        Assert.That(board.HasMoved(unit), Is.False);
        unit.currentHealth = 7;
        Assert.That(board.GetHealth(unit), Is.EqualTo(7));
    }

    [Test]
    public void NestedRollbackRestoresExistingOverridesAndThenLiveFallback()
    {
        var from = Tile(0);
        var to = Tile(1);
        var unit = Unit(player, from);
        var city = City(to, enemy);
        board.WithDamage(unit, 8);
        board.WithPendingCityCapture(city, unit);
        int inner = board.Checkpoint();
        board.WithMove(unit, from, to);
        board.WithAttacked(unit);
        board.WithDeactivated(unit);
        board.WithCityClaim(city, player);
        board.WithDamage(unit, 0);
        Assert.That(board.IsAlive(unit), Is.False);
        Assert.That(board.GetOccupant(to), Is.Null);
        board.Rollback(inner);
        Assert.That(board.GetHealth(unit), Is.EqualTo(8));
        Assert.That(board.IsAlive(unit), Is.True);
        Assert.That(board.IsActive(unit), Is.True);
        Assert.That(board.HasAttacked(unit), Is.False);
        Assert.That(board.HasMoved(unit), Is.False);
        Assert.That(board.GetOwner(city), Is.SameAs(enemy));
        Assert.That(board.GetPendingCityCapturer(city), Is.SameAs(unit));
        board.Rollback(0);
        Assert.That(board.GetHealth(unit), Is.EqualTo(10));
        Assert.That(board.HasPendingCityCapture(city), Is.False);
        Assert.That(unit.currentHealth, Is.EqualTo(10));
        Assert.That(city.owner, Is.SameAs(enemy));
        Assert.That(city.pendingCapturer, Is.Null);
        Assert.That(board.Checkpoint(), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MovingOrDyingClearsOnlyThatUnitsPendingCaptures(bool dies)
    {
        var from = Tile(0);
        var to = Tile(1);
        var unit = Unit(player, from);
        var other = Unit(enemy, Tile(2));
        var city = City(from);
        var otherCity = City(other.currentTile);
        board.WithPendingCityCapture(city, unit);
        board.WithPendingCityCapture(otherCity, other);
        int checkpoint = board.Checkpoint();
        if (dies) board.WithDamage(unit, 0);
        else board.WithMove(unit, from, to);
        Assert.That(board.HasPendingCityCapture(city), Is.False);
        Assert.That(board.GetPendingCityCapturer(otherCity), Is.SameAs(other));
        board.Rollback(checkpoint);
        Assert.That(board.GetPendingCityCapturer(city), Is.SameAs(unit));
    }

    [Test]
    public void StaticMoveDeactivatesAndRollbackReactivates()
    {
        var unit = Unit(player, Tile(0));
        unit.data.skills = new[] { Skill.Static };
        board.WithMove(unit, unit.currentTile, Tile(1));
        Assert.That(board.IsActive(unit), Is.False);
        Assert.That(unit.isActive, Is.True);
        board.Rollback(0);
        Assert.That(board.IsActive(unit), Is.True);
    }

    [Test]
    public void ReplacingAndRemovingPendingCaptureRollsBackInOrder()
    {
        var unit = Unit(player, Tile(0));
        var other = Unit(enemy, Tile(1));
        var city = City(unit.currentTile);
        board.WithPendingCityCapture(city, unit);
        int checkpoint = board.Checkpoint();
        board.WithPendingCityCapture(city, other);
        board.WithoutPendingCityCapture(city);
        Assert.That(board.HasPendingCityCapture(city), Is.False);
        board.Rollback(checkpoint);
        Assert.That(board.GetPendingCityCapturer(city), Is.SameAs(unit));
    }

    [Test]
    public void PendingCaptureCleanupNeedsNoWorldManagerOrRegisteredCities()
    {
        var unit = Unit(player, Tile(0));
        var first = City(unit.currentTile);
        var second = City(Tile(1));
        board.WithPendingCityCapture(first, unit);
        board.WithPendingCityCapture(second, unit);
        WorldPopulationManager.Instance = null;
        population.allCities.Clear();
        int checkpoint = board.Checkpoint();
        board.WithMove(unit, unit.currentTile, second.centerTile);
        Assert.That(board.HasPendingCityCapture(first), Is.False);
        Assert.That(board.HasPendingCityCapture(second), Is.False);
        board.Rollback(checkpoint);
        Assert.That(board.GetPendingCityCapturer(first), Is.SameAs(unit));
        Assert.That(board.GetPendingCityCapturer(second), Is.SameAs(unit));
    }

    [Test]
    public void RepeatedWritesAndRollbackToCurrentCheckpointAreHarmless()
    {
        var unit = Unit(player, Tile(0));
        board.WithDamage(unit, 8);
        board.WithAttacked(unit);
        int checkpoint = board.Checkpoint();
        board.WithDamage(unit, 8);
        board.WithAttacked(unit);
        Assert.That(board.Checkpoint(), Is.EqualTo(checkpoint));
        board.Rollback(checkpoint);
        Assert.That(board.GetHealth(unit), Is.EqualTo(8));
        board.Rollback(0);
        unit.currentHealth = 6;
        Assert.That(board.GetHealth(unit), Is.EqualTo(6), "Rollback must remove overrides, not copy old live values.");
    }
}
