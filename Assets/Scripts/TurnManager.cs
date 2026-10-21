using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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
    [SerializeField] private UnityEngine.UI.Button endTurnButton;
    private bool enemyTurnsInProgress;
    public bool IsGameOver { get; private set; }
    private bool matchStarted;
    private bool checkEnding;
    public CombatSettings combatSettings;
    public UnitAnimationSettings unitAnimationSettings;
    private Tween actionAnimation;
    public bool IsAnimatingAction => actionAnimation != null && actionAnimation.IsActive() && !actionAnimation.IsComplete();

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

    public void TrackActionAnimation(Tween animation)
    {
        actionAnimation = animation;
        if (endTurnButton != null) endTurnButton.interactable = false;
        animation.OnKill(() =>
        {
            actionAnimation = null;
            if (endTurnButton != null) endTurnButton.interactable = !enemyTurnsInProgress && !IsGameOver;
        });
    }

    private void OnDestroy()
    {
        if (enemyTurnsInProgress) WorldLoadingOverlay.Hide();
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
            player.isAI = entry.isAI;
            player.desiredConnections = entry.desiredConnections ?? System.Array.Empty<DesiredConnection>();
            player.stars = entry.startingStars;
            players.Add(player);
        }
        activePlayerIndex = 0;
        // Apply campaign defaults before the board is populated or match input begins.
        var initialDiplomacy = new DiplomacyState(players);
        foreach (var relation in SaveManager.Campaign.relations)
        {
            Player a = players.Find(p => p.faction.CampaignId == relation.a);
            Player b = players.Find(p => p.faction.CampaignId == relation.b);
            if (a == null || b == null || a == b) continue;
            switch (relation.value)
            {
                case DiplomaticRelation.Peace: initialDiplomacy.MakePeace(a, b); break;
                case DiplomaticRelation.Allied: initialDiplomacy.MakeAlliance(a, b); break;
                case DiplomaticRelation.War: initialDiplomacy.DeclareWar(a, b); break;
            }
        }
        diplomacy = initialDiplomacy;
        diplomacy.RelationChanged += OnRelationChanged;
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
        if (!matchStarted || IsGameOver || !checkEnding || IsAnimatingAction) return;
        checkEnding = false;
        CheckGameOver();
    }

    public void RequestGameOverCheck() => checkEnding = true;

    public bool CheckGameOver()
    {
        if (IsGameOver) return true;
        if (IsAnimatingAction) return false;
        if (!matchStarted) return false;
        Player human = players.Find(p => !p.isAI);
        if (human != null && !human.IsAlive())
        {
            EndGame();
            return true;
        }
        return false;
    }

    public void EndGame()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        SetEnemyTurnsInProgress(false);
        if (endTurnButton != null) endTurnButton.interactable = false;
        StopAllCoroutines();
        Bonds.Refresh();
        var links = new SortedSet<string>(System.StringComparer.Ordinal);
        var cityPairs = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (CityBond bond in Bonds.All)
        {
            if (!bond.Active) continue;
            links.Add(LevelEnding.PairKey(bond.a.cityName, bond.b.cityName));
            string a = bond.a.cityName, b = bond.b.cityName;
            cityPairs.Add(System.StringComparer.Ordinal.Compare(a, b) <= 0 ? $"{a} — {b}" : $"{b} — {a}");
        }
        UIManager.Instance?.CloseSpawnPanel();
        UIManager.Instance?.CloseBuildPanel();
        UIManager.Instance?.HideAllCaptureButtons();
        TurnChanged?.Invoke();
        var factionScores = GetFactionSuccess();
        var scores = new Dictionary<string, float>();
        foreach (var score in factionScores)
            if (!string.IsNullOrWhiteSpace(score.Key.endingSuccessFunction))
                scores.TryAdd(score.Key.endingSuccessFunction, score.Value);
        var campaign = SaveManager.Campaign;
        campaign.cityLinks = new List<string>(links);
        campaign.scores.Clear();
        foreach (var score in scores) campaign.scores.Add(new CampaignScore { key = score.Key, value = score.Value });
        // Results become durable when the player leaves the ending for the interlude.
        gameObject.AddComponent<LevelEnding>().Show(GameManager.Instance.Level.endingStory, links, scores,
            cityPairs, factionScores);
    }

    private Dictionary<Faction, float> GetFactionSuccess()
    {
        var scores = new Dictionary<Faction, float>();
        foreach (LevelFaction entry in GameManager.Instance.Level.factions)
            scores.TryAdd(entry.faction, 0f);

        int total = 0;
        foreach (City city in WorldPopulationManager.Instance.allCities)
        {
            if (city == null) continue;
            total++;
            Faction faction = city.owner != null ? city.owner.faction : null;
            if (faction != null && scores.ContainsKey(faction)) scores[faction]++;
        }

        if (total > 0)
            foreach (Faction faction in new List<Faction>(scores.Keys))
                scores[faction] /= total;
        return scores;
    }

    public void EndTurn()
    {
        if (!matchStarted || enemyTurnsInProgress || ActivePlayer.isAI || IsAnimatingAction) return;
        AdvanceTurn();
    }

    private void AdvanceTurn()
    {
        if (IsGameOver || GridGenerator.Instance == null || !GridGenerator.Instance.IsReady) return;
        UIManager.Instance?.CloseSpawnPanel();
        if (CheckGameOver()) return;

        HealUnusedUnits(ActivePlayer);

        do
        {
            activePlayerIndex = (activePlayerIndex + 1) % players.Count;
            if (activePlayerIndex == 0)
            {
                if (turnNumber >= GameManager.Instance.Level.turnLimit)
                {
                    EndGame();
                    return;
                }
                turnNumber++;
            }
        }
        while (!ActivePlayer.IsAlive());

        TurnChanged?.Invoke();
        StartTurn(ActivePlayer);
    }

    private void StartTurn(Player player)
    {
        SetEnemyTurnsInProgress(player.isAI);
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
        // Give the panel a frame to render before AI work begins.
        yield return null;
        yield return ai.PlayTurn(player);
        AdvanceTurn();
    }

    private void SetEnemyTurnsInProgress(bool inProgress)
    {
        if (endTurnButton != null) endTurnButton.interactable = !inProgress && !IsGameOver;
        if (enemyTurnsInProgress == inProgress) return;
        enemyTurnsInProgress = inProgress;
        if (inProgress) WorldLoadingOverlay.Show("Enemy turns in progress", compact: true);
        else WorldLoadingOverlay.Hide();
    }
}
