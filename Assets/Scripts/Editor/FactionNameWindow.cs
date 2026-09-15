using System;
using UnityEditor;
using UnityEngine;

public sealed class FactionNameWindow : EditorWindow
{
    private Faction source;
    private bool rename;
    private bool renameRelated = true;
    private string factionName = "";
    private string error;
    private Action<Faction> completed;

    public static void Open(Faction source, bool rename, Action<Faction> completed)
    {
        var window = CreateInstance<FactionNameWindow>();
        window.source = source;
        window.rename = rename;
        window.completed = completed;
        window.factionName = source == null ? "" : rename ? source.name : source.name + " Copy";
        window.titleContent = new GUIContent(rename ? "Rename faction" : source == null ? "Create faction" : "Duplicate faction");
        window.minSize = window.maxSize = new Vector2(540, 310);
        window.ShowUtility();
    }

    private void OnGUI()
    {
        factionName = EditorGUILayout.TextField("Faction name", factionName);
        if (rename) renameRelated = EditorGUILayout.ToggleLeft("Rename related assets (faction units, unit prefabs and city prefab)", renameRelated);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Destination folders", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(FactionAssetUtility.DataFolder(factionName));
        EditorGUILayout.LabelField(FactionAssetUtility.PrefabFolder(factionName));
        EditorGUILayout.HelpBox(rename
            ? "The faction asset and folders are always renamed. Unity references are preserved. These file operations are saved immediately."
            : "UnitData stays shared. Unit and city prefabs are copied into the new faction's prefab folder.", MessageType.Info);
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        GUILayout.FlexibleSpace();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Cancel")) Close();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button(rename ? "Rename" : source == null ? "Create" : "Duplicate"))
                {
                    try
                    {
                        Faction result;
                        if (rename) { FactionAssetUtility.Rename(source, factionName, renameRelated); result = source; }
                        else result = FactionAssetUtility.Create(factionName, source);
                        completed?.Invoke(result);
                        Close();
                    }
                    catch (Exception exception) { error = exception.Message; }
                }
        }
    }
}
