using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    [System.NonSerialized] public List<Player> players = new List<Player>();
    public int activePlayerIndex = 0;
    public int turnNumber = 1;

    public Player ActivePlayer => players[activePlayerIndex];
    public event System.Action TurnChanged;
    public TurnAI ai;
    public bool IsGameOver { get; private set; }
    private bool matchStarted;
    private bool checkEnding;
    public CombatSettings combatSettings;
    [Min(0)] public int neuronStarsPerConnection = 1;
    private NeuronNetwork neurons;
    public NeuronNetwork Neurons => neurons ??= new NeuronNetwork(this);
    [Header("City bonds")]
    [Min(0)] public int maxBondsPerCity = 2;
    [Min(0)] public int bondUpgradeCost = 6;
    [Min(0)] public int cityUnitCapacity = 2;
    private CityBondManager bonds;
    public CityBondManager Bonds => bonds ??= new CityBondManager(this);

    private DiplomacyState diplomacy;
    // The roster must be populated before first access and stays fixed for the match.
    public DiplomacyState Diplomacy
    {
        get
        {
            if (diplomacy == null)
            {
                diplomacy = new DiplomacyState(players);
                diplomacy.RelationChanged += OnRelationChanged;
            }
            return diplomacy;
        }
    }

    private void OnDestroy()
    {
        if (diplomacy != null) diplomacy.RelationChanged -= OnRelationChanged;
        if (Instance == this) Instance = null;
    }

    private void OnRelationChanged(Player a, Player b, DiplomaticRelation relation)
    {
        Neurons.Invalidate();
        if (WorldPopulationManager.Instance == null) return;
        foreach (City city in WorldPopulationManager.Instance.allCities)
        {
            if (city == null || (city.owner != a && city.owner != b)) continue;
            Player other = city.owner == a ? b : a;
            if (city.pendingCapturer != null && city.pendingCapturer.owner == other)
                city.ClearPendingCapture();

            Unit occupant = city.centerTile != null ? city.centerTile.currentUnit : null;
            if (relation == DiplomaticRelation.Peace)
            {
                if (occupant != null && occupant.owner == other)
                    UIManager.Instance?.HideCaptureButton(city);
            }
            else if (relation == DiplomaticRelation.War && occupant != null &&
                     occupant.isAlive && occupant.owner == other)
            {
                city.SetPendingCapture(occupant);
            }
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    public void InitializePlayers(Level level)
    {
        if (players.Count != 0 || diplomacy != null)
            throw new System.InvalidOperationException("The match roster is already initialized.");
        foreach (LevelFaction entry in level.factions)
        {
            Player player = new GameObject(entry.faction.name).AddComponent<Player>();
            player.transform.SetParent(transform, false);
            player.faction = entry.faction;
            player.factionName = entry.faction.name;
            player.factionColor = entry.color;
            player.isAI = entry.isAI;
            player.stars = entry.startingStars;
            players.Add(player);
        }
        activePlayerIndex = 0;
    }

    private IEnumerator Start()
    {
        while (GridGenerator.Instance == null || !GridGenerator.Instance.IsReady)
            yield return null;
        _ = Diplomacy;
        Neurons.Invalidate();
        matchStarted = true;
        if (CheckGameOver()) yield break;
        StartTurn(ActivePlayer);
    }

    private void LateUpdate()
    {
        if (!matchStarted || IsGameOver || !checkEnding) return;
        checkEnding = false;
        CheckGameOver();
    }

    public void RequestGameOverCheck() => checkEnding = true;

    public bool CheckGameOver()
    {
        if (IsGameOver) return true;
        if (!matchStarted) return false;
        if (players.Count == 1) return false;
        Player human = players.Find(p => !p.isAI);
        if (human != null && !human.IsAlive())
        {
            EndGame(false);
            return true;
        }
        int alive = 0;
        foreach (Player player in players) if (player.IsAlive()) alive++;
        if (alive > 1) return false;
        EndGame(human != null && human.IsAlive());
        return true;
    }

    // Also used by a future turn limit, after its final turn has resolved.
    public void EndGame(bool won)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        StopAllCoroutines();
        Bonds.Refresh();
        var links = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (CityBond bond in Bonds.All)
            if (bond.Active) links.Add(GameEnding.PairKey(bond.a.cityName, bond.b.cityName));
        UIManager.Instance?.CloseSpawnPanel();
        UIManager.Instance?.CloseBuildPanel();
        UIManager.Instance?.HideAllCaptureButtons();
        TurnChanged?.Invoke();
        gameObject.AddComponent<GameEnding>().Show(GameManager.Instance.Level.endingStory, links, won);
    }

    public void EndTurn()
    {
        if (IsGameOver || GridGenerator.Instance == null || !GridGenerator.Instance.IsReady) return;
        UIManager.Instance?.CloseSpawnPanel();
        if (CheckGameOver()) return;

        HealUnusedUnits(ActivePlayer);

        do
        {
            activePlayerIndex = (activePlayerIndex + 1) % players.Count;
            if (activePlayerIndex == 0) turnNumber++;
        }
        while (!ActivePlayer.IsAlive());

        TurnChanged?.Invoke();
        StartTurn(ActivePlayer);
    }

    private void StartTurn(Player player)
    {
        int income = player.CalculateTurnIncome();
        player.AddStars(income);

        foreach (var unit in player.units)
            unit.ResetTurn();

        ResolvePendingCaptures(player);

        if (CheckGameOver()) return;

        if (player.isAI)
            StartCoroutine(RunAITurn(player));
    }

    void HealUnusedUnits(Player player)
    {
        foreach (var unit in player.units)
        {
            if (!unit.hasMoved && !unit.hasAttacked)
            {
                unit.Heal();
            }
        }
    }

    private void ResolvePendingCaptures(Player player)
    {
        foreach (City city in WorldPopulationManager.Instance.allCities)
        {
            if (!city.HasPendingCapture)
                continue;

            if (city.pendingCapturer.owner != player)
                continue;

            if (player.isAI)
            {
                Debug.Log($"{player.faction.name} capture {city.cityName} without showing button");
                city.ResolvePendingCapture(false);
                if (CheckGameOver()) return;
            }
            else
            {
                Debug.Log($"{player.faction.name} capture {city.cityName} showing button");
                city.ResolvePendingCapture(true);
            }
        }
    }

    private IEnumerator RunAITurn(Player player)
    {
        yield return ai.PlayTurn(player);
        EndTurn();
    }
}
