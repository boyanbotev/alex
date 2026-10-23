using UnityEngine;
using TMPro;

public class HealthUI : MonoBehaviour
{
    [SerializeField] TextMeshPro text;
    private Unit unit;
    private Camera mainCamera;
    private Player viewer;
    private Transform backing;
    private int currentHealth;
    private int styleRevision = -1;

    private void Awake()
    {
        unit = GetComponentInParent<Unit>();
        mainCamera = Camera.main;
    }

    public void Set(int health)
    {
        currentHealth = health;
        var style = MapUIStyle.Shared;
        styleRevision = style.Revision;
        text.text = health.ToString();
        text.color = style.textColor;
        text.fontSize = style.healthFontSize;
        text.fontSharedMaterial = MapLabelStyle.Overlay(text.fontSharedMaterial);
        text.ForceMeshUpdate(true);
        if (backing == null) backing = MapLabelStyle.CreateBacking(text.transform);
        MapLabelStyle.FitBacking(backing, text.textBounds, style.healthUnderlayPadding);
    }

    private void LateUpdate()
    {
        if (unit == null || mainCamera == null || backing == null) return;
        var style = MapUIStyle.Shared;
        if (styleRevision != style.Revision) Set(currentHealth);
        if (viewer == null && TurnManager.Instance != null)
            viewer = TurnManager.Instance.players.Find(player => !player.isAI);
        // Overlay materials bypass fog geometry, so explicitly respect tile visibility.
        bool visible = unit.isAlive && unit.currentTile != null && viewer != null &&
            viewer.visibleTiles != null && viewer.visibleTiles.IsVisible(unit.currentTile);
        text.renderer.enabled = visible;
        backing.gameObject.SetActive(visible);
        if (!visible) return;

        // One fixed slot relative to the unit root, independent of artwork bounds,
        // nearby labels, and selection/attack scaling.
        Vector3 parentScale = text.transform.parent.lossyScale;
        text.transform.localScale = new Vector3(1f / Mathf.Max(.001f, parentScale.x),
            1f / Mathf.Max(.001f, parentScale.y), 1f / Mathf.Max(.001f, parentScale.z));
        text.transform.rotation = mainCamera.transform.rotation;
        Vector3 anchor = unit.transform.position + Vector3.up * style.healthWorldHeight +
            mainCamera.transform.right * style.healthOffset.x + mainCamera.transform.up * style.healthOffset.y;
        text.transform.position = anchor - text.transform.TransformVector(text.textBounds.center);
    }
}
