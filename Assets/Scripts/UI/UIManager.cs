using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] Button spawnButton;
    public static UIManager Instance;

    private void Awake()
    {
        Instance = this;
        spawnButton.gameObject.SetActive(false);
    }

    public void ShowSpawnButton(UnityAction action)
    {
        spawnButton.onClick.RemoveAllListeners();
        spawnButton.gameObject.SetActive(true);
        spawnButton.onClick.AddListener(action);
    }
}
