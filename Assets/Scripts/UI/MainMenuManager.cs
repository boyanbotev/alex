using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public void StartGame()
    {
        WorldLoadingOverlay.LoadGame(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
