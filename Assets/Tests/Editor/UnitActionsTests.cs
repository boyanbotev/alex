using System.Collections;
using NUnit.Framework;

public class UnitActionsTests : TacticsTestFixture
{
    private FogOfWarManager previousFog;

    [SetUp]
    public void SetUpActions()
    {
        previousFog = FogOfWarManager.Instance;
        FogOfWarManager.Instance = Component<FogOfWarManager>();
        player.visibleTiles = new VisibilityState(20, 20);
    }

    [TearDown]
    public void TearDownActions() => FogOfWarManager.Instance = previousFog;

    [TestCase(1, 1, 4, 8)]
    [TestCase(2, 1, 7, 7)]
    [TestCase(1, 2, 5, 5)]
    [TestCase(2, 2, 8, 4)]
    public void MatchupSkillsAffectPreviewCombatAndCityDefense(int speed, int range, int damage, int retaliation)
    {
        var attacker = Unit(player, Tile(0));
        var defender = Unit(enemy, Tile(1));
        attacker.data.skills = new[] { Skill.CavalryKiller };
        attacker.data.attackRange = range;
        defender.data.skills = new[] { Skill.SpearWall };
        defender.data.moveRange = speed;
        turns.Diplomacy.DeclareWar(player, enemy);

        Assert.That(attacker.PredictAttackDamage(defender), Is.EqualTo((damage, retaliation)));
        Assert.That(CombatMath.PredictDamage(attacker, defender, board), Is.EqualTo((damage, retaliation)));
        Assert.That(CityDefense.RemainingHealth(defender.data, 10, defender.currentTile, enemy, board),
            Is.EqualTo(10 - damage));
        Assert.That(attacker.Attack(defender), Is.True);
        Assert.That(defender.currentHealth, Is.EqualTo(10 - damage));
        Assert.That(attacker.currentHealth, Is.EqualTo(10 - retaliation));
        Assert.That(attacker.data.attackPower, Is.EqualTo(2));
        Assert.That(defender.data.defensePower, Is.EqualTo(2));
    }

    [Test]
    public void CavalryKillerIgnoresTerritoryMovementBonus()
    {
        var attacker = Unit(player, Tile(0));
        var defender = Unit(enemy, Tile(1));
        attacker.data.skills = new[] { Skill.CavalryKiller };
        var city = City(defender.currentTile, enemy);
        city.data = Asset<CityData>();
        city.data.perk = Asset<CityPerkData>();
        city.data.perk.kind = CityPerkKind.Adrenaline;
        city.data.perk.amount = 1;

        Assert.That(board.GetMoveRange(defender), Is.EqualTo(2));
        Assert.That(CombatMath.PredictDamage(attacker, defender, board), Is.EqualTo((5, 5)));
    }

    [Test]
    public void MatchupSkillsStackWithDefenseBonusesButDoNotApplyInReverseRoles()
    {
        var attacker = Asset<UnitData>();
        var defender = Asset<UnitData>();
        attacker.skills = new[] { Skill.SpearWall };
        defender.skills = new[] { Skill.CavalryKiller };
        attacker.moveRange = 2;
        Assert.That(CombatMath.CalculateDamage(attacker, 10, defender, 10, 2), Is.EqualTo((5, 5)));

        defender.skills = new[] { Skill.SpearWall };
        Assert.That(CombatMath.CalculateDamage(attacker, 10, defender, 10, 3), Is.EqualTo((3, 12)));
    }

    [TestCase("occupied")]
    [TestCase("range")]
    [TestCase("moved")]
    [TestCase("attacked")]
    [TestCase("inactive")]
    [TestCase("dead")]
    [TestCase("turn")]
    [TestCase("blocked")]
    public void InvalidMoveDoesNotChangeOccupancyOrActionFlags(string reason)
    {
        var origin = Tile(0);
        var unit = Unit(player, origin);
        var middle = Tile(1);
        var destination = Tile(2);
        unit.data.moveRange = 2;
        switch (reason)
        {
            case "occupied": Unit(player, destination); break;
            case "range": unit.data.moveRange = 1; break;
            case "moved": unit.hasMoved = true; break;
            case "attacked": unit.hasAttacked = true; break;
            case "inactive": unit.isActive = false; break;
            case "dead": unit.isAlive = false; break;
            case "turn": turns.activePlayerIndex = 1; break;
            case "blocked": middle.terrainType = TerrainType.Mountain; break;
        }
        var occupant = destination.currentUnit;
        bool moved = unit.hasMoved, attacked = unit.hasAttacked;
        Assert.That(unit.MoveTo(destination), Is.False);
        Assert.That(unit.currentTile, Is.SameAs(origin));
        Assert.That(origin.currentUnit, Is.SameAs(unit));
        Assert.That(destination.currentUnit, Is.SameAs(occupant));
        Assert.That(unit.hasMoved, Is.EqualTo(moved));
        Assert.That(unit.hasAttacked, Is.EqualTo(attacked));
    }

    [TestCase("range")]
    [TestCase("occupied")]
    [TestCase("static")]
    [TestCase("peace")]
    [TestCase("deadTarget")]
    [TestCase("attacked")]
    [TestCase("turn")]
    public void InvalidQueuedAttackIsRejectedBeforeMoving(string reason)
    {
        var attacker = Unit(player, Tile(0));
        var from = Tile(1);
        var target = Unit(enemy, Tile(2));
        switch (reason)
        {
            case "range": attacker.data.attackRange = 0; break;
            case "occupied": Unit(player, from); break;
            case "static": attacker.data.skills = new[] { Skill.Static }; break;
            case "peace": turns.Diplomacy.MakePeace(player, enemy); break;
            case "deadTarget": target.isAlive = false; break;
            case "attacked": attacker.hasAttacked = true; break;
            case "turn": turns.activePlayerIndex = 1; break;
        }
        var action = new CandidateAction { unit = attacker, moveTile = from, target = target, kind = ActionKind.Attack };
        Assert.That(attacker.Attack(target, from), Is.False);
        var execution = (IEnumerator)Call(ai, "Execute", action, turns.Diplomacy.Revision);
        Assert.That(execution.MoveNext(), Is.False);
        Assert.That(attacker.currentTile.gridPosition.x, Is.Zero);
        Assert.That(attacker.hasMoved, Is.False);
        Assert.That(target.currentHealth, Is.EqualTo(target.data.maxHealth));
    }

    [TestCase(false, "meleeKill")]
    [TestCase(true, "meleeKill")]
    [TestCase(false, "moveAndKill")]
    [TestCase(true, "moveAndKill")]
    [TestCase(false, "rangedKill")]
    [TestCase(true, "rangedKill")]
    [TestCase(false, "retaliation")]
    [TestCase(true, "retaliation")]
    [TestCase(false, "staticKill")]
    [TestCase(true, "staticKill")]
    [TestCase(false, "attackerDies")]
    [TestCase(true, "attackerDies")]
    public void LiveAndSimulatedCombatAgreeForBothCallers(bool useAI, string scenario)
    {
        var origin = Tile(0);
        var attacker = Unit(player, origin);
        var from = scenario == "moveAndKill" ? Tile(1) : origin;
        var targetTile = Tile(scenario == "moveAndKill" || scenario == "rangedKill" ? 2 : 1);
        var city = City(targetTile, enemy);
        var target = Unit(enemy, targetTile);
        if (scenario == "rangedKill") attacker.data.attackRange = 2;
        if (scenario == "staticKill") attacker.data.skills = new[] { Skill.Static };
        if (scenario == "attackerDies") attacker.currentHealth = 1;
        else if (scenario != "retaliation") target.currentHealth = 1;
        var action = new CandidateAction { unit = attacker, moveTile = from, target = target, kind = ActionKind.Attack };
        ActionSimulator.Apply(board, action);
        var expectedTile = board.GetTile(attacker);
        int expectedHealth = board.GetHealth(attacker), expectedTargetHealth = board.GetHealth(target);
        bool expectedAlive = board.IsAlive(attacker), expectedTargetAlive = board.IsAlive(target);
        bool expectedActive = board.IsActive(attacker), expectedCapture = board.HasPendingCityCapture(city);
        board.Rollback(0);

        if (useAI)
        {
            var execution = (IEnumerator)Call(ai, "Execute", action, turns.Diplomacy.Revision);
            Assert.That(execution.MoveNext(), Is.True);
        }
        else Assert.That(attacker.Attack(target, from), Is.True);

        Assert.That(player.units.Contains(attacker), Is.EqualTo(expectedAlive));
        Assert.That(enemy.units.Contains(target), Is.EqualTo(expectedTargetAlive));
        if (expectedAlive)
        {
            Assert.That(attacker.currentTile, Is.SameAs(expectedTile));
            Assert.That(expectedTile.currentUnit, Is.SameAs(attacker));
            Assert.That(attacker.currentHealth, Is.EqualTo(expectedHealth));
            Assert.That(attacker.hasMoved && attacker.hasAttacked, Is.True);
            Assert.That(attacker.isActive, Is.EqualTo(expectedActive));
            Assert.That(attacker.MoveTo(origin), Is.False, "An attack must consume normal movement.");
        }
        else Assert.That(origin.currentUnit, Is.Null);
        if (expectedTargetAlive) Assert.That(target.currentHealth, Is.EqualTo(expectedTargetHealth));
        Assert.That(city.HasPendingCapture, Is.EqualTo(expectedCapture));
    }

    [Test]
    public void MovingAwayClearsPendingCaptureAndCannotMoveTwice()
    {
        var origin = Tile(0);
        var city = City(origin, enemy);
        var unit = Unit(player, origin);
        city.SetPendingCapture(unit);
        var destination = Tile(1);
        Assert.That(unit.MoveTo(destination), Is.True);
        Assert.That(city.pendingCapturer, Is.Null);
        Assert.That(origin.currentUnit, Is.Null);
        Assert.That(destination.currentUnit, Is.SameAs(unit));
        Assert.That(unit.MoveTo(origin), Is.False);
    }
}
