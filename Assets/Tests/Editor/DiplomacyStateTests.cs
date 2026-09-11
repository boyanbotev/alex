using System;
using NUnit.Framework;

public class DiplomacyStateTests : TacticsTestFixture
{
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
