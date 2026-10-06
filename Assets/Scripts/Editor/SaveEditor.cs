using UnityEditor;
using UnityEngine;

public sealed class SaveEditor : EditorWindow
{
    [SerializeField] private GameData game;
    [SerializeField] private int levelIndex;

    [MenuItem("Tools/SaveEditor")]
    private static void Open() => GetWindow<SaveEditor>("SaveEditor");

    private void OnEnable()
    {
        if (game == null) game = AssetDatabase.LoadAssetAtPath<GameData>("Assets/Data/Game.asset");
        levelIndex = SaveManager.LevelIndex;
        minSize = new Vector2(360, 160);
    }

    private void OnGUI()
    {
        game = (GameData)EditorGUILayout.ObjectField("Game catalog", game, typeof(GameData), false);
        if (game == null || game.levels == null || game.levels.Length == 0)
        {
            EditorGUILayout.HelpBox("Select a game catalog with levels.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Saved level", LevelName(SaveManager.LevelIndex));
        var names = new string[game.levels.Length];
        for (int i = 0; i < names.Length; i++) names[i] = LevelName(i);
        levelIndex = EditorGUILayout.Popup("Current level", Mathf.Clamp(levelIndex, 0, names.Length - 1), names);
        using (new EditorGUI.DisabledScope(game.levels[levelIndex] == null))
            if (GUILayout.Button("Set Level")) SaveManager.SetLevelIndex(levelIndex);

        EditorGUILayout.HelpBox("Applies the next time you start or restart the game.", MessageType.Info);
    }

    private string LevelName(int index) => index < game.levels.Length && game.levels[index] != null
        ? $"{index + 1}: {game.levels[index].name}"
        : $"{index + 1}: Missing level";
}
