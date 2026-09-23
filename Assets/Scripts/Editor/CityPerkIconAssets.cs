using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

// Rebuild after assigning a new PNG to a CityPerkData icon field.
public static class CityPerkIconAssets
{
    [MenuItem("Tools/City Perks/Rebuild Text Icons")]
    public static void Rebuild()
    {
        const string folder = "Assets/Resources/Sprite Assets";
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Resources", "Sprite Assets");
        foreach (string guid in AssetDatabase.FindAssets("t:CityPerkData"))
        {
            var perk = AssetDatabase.LoadAssetAtPath<CityPerkData>(AssetDatabase.GUIDToAssetPath(guid));
            Sprite sprite = perk.icon;
            if (sprite == null) continue;
            string path = $"{folder}/CityPerk{perk.kind}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_Version").stringValue = "1.1.0";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            asset.spriteSheet = sprite.texture;
            if (asset.material == null)
            {
                asset.material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = asset.name + " Material" };
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            asset.material.mainTexture = sprite.texture;
            // World-space label icons must remain readable over the terrain, like UI text.
            asset.material.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            Rect rect = sprite.rect;
            var glyph = new TMP_SpriteGlyph(0,
                new GlyphMetrics(rect.width, rect.height, 0, rect.height * .85f, rect.width),
                new GlyphRect((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height), 1, 0, sprite);
            asset.spriteGlyphTable.Clear();
            asset.spriteCharacterTable.Clear();
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = perk.kind.ToString(), scale = 1 });
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset.material);
            EditorUtility.SetDirty(asset);
        }
        AssetDatabase.SaveAssets();
    }
}
