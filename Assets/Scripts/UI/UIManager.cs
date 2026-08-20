using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [SerializeField] RectTransform spawnButtonHolder;
    [SerializeField] RectTransform spawnPanel;
    [SerializeField] RectTransform buildPanel;
    [SerializeField] RectTransform buildButtonHolder;
    [SerializeField] RectTransform techPanel;
    [SerializeField] RectTransform techButtonHolder;
    [SerializeField] TextMeshProUGUI starsCounter;

    [SerializeField] GameObject spawnButtonPrefab;
    public static UIManager Instance;

    private void Awake()
    {
        Instance = this;
        spawnPanel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        Player.OnUpdateStars += SetStars;
    }

    private void OnDisable()
    {
        Player.OnUpdateStars -= SetStars;
    }

    /// <summary>
    /// Programmatically create spawn buttons
    /// based on the available units
    /// </summary>
    /// <param name="action"></param>
    public void ShowSpawnButtons(FactionUnit[] availableUnits, City city)
    {
        if (spawnPanel.gameObject.activeSelf) return;

        spawnPanel.gameObject.SetActive(true);

        foreach (FactionUnit unit in availableUnits) {
            var button = Instantiate(spawnButtonPrefab, spawnButtonHolder);
            Button buttonComponent = button.GetComponent<Button>();
            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();

            buttonText.text = "Spawn " + unit.unitData.name;

            buttonComponent.onClick.AddListener(() => {
                city.SpawnUnit(unit.prefab, unit.unitData.cost);
                CloseSpawnPanel();
            });
        }
    }

    public void ShowBuildButtons(BuildingData[] availableBuildings, Tile tile, City city)
    {
        if (buildPanel.gameObject.activeSelf) return;

        buildPanel.gameObject.SetActive(true);

        foreach (BuildingData building in availableBuildings)
        {
            var button = Instantiate(spawnButtonPrefab, buildButtonHolder);
            Button buttonComponent = button.GetComponent<Button>();
            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();

            buttonText.text = "Build " + building.name;

            buttonComponent.onClick.AddListener(() => {
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

            var button = Instantiate(spawnButtonPrefab, techButtonHolder);
            Button buttonComponent = button.GetComponent<Button>();
            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();

            buttonText.text = "Research " + tech.techName;

            buttonComponent.onClick.AddListener(() => {
                player.techState.TryResearch(tech, player);
                CloseTechPanel();
            });
        }
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
