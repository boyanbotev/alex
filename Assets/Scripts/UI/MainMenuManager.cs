using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour, IUIStyleTarget
{
    [SerializeField] private TextAsset preludeStory;
    private bool starting;
    private Canvas[] menuCanvases;
    private Camera menuCamera;

    private void Awake()
    {
        // Cache the authored menu only; a later prelude has its own light theme.
        menuCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        menuCamera = Camera.main;
        ApplyStyle();
    }

    public void ApplyStyle()
    {
        var style = UIStyle.Shared;
        if (style == null || menuCanvases == null) return;
        if (menuCamera != null)
        {
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = style.dark.background;
        }
        foreach (var canvas in menuCanvases)
        {
            if (canvas == null || canvas.gameObject.scene != gameObject.scene) continue;
            style.Surface(canvas, style.dark);
            foreach (var image in canvas.GetComponentsInChildren<Image>(true))
                if (image.GetComponent<Button>() == null && image.sprite == null && image.color.a > 0)
                    image.color = style.dark.panel;
        }
    }

    public void StartGame()
    {
        if (starting) return;
        starting = true;
        if (SaveManager.LevelIndex == 0 && SaveManager.Campaign.phase == CampaignPhase.Gameplay)
            gameObject.AddComponent<LevelEnding>().ShowPrelude(preludeStory, GameManager.LoadGame);
        else GameManager.ContinueCampaign();
    }
}
