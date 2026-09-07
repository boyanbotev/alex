using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class WorldLoadingOverlay : MonoBehaviour
{
    private static WorldLoadingOverlay instance;
    private RectTransform spinner;
    private TextMeshProUGUI label;

    public static void Show(string message)
    {
        if (instance == null)
        {
            var root = new GameObject("World Loading", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(root);
            instance = root.AddComponent<WorldLoadingOverlay>();
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(UIInputBlocker));
            background.transform.SetParent(root.transform, false);
            var rect = background.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 1f);
            instance.spinner = new GameObject("Spinner", typeof(RectTransform)).GetComponent<RectTransform>();
            instance.spinner.SetParent(root.transform, false);
            instance.spinner.anchoredPosition = new Vector2(0, 35);
            for (int i = 0; i < 8; i++)
            {
                var segment = new GameObject("Segment", typeof(RectTransform), typeof(Image));
                segment.transform.SetParent(instance.spinner, false);
                var segmentRect = segment.GetComponent<RectTransform>();
                float angle = i * Mathf.PI / 4;
                segmentRect.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 25;
                segmentRect.sizeDelta = new Vector2(7, 14);
                segmentRect.localRotation = Quaternion.Euler(0, 0, -i * 45);
                var graphic = segment.GetComponent<Image>();
                graphic.color = new Color(1, 1, 1, (i + 1) / 8f);
                graphic.raycastTarget = false;
            }
            instance.label = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            instance.label.transform.SetParent(root.transform, false);
            instance.label.rectTransform.anchoredPosition = new Vector2(0, -40);
            instance.label.rectTransform.sizeDelta = new Vector2(700, 60);
            instance.label.fontSize = 28;
            instance.label.alignment = TextAlignmentOptions.Center;
            instance.label.raycastTarget = false;
        }
        instance.label.text = message;
    }

    public static void Hide()
    {
        if (instance == null) return;
        Destroy(instance.gameObject);
        instance = null;
    }

    public static void LoadGame(int sceneIndex)
    {
        if (instance != null) return;
        Show("Loading scene…");
        instance.StartCoroutine(instance.LoadScene(sceneIndex));
    }

    private IEnumerator LoadScene(int sceneIndex)
    {
        // Let the overlay render before scene loading begins.
        yield return null;
        yield return null;
        yield return SceneManager.LoadSceneAsync(sceneIndex);
    }

    private void Update() => spinner.Rotate(0, 0, -240 * Time.unscaledDeltaTime);
}
