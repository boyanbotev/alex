using System;
using NUnit.Framework;

public class DiplomacyStateTests : TacticsTestFixture
{
    [Test]
    public void TransitionsAreSymmetricAndOnlyNotifyOnChange()
    {
        var diplomacy = turns.Diplomacy;
        int events = 0;
        diplomacy.RelationChanged += (a, b, relation) =>
        {
            events++;
            Assert.That(diplomacy.GetRelation(a, b), Is.EqualTo(relation));
            Assert.That(diplomacy.GetRelation(b, a), Is.EqualTo(relation));
        };
        Assert.That(diplomacy.DeclareWar(player, enemy), Is.False);
        Assert.That(diplomacy.MakePeace(player, enemy), Is.True);
        Assert.That(diplomacy.MakePeace(enemy, player), Is.False);
        Assert.That(diplomacy.DeclareWar(enemy, player), Is.True);
        Assert.That(events, Is.EqualTo(2));
        Assert.That(diplomacy.Revision, Is.EqualTo(2));
    }

    [Test]
    public void InvalidTransitionsLeaveStateUnchanged()
    {
        var diplomacy = turns.Diplomacy;
        var stranger = Component<Player>();
        Assert.Throws<ArgumentException>(() => diplomacy.DeclareWar(player, player));
        Assert.Throws<ArgumentException>(() => diplomacy.MakePeace(player, player));
        Assert.Throws<ArgumentNullException>(() => diplomacy.MakePeace(player, null));
        Assert.Throws<ArgumentException>(() => diplomacy.DeclareWar(player, stranger));
        Assert.That(diplomacy.Revision, Is.Zero);
        Assert.That(diplomacy.IsAtWar(player, enemy), Is.True);
    }

    [Test]
    public void QueuedMoveIsDiscardedWhenDiplomacyChangesBeforeExecution()
    {
        var unit = Unit(player, Tile(0));
        var destination = Tile(1);
        var action = new CandidateAction { unit = unit, moveTile = destination, kind = ActionKind.MoveOnly };
        int revision = turns.Diplomacy.Revision;
        var execution = (System.Collections.IEnumerator)Call(ai, "Execute", action, revision);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(execution.MoveNext(), Is.False);
        Assert.That(unit.currentTile, Is.Not.SameAs(destination));
        Assert.That(unit.hasMoved, Is.False);
    }

    [Test]
    public void TransitionLeavesUnrelatedSiegeAndRelationIntact()
    {
        var third = Component<Player>();
        turns.players.Add(third);
        var unit = Unit(third, Tile(0));
        var city = City(unit.currentTile, enemy);
        city.SetPendingCapture(unit);
        turns.Diplomacy.MakePeace(player, enemy);
        Assert.That(city.pendingCapturer, Is.SameAs(unit));
        Assert.That(turns.Diplomacy.IsAtWar(third, enemy), Is.True);
    }

    [Test]
    public void AllDistinctPlayersStartAtWarInBothDirections()
    {
        var third = Component<Player>();
        turns.players.Add(third);
        var diplomacy = turns.Diplomacy;

        foreach (var a in turns.players)
        foreach (var b in turns.players)
        {
            Assert.That(diplomacy.GetRelation(a, b), Is.EqualTo(
                a == b ? DiplomaticRelation.Peace : DiplomaticRelation.War));
            Assert.That(diplomacy.IsAtWar(a, b), Is.EqualTo(a != b));
        }
    }

    [Test]
    public void UnownedEntitiesAreNotEnemies()
    {
        Assert.That(turns.Diplomacy.IsAtWar(player, null), Is.False);
        Assert.That(turns.Diplomacy.IsAtWar(null, player), Is.False);
        Assert.Throws<ArgumentNullException>(() => turns.Diplomacy.GetRelation(player, null));
    }

    [Test]
    public void RosterChangesDoNotRebuildMatchState()
    {
        var diplomacy = turns.Diplomacy;
        turns.players.Reverse();
        Assert.That(turns.Diplomacy, Is.SameAs(diplomacy));
        Assert.That(diplomacy.IsAtWar(player, enemy), Is.True);

        var newcomer = Component<Player>();
        turns.players.Add(newcomer);
        Assert.Throws<ArgumentException>(() => diplomacy.GetRelation(player, newcomer));
    }

    [Test]
    public void InvalidRostersAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new DiplomacyState(null));
        Assert.Throws<ArgumentException>(() => new DiplomacyState(new[] { player, player }));
        Assert.Throws<ArgumentException>(() => new DiplomacyState(new Player[] { null }));
    }
}
