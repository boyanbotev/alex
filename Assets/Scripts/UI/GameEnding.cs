using System;
using System.Collections.Generic;
using System.Text;
using Ink.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameEnding : MonoBehaviour
{
    private Story story;
    private TextMeshProUGUI text;
    private RectTransform content;
    private ScrollRect scroll;
    private readonly List<GameObject> buttons = new();

    public static string PairKey(string a, string b)
    {
        a = a.Trim().ToLowerInvariant().Replace(' ', '_');
        b = b.Trim().ToLowerInvariant().Replace(' ', '_');
        return string.CompareOrdinal(a, b) <= 0 ? a + "_" + b : b + "_" + a;
    }

    public void Show(TextAsset json, IEnumerable<string> pairKeys, bool won)
    {
        BuildPanel(won);
        try
        {
            if (json == null) throw new InvalidOperationException("Assign an exported Ink JSON to TurnManager's Ending Story.");
            story = new Story(json.text);
            story.onError += (message, type) =>
            {
                if (type == Ink.ErrorType.Warning) Debug.LogWarning(message);
                else throw new InvalidOperationException(message);
            };
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
            ContinueStory();
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not play the Ink ending: {exception.Message}");
            text.text = "The ending could not be loaded. Check the Unity Console for details.";
            AddMenuButton();
        }
    }

    private void ContinueStory()
    {
        foreach (GameObject button in buttons)
        {
            button.SetActive(false);
            Destroy(button);
        }
        buttons.Clear();
        try
        {
            var output = new StringBuilder();
            while (story.canContinue) output.Append(story.Continue());
            text.text = output.ToString().Trim();
            if (story.currentChoices.Count == 0) AddMenuButton();
            else foreach (Choice choice in story.currentChoices)
            {
                int index = choice.index;
                AddButton(choice.text, () =>
                {
                    story.ChooseChoiceIndex(index);
                    ContinueStory();
                });
            }
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Ink ending error: {exception.Message}");
            text.text = "The ending could not be read. Check the Unity Console for details.";
            AddMenuButton();
        }
    }

    private void AddMenuButton() => AddButton("Main menu", () => SceneManager.LoadScene("Main Menu"));

    private void BuildPanel(bool won)
    {
        var root = new GameObject("Game ending", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
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
        background.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.045f, 0.06f, 0.98f);
        background.gameObject.AddComponent<UIInputBlocker>();

        RectTransform viewport = Rect("Reading area", background);
        Stretch(viewport, new Vector2(120, 60), new Vector2(-120, -60));
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

        TextMeshProUGUI title = Label("Title", content, 40);
        title.text = won ? "Victory" : "Game over";
        text = Label("Story", content, 26);
        text.richText = true;
    }

    private void AddButton(string caption, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Rect("Ending button", content);
        rect.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.2f, 0.27f);
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
        rect.gameObject.AddComponent<Button>().onClick.AddListener(action);
        TextMeshProUGUI label = Label("Label", rect, 24);
        Stretch(label.rectTransform, new Vector2(12, 4), new Vector2(-12, -4));
        label.alignment = TextAlignmentOptions.Center;
        label.text = caption;
        buttons.Add(rect.gameObject);
    }

    private static TextMeshProUGUI Label(string name, Transform parent, float size)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = size;
        label.color = new Color(0.94f, 0.93f, 0.9f);
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
