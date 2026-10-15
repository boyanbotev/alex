using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private TextAsset preludeStory;
    private bool starting;

    public void StartGame()
    {
        if (starting) return;
        starting = true;
        if (SaveManager.LevelIndex == 0 && SaveManager.Campaign.phase == CampaignPhase.Gameplay)
            gameObject.AddComponent<LevelEnding>().ShowPrelude(preludeStory, GameManager.LoadGame);
        else GameManager.ContinueCampaign();
    }
}
