using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class CityStarsUI : MonoBehaviour
{
    [SerializeField] TextMeshPro text;
    private static readonly Dictionary<Material, Material> overlays = new();

    private void OnEnable() => text.OnPreRenderText += ApplyIconOverlays;
    private void OnDisable() => text.OnPreRenderText -= ApplyIconOverlays;

    public void Set(City city, int stars)
    {
        string perks = CityPerkIcons.Row(city);
        text.text = $"<size=130%><b>{city.cityName}</b></size>\n" +
            (perks.Length > 0 ? perks + "   " : "") + CurrencyUI.Amount(stars);

        // Shared city-only materials preserve map placement and avoid per-city copies.
        text.fontSharedMaterial = Overlay(text.fontSharedMaterial);
        text.ForceMeshUpdate(true);
        ApplyIconOverlays(text.textInfo);
    }

    private void ApplyIconOverlays(TMP_TextInfo _)
    {
        // TMP restores sprite materials on rebuild. Override the renderer after
        // each rebuild without dirtying the TMP material and scheduling another.
        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMesh>(true))
            subMesh.renderer.sharedMaterial = Overlay(subMesh.sharedMaterial);
    }

    private static Material Overlay(Material source)
    {
        if (overlays.TryGetValue(source, out var material)) return material;
        material = new Material(source) { name = source.name + " City Overlay", renderQueue = 4000 };
        material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        overlays.Add(source, material);
        overlays.Add(material, material);
        return material;
    }
}
