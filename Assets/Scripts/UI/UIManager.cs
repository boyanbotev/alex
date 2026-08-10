using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [SerializeField] RectTransform spawnButtonHolder;
    [SerializeField] RectTransform spawnPanel;
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

    public void CloseSpawnPanel()
    {
        for (int i = 0; i < spawnButtonHolder.childCount; i++)
        {
            Destroy(spawnButtonHolder.GetChild(i).gameObject);
        }
        spawnPanel.gameObject.SetActive(false);
    }

    public void SetStars(int value)
    {
        starsCounter.text = value + " stars";
    }
}
