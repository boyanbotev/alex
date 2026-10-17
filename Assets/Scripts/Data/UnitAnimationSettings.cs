using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(fileName = "UnitAnimationSettings", menuName = "Game/Unit Animation Settings")]
public sealed class UnitAnimationSettings : ScriptableObject
{
    [Header("Melee")]
    [Range(0f, 1f)] public float lungeFraction = 0.3f;
    [Min(0f)] public float lungeOutSeconds = 0.08f;
    [Min(0f)] public float lungeBackSeconds = 0.10f;
    [Min(0f)] public float advanceSeconds = 0.14f;
    [Tooltip("Extra travel beyond the target, as a fraction of the attack distance. Zero disables overshoot.")]
    [Range(0f, 1f)] public float advanceOvershootFraction = 0.12f;
    [Min(0f)] public float advanceSnapBackSeconds = 0.06f;
    [Tooltip("World-space offset away from the camera for the defeated unit.")]
    [Min(0f)] public float defeatedDepthOffset = 0.05f;
    public Ease lungeOutEase = Ease.OutQuad;
    public Ease lungeBackEase = Ease.InOutQuad;
    public Ease advanceEase = Ease.OutQuad;
    public Ease advanceSnapBackEase = Ease.OutQuad;

    [Header("Selection")]
    [Min(1f)] public float selectionScale = 1.1f;
    [Min(0f)] public float selectionGrowSeconds = 0.05f;
    [Min(0f)] public float selectionShrinkSeconds = 0.08f;
    public Ease selectionGrowEase = Ease.OutQuad;
    public Ease selectionShrinkEase = Ease.InQuad;
}
