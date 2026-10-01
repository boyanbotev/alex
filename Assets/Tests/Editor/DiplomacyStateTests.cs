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

}
