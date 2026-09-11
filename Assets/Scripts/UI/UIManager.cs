using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] RectTransform spawnButtonHolder;
    [SerializeField] RectTransform spawnPanel;
    [SerializeField] RectTransform cantSpawnText;
    [SerializeField] TextMeshProUGUI cityNameAndLevelText;
    [Header("Build")]
    [SerializeField] RectTransform buildPanel;
    [SerializeField] RectTransform buildButtonHolder;
    [Header("Tech")]
    [SerializeField] RectTransform techPanel;
    [SerializeField] RectTransform techButtonHolder;
    [SerializeField] RectTransform techPurchasePanel;
    [SerializeField] TextMeshProUGUI techTitle;
    [SerializeField] RectTransform techUnlocksButtonHolder;
    [SerializeField] RectTransform cantResearchText;
    [SerializeField] Button researchButton;
    [SerializeField] BuildingInfoPopup buildingInfoPopup;
    [SerializeField] UnitStatsPopup unitStatsPopup;
    [Header("Stars")]
    [SerializeField] TextMeshProUGUI starsCounter;
    [SerializeField] TextMeshProUGUI starsPerTurnCounter;

    [SerializeField] GameObject itemPurchaseButtonPrefab;

    [Header("Capture UI")]
    [SerializeField] RectTransform captureButtonHolder;
    [SerializeField] GameObject captureButtonPrefab;

    public static UIManager Instance;
    private TechTreeView techTree;
    private TechData selectedTech;
    private Player techPlayer;

    private readonly Dictionary<City, GameObject> captureButtons = new Dictionary<City, GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    private IEnumerator Start()
    {
        while (GridGenerator.Instance == null || !GridGenerator.Instance.IsReady)
            yield return null;
        SetStarsPerTurn(TurnManager.Instance.players.Find(p => !p.isAI).CalculateTurnIncome());
        PrepareTechTree(TurnManager.Instance.players.Find(p => !p.isAI));
    }

    private void OnEnable()
    {
        Player.OnUpdateStars += SetStars;
        City.OnPlayerChange += OnClaim;
        City.OnSiege += OnSiege;
        City.OnUnsiege += OnUnsiege;
        City.OnLevelUp += OnLevelUp;
    }

    private void OnDisable()
    {
        Player.OnUpdateStars -= SetStars;
        City.OnPlayerChange -= OnClaim;
        City.OnSiege -= OnSiege;
        City.OnUnsiege -= OnUnsiege;
        City.OnLevelUp -= OnLevelUp;
    }

    public void ShowSpawnButtons(FactionUnit[] availableUnits, City city)
    {
        if (spawnPanel.gameObject.activeSelf) return;

        spawnPanel.gameObject.SetActive(true);

        foreach (FactionUnit unit in availableUnits) {
            var button = Instantiate(itemPurchaseButtonPrefab, spawnButtonHolder);

            ItemPurchaseButton itemPurchaseButton = button.GetComponent<ItemPurchaseButton>();

            itemPurchaseButton.AddText("Spawn " + unit.unitData.name);
            itemPurchaseButton.AddCost(unit.unitData.cost);
            itemPurchaseButton.AddListener(() => {
                city.SpawnUnit(unit, unit.unitData.cost);
                CloseSpawnPanel();
            });
        }
        cityNameAndLevelText.text = $"City lvl {city.level}";
        cantSpawnText.gameObject.SetActive(city.units.Count > city.level);
    }

    public void ShowCityInfo(City city)
    {
        if (spawnPanel.gameObject.activeSelf) return;

        spawnPanel.gameObject.SetActive(true);

        cityNameAndLevelText.text = $"City lvl {city.level}";
        cantSpawnText.gameObject.SetActive(city.units.Count > city.level);
    }

    public void ShowBuildButtons(BuildingData[] availableBuildings, Tile tile, City city)
    {
        if (buildPanel.gameObject.activeSelf) return;

        buildPanel.gameObject.SetActive(true);

        foreach (BuildingData building in availableBuildings)
        {
            var button = Instantiate(itemPurchaseButtonPrefab, buildButtonHolder);
            ItemPurchaseButton itemPurchaseButton = button.GetComponent<ItemPurchaseButton>();

            itemPurchaseButton.AddText("Build " + building.name);
            itemPurchaseButton.AddCost(building.cost);
            itemPurchaseButton.AddListener(() => {
                city.PlaceBuilding(building, tile);
                CloseBuildPanel();
            });
        }
    }

    public void ShowTechButtons()
    {
        if (techPanel.gameObject.activeSelf) return;

        var player = TurnManager.Instance.ActivePlayer;

        if (player.isAI) return;

        techPanel.gameObject.SetActive(true);
        PrepareTechTree(player);
    }

    private void PrepareTechTree(Player player)
    {
        if (player == null) return;
        if (techTree == null)
        {
            techTree = techButtonHolder.gameObject.AddComponent<TechTreeView>();
            techTree.Initialize(techButtonHolder, itemPurchaseButtonPrefab, ShowTechPurchasePanel);
        }
        techTree.Show(player);
    }

    public void ShowTechPurchasePanel(TechData tech, Player player)
    {
        if (techPurchasePanel.gameObject.activeSelf) CloseTechPurchasePanel();
        selectedTech = tech;
        techPlayer = player;
        techPurchasePanel.gameObject.SetActive(true);
        techTitle.text = tech.techName;

        foreach (BuildingData building in player.faction.availableBuildings)
        {
            if (building.requiredTech != tech) continue;
            BuildingData b = building; // local copy for the closure
            CreateUnlockCard(b.buildingName, b.cost, () => ShowBuildingInfoPopup(b));
        }

        foreach (FactionUnit unit in player.faction.availableUnits)
        {
            if (unit.unitData.requiredTech != tech) continue;
            UnitData u = unit.unitData;
            CreateUnlockCard(u.name, u.cost, () => ShowUnitStatsPopup(u));
        }

        researchButton.onClick.RemoveAllListeners();
        researchButton.onClick.AddListener(() => {
            player.techState.TryResearch(tech, player);
            CloseTechPurchasePanel();
            techTree.RefreshState();
        });

        RefreshResearchState();
    }

    private void RefreshResearchState()
    {
        if (selectedTech == null || techPlayer == null) return;
        researchButton.interactable = techPlayer.techState.CanResearch(selectedTech) && selectedTech.cost <= techPlayer.stars;
        cantResearchText.gameObject.SetActive(!techPlayer.techState.IsUnlocked(selectedTech) && selectedTech.cost > techPlayer.stars);
    }

    private void CreateUnlockCard(string itemName, int cost, UnityAction onClick)
    {
        var button = Instantiate(itemPurchaseButtonPrefab, techUnlocksButtonHolder);
        ItemPurchaseButton card = button.GetComponent<ItemPurchaseButton>();
        card.AddText(itemName);
        card.AddCost(cost);
        card.AddListener(onClick);
    }

    public void CloseTechPurchasePanel()
    {
        for (int i = 0; i < techUnlocksButtonHolder.childCount; i++)
        {
            Destroy(techUnlocksButtonHolder.GetChild(i).gameObject);
        }

        HideBuildingInfoPopup();
        HideUnitStatsPopup();

        techPurchasePanel.gameObject.SetActive(false);
        selectedTech = null;
        techPlayer = null;
    }

    public void ShowCaptureButton(City city, Unit capturer)
    {
        if (city == null || capturer == null) return;

        if (!capturer.isAlive || capturer.currentTile != city.centerTile) return;

        if (captureButtons.ContainsKey(city)) return;

        GameObject button = Instantiate(captureButtonPrefab, captureButtonHolder);

        captureButtons[city] = button;

        Button buttonComponent = button.GetComponent<Button>();
        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonText != null) buttonText.text = "Capture";

        buttonComponent.onClick.AddListener(() =>
        {
            if (!capturer.isAlive ||
                capturer.currentTile != city.centerTile)
            {
                HideCaptureButton(city);
                return;
            }

            city.Capture(capturer);
            HideCaptureButton(city);
        });

        button.transform.position = city.centerTile.transform.position + new Vector3(0f, 1f, 0f);
    }

    public void HideCaptureButton(City city)
    {
        if (city == null) return;

        if (captureButtons.TryGetValue(city, out GameObject button))
        {
            if (button != null)
                Destroy(button);

            captureButtons.Remove(city);
        }
    }

    public void HideAllCaptureButtons()
    {
        foreach (GameObject button in captureButtons.Values)
        {
            if (button != null)
                Destroy(button);
        }

        captureButtons.Clear();
    }

    public void CloseBuildPanel()
    {
        for (int i = 0; i < buildButtonHolder.childCount; i++)
        {
            Destroy(buildButtonHolder.GetChild(i).gameObject);
        }
        buildPanel.gameObject.SetActive(false);
    }

    public void CloseSpawnPanel()
    {
        for (int i = 0; i < spawnButtonHolder.childCount; i++)
        {
            Destroy(spawnButtonHolder.GetChild(i).gameObject);
        }
        spawnPanel.gameObject.SetActive(false);
    }

    public void CloseTechPanel()
    {
        if (techPurchasePanel.gameObject.activeSelf) CloseTechPurchasePanel();
        techPanel.gameObject.SetActive(false);
    }

    public void ShowBuildingInfoPopup(BuildingData building)
    {
        buildingInfoPopup.gameObject.SetActive(true);
        buildingInfoPopup.Populate(building);
    }

    public void HideBuildingInfoPopup()
    {
        buildingInfoPopup.gameObject.SetActive(false);
    }

    public void ShowUnitStatsPopup(UnitData unit)
    {
        unitStatsPopup.gameObject.SetActive(true);
        unitStatsPopup.Populate(unit);
    }

    public void HideUnitStatsPopup()
    {
        unitStatsPopup.gameObject.SetActive(false);
    }

    public void SetStars(int value)
    {
        starsCounter.text = value + " stars";
        if (techTree != null) techTree.RefreshState();
        RefreshResearchState();
    }

    public void SetStarsPerTurn(int value)
    {
        starsPerTurnCounter.text = $"(+{value})";
    }

    public void OnSiege(Player siegedPlayer)
    {
        if (siegedPlayer && !siegedPlayer.isAI)
        {
            SetStarsPerTurn(siegedPlayer.CalculateTurnIncome());
        }
    }

    public void OnUnsiege(Player unsiegedPlayer)
    {
        if (unsiegedPlayer && !unsiegedPlayer.isAI)
        {
            SetStarsPerTurn(unsiegedPlayer.CalculateTurnIncome());
        }
    }

    public void OnClaim(Player claimingPlayer)
    {
        if (!claimingPlayer.isAI)
        {
            SetStarsPerTurn(claimingPlayer.CalculateTurnIncome());
        }
    }

    public void OnLevelUp(Player player)
    {
        if (!player.isAI)
        {
            SetStarsPerTurn(player.CalculateTurnIncome());
        }
    }
}
