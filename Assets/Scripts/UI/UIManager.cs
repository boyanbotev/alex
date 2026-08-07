using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] Button spawnButton;
    [SerializeField] TextMeshProUGUI starsCounter;
    public static UIManager Instance;

    private void Awake()
    {
        Instance = this;
        spawnButton.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        Player.OnUpdateStars += SetStars;
    }

    private void OnDisable()
    {
        Player.OnUpdateStars -= SetStars;
    }

    public void ShowSpawnButton(UnityAction action)
    {
        spawnButton.onClick.RemoveAllListeners();
        spawnButton.gameObject.SetActive(true);
        spawnButton.onClick.AddListener(action);
    }

    public void SetStars(int value)
    {
        starsCounter.text = value + " stars";
    }
}
