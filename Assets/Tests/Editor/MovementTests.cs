using System.Collections.Generic;
using NUnit.Framework;

public class MovementTests : TacticsTestFixture
{
    private readonly List<Tile> reachable = new();
    private GridGenerator previousGenerator;
    private Level settings;

    [SetUp]
    public void SetUpMovement()
    {
        previousGenerator = GridGenerator.Instance;
        var gridGenerator = Component<GridGenerator>();
        settings = Asset<Level>();
        gridGenerator.level = settings;
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
    public void ForestCanBeEnteredButNotCrossed()
    {
        var unit = Unit(player, Tile(0));
        var forest = Tile(1);
        forest.terrainType = TerrainType.Forest;
        Tile(2);
        Search(unit, 4);
        Assert.That(reachable, Is.EquivalentTo(new[] { forest }));
    }

    [Test]
    public void MountainCannotBeEnteredOrCrossed()
    {
        var unit = Unit(player, Tile(0));
        var mountain = Tile(1);
        mountain.terrainType = TerrainType.Mountain;
        Tile(2);
        Search(unit, 4);
        Assert.That(reachable, Is.Empty);
        unit.MoveTo(mountain);
        Assert.That(unit.currentTile.gridPosition.x, Is.Zero);
        Assert.That(unit.hasMoved, Is.False);
        Assert.That(mountain.currentUnit, Is.Null);
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void MountainCornerSettingIsIndependentOfEnemyCornerSetting(bool blockTerrain, bool blockEnemies)
    {
        settings.blockDiagonalsBetweenImpassableTiles = blockTerrain;
        settings.blockDiagonalsBetweenEnemies = blockEnemies;
        var unit = Unit(player, Tile(0, 0));
        unit.data.moveRange = 3;
        Tile(1, 0).terrainType = TerrainType.Mountain;
        Tile(0, 1).terrainType = TerrainType.Mountain;
        var destination = Tile(1, 1);
        Search(unit, 3);
        Assert.That(reachable, Is.EquivalentTo(blockTerrain ? new Tile[0] : new[] { destination }));
        Assert.That(Candidates(unit).Exists(c => c.moveTile == destination), Is.EqualTo(!blockTerrain));
    }

    [Test]
    public void OpenRouteAroundMountainRemainsReachableForPlayerAndAI()
    {
        var unit = Unit(player, Tile(0));
        unit.data.moveRange = 2;
        var mountain = Tile(1);
        mountain.terrainType = TerrainType.Mountain;
        var destination = Tile(2);
        Tile(1, 1);
        Search(unit, 2);
        Assert.That(reachable.Contains(destination), Is.True);
        var candidates = Candidates(unit);
        Assert.That(candidates.Exists(c => c.moveTile == destination), Is.True);
        Assert.That(candidates.Exists(c => c.moveTile == mountain), Is.False);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(4)]
    public void StartingInForestCapsMovementAtOne(int range)
    {
        var unit = Unit(player, Tile(0));
        unit.currentTile.terrainType = TerrainType.Forest;
        var adjacent = Tile(1);
        Tile(2);
        Search(unit, range);
        Assert.That(reachable, Is.EquivalentTo(range == 0 ? new Tile[0] : new[] { adjacent }));
    }

    [Test]
    public void OpenRouteAroundForestRemainsReachable()
    {
        var unit = Unit(player, Tile(0));
        Tile(1).terrainType = TerrainType.Forest;
        var destination = Tile(2);
        Tile(1, 1);
        Search(unit, 2);
        Assert.That(reachable.Contains(destination), Is.True);
    }

    [Test]
    public void AlliedUnitInForestDoesNotAllowPassageThroughForest()
    {
        var unit = Unit(player, Tile(0));
        var forest = Tile(1);
        forest.terrainType = TerrainType.Forest;
        Unit(player, forest);
        Tile(2);
        Search(unit, 4);
        Assert.That(reachable, Is.Empty);
    }

    [Test]
    public void ForestDestinationStillAllowsMoveAndAttackCandidate()
    {
        var unit = Unit(player, Tile(0));
        unit.data.moveRange = 4;
        unit.data.attackRange = 1;
        var forest = Tile(1);
        forest.terrainType = TerrainType.Forest;
        var target = Unit(enemy, Tile(2));
        Tile(3);
        var candidates = Candidates(unit);
        Assert.That(candidates.Exists(c => c.moveTile == forest && c.target == target &&
            c.kind == ActionKind.Attack), Is.True);
        Assert.That(candidates.Exists(c => c.moveTile.gridPosition.x > 1), Is.False);
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
