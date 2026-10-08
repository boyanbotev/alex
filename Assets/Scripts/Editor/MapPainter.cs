using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class MapPainter : EditorWindow
{
    [SerializeField] private Level level;
    [SerializeField] private TerrainType brush;
    [SerializeField] private int brushSize = 1;
    [SerializeField] private bool cityTool;
    [SerializeField] private CityData selectedCity;
    private MapEditingStage stage;
    private readonly List<CityData> cityChoices = new();
    private readonly List<string> cityLabels = new();
    private int newWidth, newHeight, strokeGroup = -1;
    private Vector2Int? previousCell;
    private string message;
    private MessageType messageType;
    private Vector2 scroll;
    private static readonly Color[] colors = { new(.6f, .72f, .38f), new(.2f, .45f, .25f), new(.55f, .55f, .58f), new(.25f, .55f, .85f) };

    [MenuItem("Tools/Map Painter")]
    private static void OpenMenu() => Open(Selection.activeObject as Level);

    public static void Open(Level level)
    {
        var window = GetWindow<MapPainter>("Map Painter");
        window.SetLevel(level);
        window.Show();
    }

    private void OnEnable()
    {
        minSize = new Vector2(340, 440);
        SceneView.duringSceneGui += PaintScene;
        Undo.undoRedoPerformed += UndoRedo;
        EditorApplication.playModeStateChanged += PlayModeChanged;
    }

    private void OnDisable()
    {
        EndStroke();
        ClosePreview();
        SceneView.duringSceneGui -= PaintScene;
        Undo.undoRedoPerformed -= UndoRedo;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
    }

    private void PlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) { EndStroke(); ClosePreview(); AssetDatabase.SaveAssets(); }
        Repaint();
    }

    private void SetLevel(Level value)
    {
        EndStroke();
        ClosePreview();
        level = value;
        message = null;
        newWidth = level != null ? level.Width : 15;
        newHeight = level != null ? level.Height : 15;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            var value = (Level)EditorGUILayout.ObjectField("Level", level, typeof(Level), false);
            if (value != level) SetLevel(value);
            if (level == null) EditorGUILayout.HelpBox("Choose a Level asset to paint its map.", MessageType.Info);
            else DrawTools();
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorGUILayout.HelpBox("Exit Play Mode to edit the map.", MessageType.Info);
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);
        EditorGUILayout.EndScrollView();
    }

    private void DrawTools()
    {
        var map = (MapData)EditorGUILayout.ObjectField("Map", level.map, typeof(MapData), false);
        if (map != level.map)
        {
            EndStroke();
            ClosePreview();
            Undo.RecordObject(level, "Assign map");
            level.map = map;
            EditorUtility.SetDirty(level);
            newWidth = map != null ? map.width : level.width;
            newHeight = map != null ? map.height : level.height;
        }
        if (GUILayout.Button("Create New Map")) CreateMap();
        if (level.map == null) return;
        map = level.map;
        if (newWidth < 1 || newHeight < 1) { newWidth = map.width; newHeight = map.height; }
        EditorGUILayout.LabelField("Size", $"{map.width} × {map.height}");
        newWidth = Mathf.Clamp(EditorGUILayout.IntField("Width", newWidth), 1, 512);
        newHeight = Mathf.Clamp(EditorGUILayout.IntField("Height", newHeight), 1, 512);
        if (GUILayout.Button("Resize / Initialize Grid"))
        {
            EndStroke();
            Undo.RecordObject(map, "Resize map");
            map.Resize(newWidth, newHeight);
            Changed();
            stage?.Rebuild();
            Frame(true);
        }
        if (map.terrain == null || map.terrain.Length != (long)map.width * map.height)
        {
            EditorGUILayout.HelpBox("Initialize the grid before painting.", MessageType.Info);
            return;
        }
        EditorGUILayout.Space();
        var terrainSource = (TerrainSource)EditorGUILayout.EnumPopup("Terrain source", level.terrainSource);
        var citySource = (CityPlacementSource)EditorGUILayout.EnumPopup("City placement", level.cityPlacement);
        if (terrainSource != level.terrainSource || citySource != level.cityPlacement)
        {
            Undo.RecordObject(level, "Change map sources");
            level.terrainSource = terrainSource;
            level.cityPlacement = citySource;
            EditorUtility.SetDirty(level);
        }
        if (level.terrainSource == TerrainSource.Procedural)
            EditorGUILayout.HelpBox("The painted terrain is a draft until you select Handcrafted. Fixed city tiles become fields on procedural maps.", MessageType.Info);
        if (GUILayout.Button(stage == null ? "Open Map in Scene View" : "Reopen / Refresh Preview")) StartPreview();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Top Down")) Frame(true);
            if (GUILayout.Button("Game Angle")) Frame(false);
        }
        cityTool = GUILayout.Toolbar(cityTool ? 1 : 0, new[] { "Terrain", "Cities" }) == 1;
        if (!cityTool)
        {
            brush = (TerrainType)GUILayout.SelectionGrid((int)brush, Enum.GetNames(typeof(TerrainType)), 2);
            brushSize = EditorGUILayout.IntSlider("Brush size", brushSize, 1, 9) | 1;
            EditorGUILayout.LabelField("Paint area", $"{brushSize} × {brushSize} tiles");
        }
        else
        {
            if (level.cityPlacement == CityPlacementSource.Automatic)
                EditorGUILayout.HelpBox("Select Handcrafted city placement above to use painted city positions in the game.", MessageType.Info);
            BuildCityChoices();
            if (cityChoices.Count == 0) EditorGUILayout.HelpBox("Configure starting cities on this Level first.", MessageType.Info);
            else
            {
                int choice = Mathf.Max(0, cityChoices.IndexOf(selectedCity));
                choice = EditorGUILayout.Popup("City", choice, cityLabels.ToArray());
                selectedCity = cityChoices[choice];
            }
            EditorGUILayout.LabelField("Placed", $"{map.cities?.Count ?? 0} / {level.CityCount}");
        }
        EditorGUILayout.HelpBox("Paint in the Scene view with left click / drag. Right click erases to Field or removes a city. Alt + mouse navigates. Ctrl+Z undoes a whole stroke.", MessageType.Info);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Starting layout", EditorStyles.boldLabel);
        DrawSeedFields();
        if (GUILayout.Button("Generate Terrain from Seed")) GenerateTerrain();
        if (GUILayout.Button("Scatter Level Cities onto Map")) ScatterCities();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate Level")) Validate();
            if (GUILayout.Button("Save")) { EndStroke(); AssetDatabase.SaveAssets(); message = "Map saved."; messageType = MessageType.Info; }
        }
        if (GUILayout.Button("Play This Level")) PlayLevel();
    }

    private void DrawSeedFields()
    {
        int seed = EditorGUILayout.IntField("Seed", level.seed);
        float noise = Mathf.Max(.001f, EditorGUILayout.FloatField("Noise scale", level.noiseScale));
        if (seed == level.seed && noise == level.noiseScale) return;
        Undo.RecordObject(level, "Change map seed");
        level.seed = seed;
        level.noiseScale = noise;
        EditorUtility.SetDirty(level);
    }

    private void CreateMap()
    {
        EndStroke();
        string path = EditorUtility.SaveFilePanelInProject("Create map", level.name + " Map", "asset", "Choose where to save the map.");
        if (string.IsNullOrEmpty(path)) return;
        var map = CreateInstance<MapData>();
        map.Resize(Mathf.Clamp(level.Width, 1, 512), Mathf.Clamp(level.Height, 1, 512));
        AssetDatabase.CreateAsset(map, path);
        Undo.RecordObject(level, "Create handcrafted map");
        level.map = map;
        level.terrainSource = TerrainSource.Handcrafted;
        EditorUtility.SetDirty(level);
        newWidth = map.width;
        newHeight = map.height;
        AssetDatabase.SaveAssets();
        StartPreview();
    }

    private void StartPreview()
    {
        EndStroke();
        ClosePreview();
        try { level.map.ValidateTerrain(); }
        catch (Exception e) { message = e.Message; messageType = MessageType.Error; return; }
        var source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game Scene.unity");
        var prefabs = new GameObject[4];
        try
        {
            foreach (var root in source.GetRootGameObjects())
            {
                var generator = root.GetComponentInChildren<GridGenerator>(true);
                if (generator == null) continue;
                for (int i = 0; i < prefabs.Length; i++) prefabs[i] = generator.GetTerrainPrefab((TerrainType)i);
                break;
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }
        stage = CreateInstance<MapEditingStage>();
        stage.level = level;
        stage.prefabs = prefabs;
        StageUtility.GoToStage(stage, true);
        Frame(true);
    }

    private void ClosePreview()
    {
        if (stage != null && StageUtility.GetCurrentStage() == stage) StageUtility.GoToMainStage();
        if (stage != null) DestroyImmediate(stage);
        stage = null;
    }

    private void Frame(bool topDown)
    {
        if (stage == null) return;
        var view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
        Vector3 center = stage.Position(new Vector2Int(level.map.width - 1, level.map.height - 1)) * .5f;
        Quaternion rotation = level.is3DIsometric ? Quaternion.Euler(topDown ? 90 : 45, topDown ? 0 : 45, 0) : Quaternion.identity;
        view.orthographic = true;
        view.LookAtDirect(center, rotation, Mathf.Max(level.map.width, level.map.height) * level.tileSize * .65f);
        view.sceneLighting = false;
        view.Focus();
    }

    private void GenerateTerrain()
    {
        EndStroke();
        Undo.RecordObject(level.map, "Generate map terrain");
        float offset = (float)new System.Random(level.seed).NextDouble() * 100f;
        var map = level.map;
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++) map.terrain[y * map.width + x] = GridGenerator.SampleTerrain(x, y, level.noiseScale, offset);
        foreach (var city in map.cities) if (map.Contains(city.position)) map.terrain[city.position.y * map.width + city.position.x] = TerrainType.Field;
        Changed();
        stage?.Rebuild();
    }

    private void BuildCityChoices()
    {
        cityChoices.Clear();
        cityLabels.Clear();
        if (level.factions != null)
            foreach (var faction in level.factions)
            {
                if (faction?.startingCities == null) continue;
                for (int i = 0; i < faction.startingCities.Length; i++)
                {
                    var city = faction.startingCities[i];
                    if (city == null) continue;
                    cityChoices.Add(city);
                    cityLabels.Add($"{city.cityName} — {(faction.faction != null ? faction.faction.name : "Faction")}{(i == 0 ? " (Capital)" : "")}");
                }
            }
        if (level.neutralCities != null)
            foreach (var city in level.neutralCities)
                if (city != null) { cityChoices.Add(city); cityLabels.Add(city.cityName + " — Neutral"); }
    }

    private void ScatterCities()
    {
        EndStroke();
        var settings = Instantiate(level);
        var tempTiles = new List<Tile>();
        var tempScene = EditorSceneManager.NewPreviewScene();
        try
        {
            settings.terrainSource = TerrainSource.Handcrafted;
            settings.cityPlacement = CityPlacementSource.Automatic;
            settings.Validate();
            for (int x = 0; x < level.map.width; x++)
                for (int y = 0; y < level.map.height; y++)
                {
                    var go = new GameObject { hideFlags = HideFlags.HideAndDontSave };
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, tempScene);
                    var tile = go.AddComponent<Tile>();
                    tile.gridPosition = new Vector2Int(x, y);
                    tile.terrainType = level.map.GetTerrain(x, y);
                    tempTiles.Add(tile);
                }
            var random = new System.Random(level.seed);
            random.NextDouble(); // Same random state as terrain generation.
            var placement = new CityPlacement();
            Drain(placement.Generate(settings, tempTiles, random, new GenerationBudget(float.MaxValue)));
            BuildCityChoices();
            Undo.RecordObject(level.map, "Scatter map cities");
            level.map.cities.Clear();
            for (int i = 0; i < placement.Positions.Length; i++)
                level.map.cities.Add(new MapCity(cityChoices[i], placement.Positions[i].gridPosition));
            Changed();
        }
        catch (Exception e) { message = e.Message; messageType = MessageType.Error; }
        finally
        {
            EditorSceneManager.ClosePreviewScene(tempScene);
            DestroyImmediate(settings);
        }
    }

    private static void Drain(System.Collections.IEnumerator routine)
    {
        while (routine.MoveNext()) if (routine.Current is System.Collections.IEnumerator child) Drain(child);
    }

    private bool Validate()
    {
        try { level.Validate(); message = "Level settings and authored placements are valid."; messageType = MessageType.Info; return true; }
        catch (Exception e) { message = e.Message; messageType = MessageType.Error; return false; }
    }

    private void PlayLevel()
    {
        EndStroke();
        if (!Validate()) return;
        var game = AssetDatabase.LoadAssetAtPath<GameData>("Assets/Data/Game.asset");
        int index = game != null && game.levels != null ? Array.IndexOf(game.levels, level) : -1;
        if (index < 0) { message = "Add this Level to the game catalog before playtesting."; messageType = MessageType.Error; return; }
        ClosePreview();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.SaveAssets();
        SaveManager.SetLevelIndex(index);
        EditorSceneManager.OpenScene("Assets/Scenes/Game Scene.unity");
        EditorApplication.isPlaying = true;
    }

    private void UndoRedo()
    {
        strokeGroup = -1;
        previousCell = null;
        message = null;
        if (level != null && level.map != null)
        {
            newWidth = level.map.width;
            newHeight = level.map.height;
            stage?.Rebuild();
        }
        else ClosePreview();
        Repaint();
    }

    private void Changed()
    {
        EditorUtility.SetDirty(level.map);
        message = null;
        SceneView.RepaintAll();
        Repaint();
    }

    private void EndStroke()
    {
        if (strokeGroup >= 0) Undo.CollapseUndoOperations(strokeGroup);
        strokeGroup = -1;
        previousCell = null;
    }

    private void PaintScene(SceneView view)
    {
        if (stage == null || level == null || level.map == null || EditorApplication.isPlayingOrWillChangePlaymode || StageUtility.GetCurrentStage() != stage) return;
        var map = level.map;
        if (map.terrain == null || map.terrain.Length != (long)map.width * map.height) return;
        var e = Event.current;
        int control = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.MouseUp && strokeGroup >= 0)
        {
            EndStroke();
            GUIUtility.hotControl = 0;
            e.Use();
            return;
        }
        if (e.type == EventType.Layout && !e.alt) HandleUtility.AddDefaultControl(control);
        if (e.type == EventType.Repaint)
        {
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    var p = new Vector2Int(x, y);
                    var type = map.GetTerrain(x, y);
                    Handles.DrawSolidRectangleWithOutline(stage.Corners(p), stage.HasPrefab(type) ? Color.clear : colors[(int)type], new Color(1, 1, 1, .12f));
                }
            foreach (var city in map.cities)
            {
                if (city.city == null || !map.Contains(city.position)) continue;
                Color color = Color.gray;
                bool capital = false;
                if (level.factions != null)
                    foreach (var faction in level.factions)
                        if (faction?.startingCities != null && Array.IndexOf(faction.startingCities, city.city) >= 0)
                        { color = faction.color; capital = faction.startingCities.Length > 0 && faction.startingCities[0] == city.city; break; }
                var center = stage.Position(city.position);
                if (level.is3DIsometric) center.y = .2f;
                Handles.color = color;
                Handles.DrawSolidDisc(center, level.is3DIsometric ? Vector3.up : Vector3.forward, level.tileSize * .22f);
                Handles.Label(center, $"{(capital ? "★ " : "")}{city.city.cityName}");
            }
        }
        var plane = new Plane(level.is3DIsometric ? Vector3.up : Vector3.forward, Vector3.zero);
        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!plane.Raycast(ray, out float distance)) return;
        var cell = stage.Cell(ray.GetPoint(distance));
        if (map.Contains(cell) && e.type == EventType.Repaint)
            Handles.DrawSolidRectangleWithOutline(stage.Corners(cell, cityTool ? .5f : brushSize * .5f), new Color(1, 1, 1, .1f), Color.yellow);
        if (e.type == EventType.MouseMove) view.Repaint();
        if (e.alt || (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) || e.button > 1 || !map.Contains(cell)) return;
        if (cityTool && e.type == EventType.MouseDrag) return;
        if (e.type == EventType.MouseDown)
        {
            EndStroke();
            Undo.IncrementCurrentGroup();
            strokeGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(cityTool ? "Place map city" : "Paint map terrain");
            Undo.RecordObject(map, cityTool ? "Place map city" : "Paint map terrain");
            GUIUtility.hotControl = control;
        }
        if (strokeGroup < 0) return;
        if (cityTool)
        {
            if (!PlaceCity(cell, e.button == 1)) { Repaint(); e.Use(); return; }
        }
        else
        {
            Undo.RecordObject(map, "Paint map terrain");
            var start = previousCell ?? cell;
            int steps = Mathf.Max(Mathf.Abs(cell.x - start.x), Mathf.Abs(cell.y - start.y));
            for (int i = 0; i <= steps; i++) PaintTerrain(Vector2Int.RoundToInt(Vector2.Lerp(start, cell, steps == 0 ? 1 : (float)i / steps)), e.button == 1 ? TerrainType.Field : brush);
            previousCell = cell;
        }
        Changed();
        e.Use();
    }

    private void PaintTerrain(Vector2Int center, TerrainType type)
    {
        var map = level.map;
        int low = -(brushSize / 2), high = low + brushSize;
        for (int dy = low; dy < high; dy++)
            for (int dx = low; dx < high; dx++)
            {
                var p = center + new Vector2Int(dx, dy);
                if (!map.Contains(p)) continue;
                int index = p.y * map.width + p.x;
                if (map.terrain[index] == type) continue;
                map.terrain[index] = type;
                stage.RefreshTile(p);
            }
    }

    private bool PlaceCity(Vector2Int p, bool erase)
    {
        var map = level.map;
        if (erase) { map.cities.RemoveAll(c => c.position == p); return true; }
        if (selectedCity == null) return false;
        if (!MapData.IsCityTerrain(map.GetTerrain(p.x, p.y)))
        { message = "Place cities on Field or Forest terrain."; messageType = MessageType.Warning; return false; }
        map.cities.RemoveAll(c => c.city == selectedCity || c.position == p);
        map.cities.Add(new MapCity(selectedCity, p));
        return true;
    }
}
