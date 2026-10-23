using UnityEngine;

[CreateAssetMenu(menuName = "Game/Map UI Style", fileName = "Map UI Style")]
public sealed class MapUIStyle : ScriptableObject
{
    private static MapUIStyle instance;
    public static MapUIStyle Shared => instance != null ? instance : instance = Resources.Load<MapUIStyle>("Map UI Style");
    public int Revision { get; private set; }

    [Header("Shared appearance")]
    public Color textColor = Color.white;
    public bool showUnderlay = true;
    public Color underlayColor = new Color(.06f, .08f, .12f, .85f);

    [Header("City labels")]
    [Min(.1f)] public float cityFontSize = 1.7f;
    [Min(.1f)] public float cityNameScale = 1.15f;
    [Min(.1f)] public float cityDetailsScale = .9f;
    public Color coinColor = new Color(1f, 211f / 255f, 77f / 255f);
    public float cityLineSpacing;
    [Tooltip("Spacing between perks, currency icon, and amount, in em units.")]
    [Min(0)] public float cityElementSpacing = .25f;
    [Tooltip("Maximum backing width and height in world units. The whole label shrinks to fit.")]
    public Vector2 cityMaxSize = new Vector2(.9f, .3f);
    public Vector2 cityUnderlayPadding = new Vector2(.045f, .025f);
    [Tooltip("Gap from the city marker to the top of its label, in world units.")]
    [Min(0)] public float cityMarkerGap = .07f;
    [Tooltip("Additional offset in camera-right and camera-up world units.")]
    public Vector2 cityOffset;

    [Header("Unit health")]
    [Min(.1f)] public float healthFontSize = 1.65f;
    public Vector2 healthUnderlayPadding = new Vector2(.025f, .015f);
    [Tooltip("World height above the unit root, independent of the artwork.")]
    public float healthWorldHeight = 1f;
    [Tooltip("Offset in camera-right and camera-up world units.")]
    public Vector2 healthOffset = new Vector2(.24f, .35f);

    private void OnValidate()
    {
        cityMaxSize = Vector2.Max(Vector2.one * .01f, cityMaxSize);
        cityUnderlayPadding = Vector2.Max(Vector2.zero, cityUnderlayPadding);
        healthUnderlayPadding = Vector2.Max(Vector2.zero, healthUnderlayPadding);
        // Labels observe this on the main thread, including Inspector edits in Play mode.
        Revision++;
    }
}
