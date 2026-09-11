using System.Collections.Generic;
using NUnit.Framework;

public class MovementTests : TacticsTestFixture
{
    private readonly List<Tile> reachable = new();
    private GridGenerator previousGenerator;
    private BoardSettings settings;

    [SetUp]
    public void SetUpMovement()
    {
        previousGenerator = GridGenerator.Instance;
        var gridGenerator = Component<GridGenerator>();
        settings = Asset<BoardSettings>();
        gridGenerator.boardSettings = settings;
        GridGenerator.Instance = gridGenerator;
    }

    [TearDown]
    public void TearDownMovement() => GridGenerator.Instance = previousGenerator;

    private void Search(Unit unit, int range) => grid.GetReachableMoveTiles(
        unit.currentTile, unit.owner, range, tile => tile.currentUnit, reachable);

    [Test]
    public void EnemyBlocksCorridorButAllyAllowsPassage()
    {
        var unit = Unit(player, Tile(0));
        var blocker = Unit(enemy, Tile(1));
        var destination = Tile(2);
        Search(unit, 2);
        Assert.That(reachable, Is.Empty);
        blocker.owner = player;
        Search(unit, 2);
        Assert.That(reachable, Is.EquivalentTo(new[] { destination }));
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void ClosedCornerRespectsSetting(bool block, bool expected)
    {
        settings.blockDiagonalsBetweenEnemies = block;
        var unit = Unit(player, Tile(0, 0));
        Unit(enemy, Tile(1, 0));
        Unit(enemy, Tile(0, 1));
        var destination = Tile(1, 1);
        Search(unit, 1);
        Assert.That(reachable.Contains(destination), Is.EqualTo(expected));
    }

    [Test]
    public void OneEnemyAllowsDiagonalAndDetourCostsMovement()
    {
        var unit = Unit(player, Tile(0));
        Unit(enemy, Tile(1));
        var destination = Tile(2);
        var detour = Tile(1, 1);
        Search(unit, 1);
        Assert.That(reachable.Contains(detour), Is.True);
        Assert.That(reachable.Contains(destination), Is.False);
        Search(unit, 2);
        Assert.That(reachable.Contains(destination), Is.True);
    }

    [Test]
    public void CandidatesRespectSimulatedDeathAndRollback()
    {
        var unit = Unit(player, Tile(0));
        unit.data.moveRange = 2;
        var blocker = Unit(enemy, Tile(1));
        var destination = Tile(2);
        Assert.That(Candidates(unit).Exists(c => c.moveTile == destination), Is.False);
        int checkpoint = board.Checkpoint();
        board.WithDamage(blocker, 0);
        Assert.That(Candidates(unit).Exists(c => c.moveTile == destination), Is.True);
        Search(unit, 2);
        Assert.That(reachable, Is.Empty);
        board.Rollback(checkpoint);
        Assert.That(Candidates(unit).Exists(c => c.moveTile == destination), Is.False);
    }
}
