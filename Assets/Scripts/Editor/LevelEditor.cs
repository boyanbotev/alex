using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Level))]
public sealed class LevelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var level = (Level)target;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("terrainSource"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("map"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cityPlacement"));
        bool handcrafted = serializedObject.FindProperty("terrainSource").enumValueIndex == (int)TerrainSource.Handcrafted;
        bool automatic = serializedObject.FindProperty("cityPlacement").enumValueIndex == (int)CityPlacementSource.Automatic;
        var property = serializedObject.GetIterator();
        bool enter = true;
        while (property.NextVisible(enter))
        {
            enter = false;
            if (property.name == "m_Script" || property.name == "terrainSource" || property.name == "map" || property.name == "cityPlacement") continue;
            if (handcrafted && (property.name == "width" || property.name == "height" || property.name == "noiseScale")) continue;
            if (!automatic && (property.name == "minCityDistance" || property.name == "minMargin")) continue;
            // Seed is still used by automatic city placement on painted terrain.
            if (handcrafted && !automatic && (property.name == "seed" || property.name == "randomizeSeed")) continue;
            EditorGUILayout.PropertyField(property, true);
        }
        serializedObject.ApplyModifiedProperties();
        if (handcrafted && level.map != null)
            EditorGUILayout.LabelField("Map size", $"{level.Width} × {level.Height} (edit in painter)");
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Edit Map")) MapPainter.Open(level);
    }
}
