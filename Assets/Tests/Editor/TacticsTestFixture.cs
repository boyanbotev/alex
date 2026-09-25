using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public abstract class TacticsTestFixture
{
    private readonly List<Object> objects = new();
    private GridManager previousGrid;
    private TurnManager previousTurns;
    private WorldPopulationManager previousPopulation;
    private int liveCheckpoint;
    protected GridManager grid;
    protected TurnManager turns;
    protected WorldPopulationManager population;
    protected Player player;
    protected Player enemy;
    protected AIProfile profile;
    protected TacticsAI ai;
    protected BoardState board;
    protected TacticalScorer scorer;
    protected CandidateGenerator generator;

    [SetUp]
    public void SetUp()
    {
        previousGrid = GridManager.Instance;
        previousTurns = TurnManager.Instance;
        previousPopulation = WorldPopulationManager.Instance;
        liveCheckpoint = BoardState.Live.Checkpoint();
        grid = Component<GridManager>();
        turns = Component<TurnManager>();
        turns.combatSettings = Asset<CombatSettings>();
        turns.combatSettings.cavalryKillerBonus = 1;
        turns.combatSettings.spearWallBonus = 1;
        population = Component<WorldPopulationManager>();
        GridManager.Instance = grid;
        TurnManager.Instance = turns;
        WorldPopulationManager.Instance = population;
        player = Component<Player>();
        enemy = Component<Player>();
        turns.players.AddRange(new[] { player, enemy });
        profile = Asset<AIProfile>();
        ai = Component<TacticsAI>();
        Call(ai, "Configure", player, profile);
        scorer = new TacticalScorer();
        scorer.Configure(profile, turns.players, population.allCities);
        generator = new CandidateGenerator();
        generator.Configure(grid, scorer);
        board = new BoardState();
    }

    [TearDown]
    public void TearDown()
    {
        BoardState.Live.Rollback(liveCheckpoint);
        for (int i = objects.Count - 1; i >= 0; i--)
            Object.DestroyImmediate(objects[i]);
        objects.Clear();
        GridManager.Instance = previousGrid;
        TurnManager.Instance = previousTurns;
        WorldPopulationManager.Instance = previousPopulation;
    }

    protected T Component<T>() where T : Component
    {
        var go = new GameObject(typeof(T).Name);
        go.SetActive(false); // Avoid scene lifecycle and rendering dependencies.
        objects.Add(go);
        return go.AddComponent<T>();
    }

    protected T Asset<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        objects.Add(asset);
        return asset;
    }

    protected Tile Tile(int x, int y = 0)
    {
        var tile = Component<Tile>();
        tile.gridPosition = new Vector2Int(x, y);
        grid.grid.Add(tile.gridPosition, tile);
        return tile;
    }

    protected Unit Unit(Player owner, Tile tile)
    {
        var unit = Component<Unit>();
        unit.owner = owner;
        unit.currentTile = tile;
        unit.data = Asset<UnitData>();
        unit.data.skills = System.Array.Empty<Skill>();
        unit.currentHealth = unit.data.maxHealth;
        unit.isAlive = unit.isActive = true;
        tile.currentUnit = unit;
        owner.units.Add(unit);
        return unit;
    }

    protected City City(Tile tile, Player owner = null)
    {
        var city = Component<City>();
        city.centerTile = tile;
        city.owner = owner;
        tile.city = city;
        population.allCities.Add(city);
        return city;
    }

    protected static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    protected void Simulate(CandidateAction action) => ActionSimulator.Apply(board, action);

    protected List<CandidateAction> Candidates(Unit unit)
    {
        var result = new List<CandidateAction>();
        generator.Generate(unit.owner, board);
        foreach (var candidate in generator.Candidates)
            if (candidate.unit == unit) result.Add(candidate);
        return result;
    }
}
