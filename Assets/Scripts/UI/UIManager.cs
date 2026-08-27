using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] RectTransform spawnButtonHolder;
    [SerializeField] RectTransform spawnPanel;
    [SerializeField] RectTransform cantSpawnText;
    [SerializeField] RectTransform buildPanel;
    [SerializeField] RectTransform buildButtonHolder;
    [SerializeField] RectTransform techPanel;
    [SerializeField] RectTransform techButtonHolder;
    [SerializeField] TextMeshProUGUI starsCounter;

    [SerializeField] GameObject itemPurchaseButtonPrefab;

    [Header("Capture UI")]
    [SerializeField] RectTransform captureButtonHolder;
    [SerializeField] GameObject captureButtonPrefab;

    public static UIManager Instance;

    private readonly Dictionary<City, GameObject> captureButtons = new Dictionary<City, GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        Player.OnUpdateStars += SetStars;
    }

    private void OnDisable()
    {
        Player.OnUpdateStars -= SetStars;
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
        var availableTech = player.faction.availableTech;

        if (player.isAI) return;

        techPanel.gameObject.SetActive(true);

        foreach (TechData tech in availableTech)
        {
            if (!player.techState.CanResearch(tech)) continue;

            var button = Instantiate(itemPurchaseButtonPrefab, techButtonHolder);

            ItemPurchaseButton itemPurchaseButton = button.GetComponent<ItemPurchaseButton>();

            itemPurchaseButton.AddText("Research " + tech.techName);
            itemPurchaseButton.AddCost(tech.cost);
            itemPurchaseButton.AddListener(() => {
                player.techState.TryResearch(tech, player);
                CloseTechPanel();
            });
        }
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
        for (int i = 0; i < techButtonHolder.childCount; i++)
        {
            Destroy(techButtonHolder.GetChild(i).gameObject);
        }
        techPanel.gameObject.SetActive(false);
    }

    public void SetStars(int value)
    {
        starsCounter.text = value + " stars";
    }
}
