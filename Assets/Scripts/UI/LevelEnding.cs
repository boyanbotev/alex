using System;
using System.Collections.Generic;
using System.Text;
using Ink.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LevelEnding : MonoBehaviour
{
    private Story story;
    private TextMeshProUGUI text;
    private RectTransform content;
    private ScrollRect scroll;
    private Action onComplete;
    private GameObject panel;
    private TextMeshProUGUI pageNumber;
    private TMP_FontAsset readingFont;
    private bool reading;
    private readonly List<GameObject> buttons = new();

    public static string PairKey(string a, string b)
    {
        a = a.Trim().ToLowerInvariant().Replace(' ', '_');
        b = b.Trim().ToLowerInvariant().Replace(' ', '_');
        return string.CompareOrdinal(a, b) <= 0 ? a + "_" + b : b + "_" + a;
    }

    public void Show(TextAsset json, IEnumerable<string> pairKeys, IReadOnlyDictionary<string, float> factionSuccess,
        IEnumerable<string> cityPairs, IReadOnlyDictionary<Faction, float> factionScores)
    {
        BuildPanel("Level completed");
        text.richText = false;
        var summary = new StringBuilder();
        bool hasBonds = false;
        foreach (string pair in cityPairs)
        {
            if (!hasBonds) summary.AppendLine("Strong bonds:");
            summary.AppendLine(pair);
            hasBonds = true;
        }
        if (hasBonds) summary.AppendLine();
        summary.AppendLine("Faction scores:");
        foreach (var score in factionScores)
            summary.AppendLine($"{score.Key.name}: {score.Value:P0}");
        text.text = summary.ToString().TrimEnd();
        AddButton("Continue", () => ShowStory(json, null, null, pairKeys, factionSuccess), primary: true);
    }

    public void ShowPrelude(TextAsset json, Action beginLevel)
    {
        ShowStory(json, "Prelude", beginLevel);
    }

    private void ShowStory(TextAsset json, string title, Action completion,
        IEnumerable<string> pairKeys = null, IReadOnlyDictionary<string, float> factionSuccess = null)
    {
        onComplete = completion;
        reading = true;
        BuildPanel(title ?? "Placeholder title");
        try
        {
            if (json == null) throw new InvalidOperationException("Assign an exported Ink JSON to the story.");
            story = new Story(json.text);
            story.onError += (message, type) =>
            {
                if (type == Ink.ErrorType.Warning) Debug.LogWarning(message);
                else throw new InvalidOperationException(message);
            };
            if (pairKeys != null)
            {
                var links = new InkList();
                if (story.listDefinitions.TryListGetDefinition("city_links", out var definition))
                {
                    links = new InkList("city_links", story);
                    foreach (string key in pairKeys)
                    {
                        if (definition.ContainsItemWithName(key)) links.AddItem(key);
                        else Debug.LogWarning($"Ending: active pair '{key}' is missing from Ink LIST city_links.");
                    }
                }
                else Debug.LogWarning("Ending: declare LIST city_links in Ink to receive the active city pairs.");

                story.BindExternalFunction("get_links", () => new InkList(links), true);
            }
            if (factionSuccess != null)
            {
                foreach (var score in factionSuccess)
                {
                    float value = score.Value;
                    story.BindExternalFunction(score.Key, () => value, true);
                }
            }
            ContinueStory();
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not play the Ink story: {exception.Message}");
            text.text = "The story could not be loaded. Check the Unity Console for details.";
            AddEndButtons();
        }
    }

    private void ContinueStory()
    {
        try
        {
            var output = new StringBuilder();
            while (story.canContinue) output.Append(story.Continue());
            text.text = output.ToString().Trim();
            text.pageToDisplay = 1;
            Canvas.ForceUpdateCanvases();
            text.ForceMeshUpdate();
            ShowPage();
        }
        catch (Exception exception)
        {
            Debug.LogError($"Ink story error: {exception.Message}");
            text.text = "The story could not be read. Check the Unity Console for details.";
            ClearButtons();
            AddEndButtons();
        }
    }

    private void ClearButtons()
    {
        foreach (GameObject button in buttons)
        {
            button.SetActive(false);
            Destroy(button);
        }
        buttons.Clear();
    }

    private void ShowPage()
    {
        ClearButtons();
        int pages = Mathf.Max(1, text.textInfo.pageCount);
        pageNumber.text = pages > 1 ? $"{text.pageToDisplay} / {pages}" : "";
        if (text.pageToDisplay > 1)
            AddButton("Previous", () => { text.pageToDisplay--; ShowPage(); });
        if (text.pageToDisplay < pages)
            AddButton("Next", () => { text.pageToDisplay++; ShowPage(); }, primary: true);
        else
        {
            if (story.currentChoices.Count == 0) AddEndButtons();
            else foreach (Choice choice in story.currentChoices)
            {
                int index = choice.index;
                AddButton(choice.text, () =>
                {
                    story.ChooseChoiceIndex(index);
                    ContinueStory();
                });
            }
        }
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 1f;
    }

    private void AddEndButtons()
    {
        RectTransform row = Rect("Navigation", content);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        buttons.Add(row.gameObject);
        if (onComplete != null)
            AddButton("Begin level 1", () => onComplete(), row, true);
        else if (GameManager.Instance.HasNextLevel)
            AddButton(GameManager.Instance.HasInterlude ? "Continue" : "Next level",
                () => GameManager.Instance.LoadNextLevel(), row, true);
        AddButton("Main menu", () => SceneManager.LoadScene("Main Menu"), row);
    }

    private void BuildPanel(string heading)
    {
        if (panel != null)
        {
            panel.SetActive(false);
            Destroy(panel);
        }
        buttons.Clear();
        var root = new GameObject(heading ?? "Ending story", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        panel = root;
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform background = Rect("Background", root.transform);
        Stretch(background, Vector2.zero, Vector2.zero);
        background.gameObject.AddComponent<Image>().color = new Color(0.99f, 0.985f, 0.97f, 1f);
        background.gameObject.AddComponent<UIInputBlocker>();

        RectTransform viewport = Rect("Reading area", background);
        Stretch(viewport, new Vector2(24, 40), new Vector2(-24, -40));
        viewport.anchorMin = new Vector2(0.14f, 0);
        viewport.anchorMax = new Vector2(0.86f, 1);
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.viewport = viewport;

        content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 24;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;

        if (!string.IsNullOrEmpty(heading))
        {
            TextMeshProUGUI title = Label("Title", content, 44);
            title.text = heading;
            title.alignment = TextAlignmentOptions.Center;
        }
        text = Label("Story", content, 32);
        text.richText = true;
        text.lineSpacing = 10;
        text.paragraphSpacing = 22;
        if (reading)
        {
            Canvas.ForceUpdateCanvases();
            var source = Resources.Load<Font>("Fonts/EBGaramond");
            if (source != null && readingFont == null) readingFont = TMP_FontAsset.CreateFontAsset(source);
            foreach (var label in content.GetComponentsInChildren<TextMeshProUGUI>())
                if (readingFont != null) label.font = readingFont;
            text.overflowMode = TextOverflowModes.Page;
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(180, viewport.rect.height - 220);
            pageNumber = Label("Page number", content, 20);
            pageNumber.alignment = TextAlignmentOptions.Center;
        }
    }

    private void OnDestroy()
    {
        if (readingFont == null) return;
        foreach (Texture2D atlas in readingFont.atlasTextures)
            if (atlas != null) Destroy(atlas);
        Destroy(readingFont.material);
        Destroy(readingFont);
    }

    private void AddButton(string caption, UnityEngine.Events.UnityAction action, RectTransform parent = null, bool primary = false)
    {
        RectTransform rect = Rect("Ending button", parent != null ? parent : content);
        rect.gameObject.AddComponent<Image>().color = primary
            ? new Color(0.24f, 0.34f, 0.46f)
            : new Color(0.9f, 0.89f, 0.86f);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.preferredHeight = 56;
        size.flexibleWidth = 1;
        rect.gameObject.AddComponent<Button>().onClick.AddListener(action);
        TextMeshProUGUI label = Label("Label", rect, 24);
        Stretch(label.rectTransform, new Vector2(12, 4), new Vector2(-12, -4));
        label.alignment = TextAlignmentOptions.Center;
        label.text = caption;
        label.color = primary ? Color.white : new Color(0.17f, 0.16f, 0.14f);
        if (parent == null) buttons.Add(rect.gameObject);
    }

    private static TextMeshProUGUI Label(string name, Transform parent, float size)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = size;
        label.color = new Color(0.17f, 0.16f, 0.14f);
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }
}
