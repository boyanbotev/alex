using UnityEditor;
using UnityEngine;

public sealed class DiplomacyWindow : EditorWindow
{
    private int first;
    private int second = 1;

    [MenuItem("Tools/Diplomacy")]
    public static void Open() => GetWindow<DiplomacyWindow>("Diplomacy");

    private void OnInspectorUpdate() => Repaint();

    private void OnGUI()
    {
        var turns = TurnManager.Instance;
        if (!Application.isPlaying || turns == null || GridGenerator.Instance == null ||
            !GridGenerator.Instance.IsReady || turns.players.Count < 2)
        {
            EditorGUILayout.HelpBox("Enter Play mode and wait for the world to load.", MessageType.Info);
            return;
        }

        var names = new string[turns.players.Count];
        for (int i = 0; i < names.Length; i++)
            names[i] = $"{i + 1}: {turns.players[i].name}";
        first = EditorGUILayout.Popup("Player", Mathf.Clamp(first, 0, names.Length - 1), names);
        second = EditorGUILayout.Popup("Other player", Mathf.Clamp(second, 0, names.Length - 1), names);
        if (first == second)
        {
            EditorGUILayout.HelpBox("Choose two different players.", MessageType.Info);
            return;
        }

        Player a = turns.players[first];
        Player b = turns.players[second];
        var diplomacy = turns.Diplomacy;
        var relation = diplomacy.GetRelation(a, b);
        EditorGUILayout.LabelField("Relation", relation.ToString());
        EditorGUILayout.HelpBox("Manual testing: peace applies immediately. Changes last for this play session.", MessageType.Info);
        using (new EditorGUI.DisabledScope(relation == DiplomaticRelation.War))
            if (GUILayout.Button("Declare war")) diplomacy.DeclareWar(a, b);
        using (new EditorGUI.DisabledScope(relation == DiplomaticRelation.Peace))
            if (GUILayout.Button("Make peace")) diplomacy.MakePeace(a, b);
        using (new EditorGUI.DisabledScope(relation == DiplomaticRelation.Allied))
            if (GUILayout.Button("Form alliance")) diplomacy.MakeAlliance(a, b);
    }
}
