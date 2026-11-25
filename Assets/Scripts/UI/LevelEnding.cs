using System;
using System.Collections.Generic;
using System.Text;
using Ink.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Story screen layout: title on top, footer of buttons pinned to the bottom, and the paged text filling
// whatever height is left. The footer reserves room for the passage's choices before pagination, so
// choices can never be pushed below the screen edge.
public sealed class LevelEnding : MonoBehaviour
{
    private const float RowHeight = 56;
    private const float MinRowHeight = 36;
    private const float RowSpacing = 12;
    private const float MaxFooterShare = 0.5f;

    private Story story;
    private TextMeshProUGUI text;
    private RectTransform area;
    private RectTransform footer;
    private LayoutElement footerSize;
    private Action onComplete;
    private Action addFinalButtons;
    private int finalRows;
    private float rowHeight = RowHeight;
    private Vector2 measuredSize;
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
            summary.AppendLine(pair);
            hasBonds = true;
        }
        if (hasBonds) summary.AppendLine();
        foreach (var score in factionScores)
            summary.AppendLine($"{score.Key.name}: {score.Value:P0}");
        text.text = summary.ToString().TrimEnd();
        ShowPassage(() => AddButton("Continue", () => ShowStory(json, null, null, pairKeys, factionSuccess), primary: true), 1);
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
            ShowPassage(AddEndButtons, 1);
        }
    }

    private void ContinueStory()
    {
        try
        {
            var output = new StringBuilder();
            while (story.canContinue) output.Append(story.Continue());
            text.text = output.ToString().Trim();
            var choices = story.currentChoices;
            if (choices.Count == 0) ShowPassage(AddEndButtons, 1);
            else ShowPassage(() =>
            {
                foreach (Choice choice in choices)
                {
                    int index = choice.index;
                    AddButton(choice.text, () =>
                    {
                        story.ChooseChoiceIndex(index);
                        ContinueStory();
                    });
                }
            }, choices.Count);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Ink story error: {exception.Message}");
            text.text = "The story could not be read. Check the Unity Console for details.";
            ShowPassage(AddEndButtons, 1);
        }
    }

    // finalRows: how many button rows the last page needs (one per choice, or one for a single row of buttons).
    private void ShowPassage(Action addButtons, int rows)
    {
        addFinalButtons = addButtons;
        finalRows = Mathf.Max(1, rows);
        text.pageToDisplay = 1;
        Paginate();
        ShowPage();
    }

    // Reserve footer space for the busiest page, then let TMP paginate the text into the remaining height.
    private void Paginate()
    {
        Canvas.ForceUpdateCanvases();
        ReserveFooter(finalRows);
        // A multi-page passage also shows "Previous" above the choices on its last page.
        if (PageCount() > 1) ReserveFooter(finalRows + 1);
        text.pageToDisplay = Mathf.Clamp(text.pageToDisplay, 1, PageCount());
        measuredSize = area.rect.size;
    }

    private void ReserveFooter(int rows)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);
        float available = area.rect.height * MaxFooterShare - RowSpacing * (rows - 1);
        rowHeight = Mathf.Clamp(available / rows, MinRowHeight, RowHeight);
        footerSize.minHeight = footerSize.preferredHeight = rows * rowHeight + (rows - 1) * RowSpacing;
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);
    }

    private int PageCount()
    {
        text.ForceMeshUpdate();
        return Mathf.Max(1, text.textInfo.pageCount);
    }

    private void Update()
    {
        // Re-flow the text when the reading area changes size (window resize, canvas scaler settling).
        if (panel == null || addFinalButtons == null) return;
        if ((area.rect.size - measuredSize).sqrMagnitude < 1f) return;
        Paginate();
        ShowPage();
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
        int pages = PageCount();
        pageNumber.text = pages > 1 ? $"{text.pageToDisplay} / {pages}" : "";
        if (text.pageToDisplay < pages)
        {
            RectTransform row = Row();
            if (text.pageToDisplay > 1)
                AddButton("Previous", () => { text.pageToDisplay--; ShowPage(); }, row);
            AddButton("Next", () => { text.pageToDisplay++; ShowPage(); }, row, true);
        }
        else
        {
            if (pages > 1) AddButton("Previous", () => { text.pageToDisplay--; ShowPage(); });
            addFinalButtons?.Invoke();
        }
    }

    private RectTransform Row()
    {
        RectTransform row = Rect("Navigation", footer);
        LayoutElement size = row.gameObject.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = rowHeight;
        size.flexibleHeight = 0;
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        buttons.Add(row.gameObject);
        return row;
    }

    private void AddEndButtons()
    {
        RectTransform row = Row();
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
        addFinalButtons = null;
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
        background.gameObject.AddComponent<Image>();
        UIStyleBinding.Bind(background, UIStyleBinding.Role.Background);
        background.gameObject.AddComponent<UIInputBlocker>();

        area = Rect("Reading area", background);
        Stretch(area, new Vector2(24, 40), new Vector2(-24, -40));
        area.anchorMin = new Vector2(0.14f, 0);
        area.anchorMax = new Vector2(0.86f, 1);
        VerticalLayoutGroup layout = area.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 20;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        if (!string.IsNullOrEmpty(heading))
        {
            TextMeshProUGUI title = Label("Title", area, 44);
            title.text = heading;
            title.alignment = TextAlignmentOptions.Center;
        }
        text = Label("Story", area, 32);
        text.richText = true;
        text.lineSpacing = 10;
        text.paragraphSpacing = 22;
        text.overflowMode = TextOverflowModes.Page;
        // Zero preferred height so the text only takes the space left after the title and footer.
        LayoutElement textSize = text.gameObject.AddComponent<LayoutElement>();
        textSize.minHeight = 0;
        textSize.preferredHeight = 0;
        textSize.flexibleHeight = 1;

        pageNumber = Label("Page number", area, 20);
        UIStyleBinding.Bind(pageNumber, UIStyleBinding.Role.MutedText);
        pageNumber.alignment = TextAlignmentOptions.Center;

        footer = Rect("Choices", area);
        footerSize = footer.gameObject.AddComponent<LayoutElement>();
        // Child rows force-expand their buttons, which would otherwise report flexible height up through the
        // footer and make it share the spare space with the story text.
        footerSize.flexibleHeight = 0;
        VerticalLayoutGroup footerLayout = footer.gameObject.AddComponent<VerticalLayoutGroup>();
        footerLayout.spacing = RowSpacing;
        footerLayout.childControlWidth = true;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = true;
        footerLayout.childForceExpandHeight = false;
        footerLayout.childAlignment = TextAnchor.LowerCenter;

        if (reading)
        {
            var source = Resources.Load<Font>("Fonts/EBGaramond");
            if (UIStyle.Shared.narrativeFont == null && source != null && readingFont == null) readingFont = TMP_FontAsset.CreateFontAsset(source);
            foreach (var label in new[] { area.Find("Title")?.GetComponent<TextMeshProUGUI>(), text })
            {
                if (label == null) continue;
                if (UIStyle.Shared.narrativeFont != null) label.font = UIStyle.Shared.narrativeFont;
                else if (readingFont != null) label.font = readingFont;
                UIStyleBinding.Bind(label, UIStyleBinding.Role.NarrativeText);
            }
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
        RectTransform rect = Rect("Ending button", parent != null ? parent : footer);
        rect.gameObject.AddComponent<Image>();
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = rowHeight;
        size.flexibleWidth = 1;
        var button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        TextMeshProUGUI label = Label("Label", rect, 24);
        Stretch(label.rectTransform, new Vector2(12, 4), new Vector2(-12, -4));
        label.alignment = TextAlignmentOptions.Center;
        // Long choices shrink to fit their row instead of spilling out of the button.
        label.enableAutoSizing = true;
        label.fontSizeMin = 14;
        label.fontSizeMax = 24;
        label.text = caption;
        UIStyleBinding.Bind(button, primary ? UIStyleBinding.Role.PrimaryButton : UIStyleBinding.Role.SecondaryButton);
        if (parent == null) buttons.Add(rect.gameObject);
    }

    private static TextMeshProUGUI Label(string name, Transform parent, float size)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = size;
        UIStyleBinding.Bind(label, UIStyleBinding.Role.Text);
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
