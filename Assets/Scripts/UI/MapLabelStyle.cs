using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Shared materials and geometry for world labels. Capture UI sorts above these.
public static class MapLabelStyle
{
    private static readonly Dictionary<Material, Material> overlays = new();
    private static Sprite backgroundSprite;
    private static Material backgroundMaterial;
    private static readonly MaterialPropertyBlock backingProperties = new();
    private static readonly int sizeId = Shader.PropertyToID("_Size");
    private static readonly int radiusId = Shader.PropertyToID("_CornerRadius");
    private static readonly int softnessId = Shader.PropertyToID("_EdgeSoftness");

    public static Material Overlay(Material source)
    {
        if (overlays.TryGetValue(source, out var material)) return material;
        material = new Material(source) { name = source.name + " Map Overlay", renderQueue = 4000 };
        material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        overlays.Add(source, material);
        overlays.Add(material, material);
        return material;
    }

    public static Transform CreateBacking(Transform parent)
    {
        if (backgroundSprite == null)
        {
            var texture = Texture2D.whiteTexture;
            backgroundSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                Vector2.one * .5f, texture.width);
            // Resources keeps the shader available in player builds too.
            backgroundMaterial = new Material(Resources.Load<Shader>("MapLabelUnderlay"))
                { name = "Map Label Backing", renderQueue = 3999 };
        }
        var backing = new GameObject("Label Backing").AddComponent<SpriteRenderer>();
        backing.transform.SetParent(parent, false);
        backing.sprite = backgroundSprite;
        backing.sharedMaterial = backgroundMaterial;
        backing.color = MapUIStyle.Shared.underlayColor;
        return backing.transform;
    }

    public static void FitBacking(Transform backing, Bounds bounds, Vector2 padding)
    {
        backing.localPosition = bounds.center;
        backing.localScale = new Vector3(bounds.size.x + padding.x * 2, bounds.size.y + padding.y * 2, 1);
        var renderer = backing.GetComponent<SpriteRenderer>();
        renderer.color = MapUIStyle.Shared.underlayColor;
        renderer.enabled = MapUIStyle.Shared.showUnderlay;
        UpdateBackingShape(backing);
    }

    public static void UpdateBackingShape(Transform backing)
    {
        var renderer = backing.GetComponent<SpriteRenderer>();
        var style = MapUIStyle.Shared;
        Vector3 size = backing.lossyScale;
        renderer.GetPropertyBlock(backingProperties);
        backingProperties.SetVector(sizeId, new Vector4(Mathf.Abs(size.x), Mathf.Abs(size.y), 0, 0));
        backingProperties.SetFloat(radiusId, style.underlayCornerRadius);
        backingProperties.SetFloat(softnessId, style.underlayEdgeSoftness);
        renderer.SetPropertyBlock(backingProperties);
    }
}
