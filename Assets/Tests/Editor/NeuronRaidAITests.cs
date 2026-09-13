using System.Collections;
using NUnit.Framework;

public class NeuronRaidAITests : TacticsTestFixture
{
    private Tile Visible(int x, int y = 0)
    {
        if (player.visibleTiles == null) player.visibleTiles = new VisibilityState(20, 20);
        Tile tile = Tile(x, y);
        player.visibleTiles.SetVisible(tile.gridPosition);
        return tile;
    }

    private Building Road(Tile tile, Player owner)
    {
        var building = Component<Building>();
        building.data = Asset<BuildingData>(); building.data.isNeuron = true;
        building.paidCost = 3; building.owner = owner; building.tile = tile;
        tile.currentBuilding = building;
        return building;
    }

    [Test]
    public void BridgeCutScoresHigherThanRedundantRoute()
    {
        City(Visible(0), enemy); City(Visible(4), enemy);
        Road(Visible(1), enemy);
        var bridge = Road(Visible(2), enemy);
        Road(Visible(3), enemy);
        var raids = new NeuronRaidScorer();
        float cut = raids.Evaluate(player, bridge, board, population.allCities, profile);
        Assert.That(cut, Is.EqualTo(1 + 2 * profile.neuronRaidIncomeWeight));
        Road(Visible(2, 1), enemy);
        Assert.That(raids.Evaluate(player, bridge, board, population.allCities, profile), Is.EqualTo(1));
    }

    [Test]
    public void MovementAndSeverCandidatesRespectFogAllianceAndStaticUnits()
    {
        var unit = Unit(player, Visible(0));
        var road = Road(Visible(1), enemy);
        Assert.That(Candidates(unit).Exists(a => a.kind == ActionKind.SeverNeuron && a.moveTile == road.tile), Is.True);
        unit.data.skills = new[] { Skill.Static };
        Assert.That(Candidates(unit).Exists(a => a.kind == ActionKind.SeverNeuron), Is.False);
        unit.data.skills = System.Array.Empty<Skill>();
        Call(turns.Diplomacy, "SetRelation", player, enemy, DiplomaticRelation.Allied);
        Assert.That(Candidates(unit).Exists(a => a.kind == ActionKind.SeverNeuron), Is.False);
        turns.Diplomacy.DeclareWar(player, enemy);
        player.visibleTiles = new VisibilityState(20, 20);
        Assert.That(Candidates(unit).Exists(a => a.kind == ActionKind.SeverNeuron), Is.False);
    }

    [Test]
    public void PeacefulRefundDoesNotJustifyWarAndFriendlyLossIsPenalized()
    {
        var unit = Unit(player, Visible(1));
        var road = Road(unit.currentTile, enemy);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(Candidates(unit).Exists(a => a.kind == ActionKind.SeverNeuron), Is.False);
        City(Visible(0), player); City(Visible(2), player);
        var raids = new NeuronRaidScorer();
        Assert.That(raids.Evaluate(player, road, board, population.allCities, profile),
            Is.EqualTo(1 - profile.neuronRaidWarPenalty - 2 * profile.neuronRaidFriendlyLossWeight));
    }

    [Test]
    public void SimulationRemovesRoadAndDeclaresWarOnlyInOverlayAndRollsBack()
    {
        var unit = Unit(player, Visible(0));
        var road = Road(Visible(1), enemy);
        turns.Diplomacy.MakePeace(player, enemy);
        int checkpoint = board.Checkpoint();
        var action = new CandidateAction { unit = unit, moveTile = road.tile, neuron = road, kind = ActionKind.SeverNeuron };
        Simulate(action);
        Assert.That(board.GetBuilding(road.tile), Is.Null);
        Assert.That(board.IsAtWar(player, enemy), Is.True);
        Assert.That(turns.Diplomacy.IsAtWar(player, enemy), Is.False);
        Assert.That(board.HasAttacked(unit), Is.True);
        Assert.That(board.IsActive(unit), Is.False);
        Assert.That(road.tile.currentBuilding, Is.SameAs(road));
        Assert.That(player.stars, Is.EqualTo(5));
        Assert.That(unit.hasAttacked, Is.False);
        board.Rollback(checkpoint);
        Assert.That(board.GetBuilding(road.tile), Is.SameAs(road));
        Assert.That(board.IsAtWar(player, enemy), Is.False);
        Assert.That(board.GetTile(unit), Is.SameAs(unit.currentTile));
        Assert.That(board.Checkpoint(), Is.EqualTo(checkpoint));
    }

    [Test]
    public void RemovedRoadCannotBeTargetedAgainDuringRollout()
    {
        var unit = Unit(player, Visible(0));
        var road = Road(Visible(1), enemy);
        board.WithRemovedNeuron(road);
        Assert.That(Candidates(unit).Exists(a => a.kind == ActionKind.SeverNeuron), Is.False);
        int checkpoint = board.Checkpoint();
        Simulate(new CandidateAction { unit = unit, moveTile = road.tile, neuron = road, kind = ActionKind.SeverNeuron });
        Assert.That(board.Checkpoint(), Is.EqualTo(checkpoint));
        Assert.That(board.GetTile(unit), Is.SameAs(unit.currentTile));
    }

    [Test]
    public void SimulatedWarEnablesEnemyResponseWithoutChangingLiveRelations()
    {
        var unit = Unit(player, Visible(0));
        var opponent = Unit(enemy, Visible(1));
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(Candidates(opponent).Exists(a => a.kind == ActionKind.Attack), Is.False);
        board.WithWar(player, enemy);
        Assert.That(Candidates(opponent).Exists(a => a.kind == ActionKind.Attack && a.target == unit), Is.True);
        Assert.That(turns.Diplomacy.IsAtWar(player, enemy), Is.False);
    }

    [Test]
    public void LiveExecutionPaysRefundAndRejectsStaleTarget()
    {
        var unit = Unit(player, Visible(0));
        var road = Road(unit.currentTile, enemy);
        var action = new CandidateAction { unit = unit, moveTile = unit.currentTile, neuron = road, kind = ActionKind.SeverNeuron };
        var execution = (IEnumerator)Call(ai, "Execute", action, turns.Diplomacy.Revision);
        Assert.That(execution.MoveNext(), Is.True);
        Assert.That(unit.currentTile.currentBuilding, Is.Null);
        Assert.That(player.stars, Is.EqualTo(6));
        Assert.That(unit.hasAttacked, Is.True);
        execution = (IEnumerator)Call(ai, "Execute", action, turns.Diplomacy.Revision);
        Assert.That(execution.MoveNext(), Is.False);
        Assert.That(player.stars, Is.EqualTo(6));
    }

    [Test]
    public void AllianceBeforeExecutionPreventsMovementAndSevering()
    {
        var unit = Unit(player, Visible(0));
        var road = Road(Visible(1), enemy);
        var action = new CandidateAction { unit = unit, moveTile = road.tile, neuron = road, kind = ActionKind.SeverNeuron };
        int revision = turns.Diplomacy.Revision;
        Call(turns.Diplomacy, "SetRelation", player, enemy, DiplomaticRelation.Allied);
        var execution = (IEnumerator)Call(ai, "Execute", action, revision);
        Assert.That(execution.MoveNext(), Is.False);
        Assert.That(unit.hasMoved, Is.False);
        Assert.That(road.tile.currentBuilding, Is.SameAs(road));
    }
}
