using TMPro;
using UnityEngine;
using System;

public class CityStarsUI : MonoBehaviour
{
    [SerializeField] TextMeshPro text;
    private Transform backing;
    private City city;
    private int income;
    private int styleRevision = -1;

    private void LateUpdate()
    {
        if (city != null && styleRevision != MapUIStyle.Shared.Revision) Set(city, income);
    }

    private void OnEnable()
    {
        text.OnPreRenderText += ApplyIconOverlays;
    }

    private void OnDisable()
    {
        text.OnPreRenderText -= ApplyIconOverlays;
    }

    public void Set(City city, int stars)
    {
        this.city = city;
        income = stars;
        var style = MapUIStyle.Shared;
        styleRevision = style.Revision;
        string spacing = FormattableString.Invariant($"<space={style.cityElementSpacing}em>");
        string perks = CityPerkIcons.Row(city, spacing);
        text.color = style.textColor;
        text.fontSize = style.cityFontSize;
        text.lineSpacing = style.cityLineSpacing;
        text.text = FormattableString.Invariant($"<size={style.cityNameScale * 100}%><b>{city.cityName}</b></size>\n<size={style.cityDetailsScale * 100}%>") +
            (perks.Length > 0 ? perks + spacing : "") +
            $"<color=#{ColorUtility.ToHtmlStringRGBA(style.coinColor)}>{CurrencyUI.Icon}{spacing}{stars}</color></size>";

        // Shared city-only materials preserve map placement and avoid per-city copies.
        text.fontSharedMaterial = MapLabelStyle.Overlay(text.fontSharedMaterial);
        text.ForceMeshUpdate(true);
        ApplyIconOverlays(text.textInfo);

        if (backing == null) backing = MapLabelStyle.CreateBacking(text.transform);
        var bounds = text.textBounds;
        MapLabelStyle.FitBacking(backing, bounds, style.cityUnderlayPadding);
        // Limit even long names and multiple shared perks to a compact tile footprint.
        float scale = Mathf.Min(1f, style.cityMaxSize.x / backing.localScale.x,
            style.cityMaxSize.y / backing.localScale.y);
        text.transform.localScale = Vector3.one * scale;
        Camera camera = Camera.main;
        if (camera != null)
        {
            text.transform.rotation = camera.transform.rotation;
            // Put the top of the backing immediately below the actual city marker.
            Vector3 marker = city.model != null ? city.model.position : city.transform.position + Vector3.up * .5f;
            float halfHeight = backing.localScale.y * scale * .5f;
            Vector3 anchor = marker + camera.transform.right * style.cityOffset.x +
                camera.transform.up * (style.cityOffset.y - style.cityMarkerGap - halfHeight);
            text.transform.position = anchor - text.transform.TransformVector(bounds.center);
        }
    }

    private void ApplyIconOverlays(TMP_TextInfo _)
    {
        // TMP restores sprite materials on rebuild. Override the renderer after
        // each rebuild without dirtying the TMP material and scheduling another.
        foreach (var subMesh in text.GetComponentsInChildren<TMP_SubMesh>(true))
            subMesh.renderer.sharedMaterial = MapLabelStyle.Overlay(subMesh.sharedMaterial);
    }
}
