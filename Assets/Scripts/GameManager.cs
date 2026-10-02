using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    [SerializeField] private GameData game;
    public int LevelIndex { get; private set; }
    public Level Level { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (game == null || game.levels == null || game.levels.Length == 0)
            throw new System.InvalidOperationException("Assign a game catalog with levels to GameManager.");
        LevelIndex = Mathf.Clamp(SaveManager.LevelIndex, 0, game.levels.Length - 1);
        Level = game.levels[LevelIndex];
        if (Level == null) throw new System.InvalidOperationException($"Level {LevelIndex} is missing from the game catalog.");
        Level.Validate();
        if (Level.tutorial != null) gameObject.AddComponent<TutorialManager>();
        if (LevelIndex != SaveManager.LevelIndex) SaveManager.SetLevelIndex(LevelIndex);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void LoadGame() => WorldLoadingOverlay.LoadGame(
        SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Game Scene.unity"));
}
