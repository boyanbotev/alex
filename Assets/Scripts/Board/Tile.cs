using DG.Tweening;
using UnityEngine;

public enum TerrainType { Field, Forest, Mountain, Water }

public class Tile : MonoBehaviour
{
    public Vector2Int gridPosition;
    public TerrainType terrainType;
    public int movementCost = 1;

    [Header("Occupants")]
    public Unit currentUnit;
    public City city;
    public Building currentBuilding;

    [Header("Territory")]
    [Tooltip("The city whose territory this tile belongs to (used for building placement). " +
         "Distinct from 'city' above, which is only set on the tile a city is physically built on.")]
    public City territoryCity;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject highlight;
    [SerializeField, Min(1f)] private float highlightExpandedScale = 1.15f;
    [SerializeField, Min(0.01f)] private float highlightGrowSeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float highlightShrinkSeconds = 0.5f;
    [SerializeField] private Ease highlightGrowEase = Ease.InOutSine;
    [SerializeField] private Ease highlightShrinkEase = Ease.InOutSine;

    private Vector3 highlightOriginalScale;
    private MeshRenderer highlightRenderer;
    private Sequence highlightAnimation;

    private void Awake()
    {
        if (highlight == null) return;
        highlightOriginalScale = highlight.transform.localScale;
        highlightRenderer = highlight.GetComponent<MeshRenderer>();
    }

    public void SetHighlight(bool active, Color color)
    {
        StopHighlightAnimation();
        if (highlight != null)
        {
            highlight.SetActive(active);
            highlightRenderer.material.color = color;
        }
    }

    public void SetExpandingHighlight(bool active, Color color)
    {
        SetHighlight(active, color);
        if (!active || highlight == null || !isActiveAndEnabled) return;

        highlightAnimation = DOTween.Sequence()
            .Append(highlight.transform.DOScale(highlightOriginalScale * highlightExpandedScale,
                highlightGrowSeconds).SetEase(highlightGrowEase))
            .Append(highlight.transform.DOScale(highlightOriginalScale,
                highlightShrinkSeconds).SetEase(highlightShrinkEase))
            .SetLoops(-1);
    }

    private void OnDisable() => StopHighlightAnimation();

    private void StopHighlightAnimation()
    {
        highlightAnimation?.Kill();
        highlightAnimation = null;
        if (highlight != null) highlight.transform.localScale = highlightOriginalScale;
    }
}
