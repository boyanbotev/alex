using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class FactionUnitWindow : EditorWindow
{
    [SerializeField] private Faction faction;
    [SerializeField] private FactionUnit selected;
    [SerializeField] private UnitData selectedData;
    private Faction[] factions = Array.Empty<Faction>();
    private UnitData[] units = Array.Empty<UnitData>();
    private FactionUnit[] entries = Array.Empty<FactionUnit>();
    private Vector2 scroll;
    private int tab;
    private UnitData addData;
    private GameObject addPrefab;

    [MenuItem("Tools/Factions and Units")]
    public static void Open() => GetWindow<FactionUnitWindow>("Factions & Units");

    private void OnEnable()
    {
        minSize = new Vector2(720, 500);
        RefreshAssets();
        EditorApplication.projectChanged += RefreshAssets;
        Undo.undoRedoPerformed += Repaint;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= RefreshAssets;
        Undo.undoRedoPerformed -= Repaint;
    }

    private static T[] FindAssets<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a != null).OrderBy(a => a.name).ToArray();

    private void RefreshAssets()
    {
        factions = FindAssets<Faction>();
        units = FindAssets<UnitData>();
        entries = FindAssets<FactionUnit>();
        Repaint();
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            tab = GUILayout.Toolbar(tab, new[] { "Factions", "Units" }, EditorStyles.toolbarButton);
            if (GUILayout.Button("Save assets", EditorStyles.toolbarButton, GUILayout.Width(90)))
                AssetDatabase.SaveAssets();
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Exit Play mode to edit faction assets.", MessageType.Info);
            return;
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (tab == 0) DrawFaction();
        else DrawUnits();
        EditorGUILayout.EndScrollView();
    }

    private void DrawFaction()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var next = (Faction)EditorGUILayout.ObjectField("Faction", faction, typeof(Faction), false);
            if (next != faction) { faction = next; selected = null; }
            if (GUILayout.Button("New", GUILayout.Width(55)))
                FactionNameWindow.Open(null, false, SelectFaction);
            using (new EditorGUI.DisabledScope(faction == null))
            {
                if (GUILayout.Button("Duplicate", GUILayout.Width(80))) FactionNameWindow.Open(faction, false, SelectFaction);
                if (GUILayout.Button("Rename", GUILayout.Width(70))) FactionNameWindow.Open(faction, true, SelectFaction);
            }
        }
        using (new EditorGUILayout.HorizontalScope())
            foreach (var item in factions)
                if (GUILayout.Button(item.name)) { faction = item; selected = null; }
        if (faction == null) return;

        var cityTemplate = (GameObject)EditorGUILayout.ObjectField("City prefab", faction.cityPrefab, typeof(GameObject), false);
        if (cityTemplate != faction.cityPrefab && cityTemplate != null)
        {
            try { FactionAssetUtility.SetCity(faction, cityTemplate); }
            catch (Exception error) { EditorUtility.DisplayDialog("Could not assign city", error.Message, "OK"); }
        }
        DrawProperties(faction, "startingUnit", "availableBuildings", "availableTech", "startingUnlockedTech");
        EditorGUILayout.HelpBox("Starting techs are free grants. Only listed techs are unlocked, regardless of prerequisites.", MessageType.Info);
        EditorGUILayout.LabelField("Roster", EditorStyles.boldLabel);
        foreach (var entry in faction.availableUnits ?? Array.Empty<FactionUnit>())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(entry != null ? entry.name : "(Missing unit)"))
                    selected = entry;
                using (new EditorGUI.DisabledScope(entry == null))
                    if (GUILayout.Button("Starting unit", GUILayout.Width(95)))
                    {
                        Undo.RecordObject(faction, "Set starting unit");
                        faction.startingUnit = entry;
                        EditorUtility.SetDirty(faction);
                    }
                if (GUILayout.Button("Remove", GUILayout.Width(65)))
                {
                    Undo.RecordObject(faction, "Remove roster unit");
                    faction.availableUnits = faction.availableUnits.Where(u => u != entry).ToArray();
                    if (faction.startingUnit == entry) faction.startingUnit = null;
                    if (selected == entry) selected = null;
                    EditorUtility.SetDirty(faction);
                    GUIUtility.ExitGUI();
                }
            }
        }
        addData = (UnitData)EditorGUILayout.ObjectField("Add: stats", addData, typeof(UnitData), false);
        addPrefab = (GameObject)EditorGUILayout.ObjectField("Add: prefab", addPrefab, typeof(GameObject), false);
        using (new EditorGUI.DisabledScope(addData == null || addPrefab == null || addPrefab.GetComponent<Unit>() == null))
            if (GUILayout.Button("Add unit to faction"))
            {
                try { selected = FactionAssetUtility.AddUnit(faction, addData, addPrefab); RefreshAssets(); }
                catch (Exception error) { EditorUtility.DisplayDialog("Could not add unit", error.Message, "OK"); }
            }
        if (selected != null && (faction.availableUnits ?? Array.Empty<FactionUnit>()).Contains(selected))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(selected.name, EditorStyles.boldLabel);
            DrawProperties(selected, "unitData", "prefab");
            if (selected.faction != faction)
            {
                EditorGUILayout.HelpBox("This entry belongs to another faction. Add it using stats and prefab to create an independent entry.", MessageType.Warning);
            }
            DrawStats(selected.unitData);
            using (new EditorGUI.DisabledScope(selected.prefab == null))
            {
                if (GUILayout.Button("Open prefab to edit visuals")) AssetDatabase.OpenAsset(selected.prefab);
            }
        }
        DrawValidation();
    }

    private void DrawUnits()
    {
        if (GUILayout.Button("Create unit stats"))
        {
            selectedData = CreateAsset<UnitData>("New Unit", "Assets/Data/Units");
            RefreshAssets();
        }
        EditorGUILayout.HelpBox("Table edits affect every faction using that stats asset.", MessageType.Info);
        string[] fields = { "cost", "maxHealth", "attackPower", "defensePower", "moveRange", "attackRange" };
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Unit", GUILayout.Width(170));
            foreach (string label in new[] { "Cost", "Health", "Attack", "Defence", "Move", "Range" })
                GUILayout.Label(label, GUILayout.Width(65));
        }
        foreach (var data in units)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(data.name, GUILayout.Width(170))) selectedData = data;
                var serialized = new SerializedObject(data);
                serialized.Update();
                foreach (string field in fields)
                    EditorGUILayout.PropertyField(serialized.FindProperty(field), GUIContent.none, GUILayout.Width(65));
                serialized.ApplyModifiedProperties();
            }
        }
        DrawStats(selectedData);
    }

    private void DrawStats(UnitData data)
    {
        if (data == null) return;
        var users = entries.Where(e => e.unitData == data).Select(e => e.name).ToArray();
        EditorGUILayout.LabelField("Stats: " + data.name, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Used by: " + (users.Length == 0 ? "no faction units" : string.Join(", ", users)), MessageType.Info);
        DrawProperties(data, "cost", "maxHealth", "attackPower", "defensePower", "moveRange", "attackRange", "skills", "counters", "requiredTech");
    }

    private static void DrawProperties(UnityEngine.Object asset, params string[] names)
    {
        var serialized = new SerializedObject(asset);
        serialized.Update();
        foreach (string name in names)
            EditorGUILayout.PropertyField(serialized.FindProperty(name), true);
        serialized.ApplyModifiedProperties();
    }

    private void DrawValidation()
    {
        var roster = faction.availableUnits ?? Array.Empty<FactionUnit>();
        if (faction.cityPrefab == null) Warn("Assign a city prefab.");
        if (faction.startingUnit == null || !roster.Contains(faction.startingUnit))
            Warn("Choose a starting unit from the roster.");
        if (roster.Distinct().Count() != roster.Length) Warn("The roster contains duplicate entries.");
        foreach (var entry in roster)
        {
            if (entry == null) { Warn("The roster contains a missing unit."); continue; }
            if (entry.faction != faction) Warn(entry.name + ": faction reference does not match.");
            if (entry.unitData == null) Warn(entry.name + ": missing stats.");
            if (entry.prefab == null || entry.prefab.GetComponent<Unit>() == null)
                Warn(entry.name + ": assign a prefab with a Unit component on its root.");
        }
        foreach (var tech in faction.startingUnlockedTech ?? Array.Empty<TechData>())
            if (tech == null) Warn("Starting tech list contains a missing technology.");
            else if (!(faction.availableTech ?? Array.Empty<TechData>()).Contains(tech))
                Warn(tech.name + " is granted at start but is absent from the faction's technology tree.");
    }

    private static void Warn(string message) => EditorGUILayout.HelpBox(message, MessageType.Warning);
    private static T CreateAsset<T>(string name, string folder) where T : ScriptableObject
    {
        string path = EditorUtility.SaveFilePanelInProject("Create " + typeof(T).Name, name, "asset", "Choose asset location", folder);
        if (string.IsNullOrEmpty(path)) return null;
        var asset = CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        Undo.RegisterCreatedObjectUndo(asset, "Create " + typeof(T).Name);
        return asset;
    }

    private void SelectFaction(Faction value)
    {
        faction = value;
        selected = null;
        RefreshAssets();
        Repaint();
    }
}
