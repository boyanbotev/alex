using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class OverworldController : MonoBehaviour
{
    [SerializeField] private GameData game;
    [SerializeField] private CharacterPortrait[] portraits = Array.Empty<CharacterPortrait>();

    [Serializable]
    private sealed class CharacterPortrait
    {
        public string speaker;
        public Texture2D image;
    }
    private InterludeConversation conversation;
    private RectTransform messages, replies;
    private ScrollRect scroll;
    private TextMeshProUGUI status;
    private int renderedMessages;
    private bool failed;
    private bool leaving;

    private void Start()
    {
        BuildUI();
        WorldLoadingOverlay.Hide();
        string checkpoint = JsonUtility.ToJson(SaveManager.Campaign);
        try
        {
            var campaign = SaveManager.Campaign;
            if (game == null || campaign.levelIndex < 0 || campaign.levelIndex >= game.levels.Length)
                throw new InvalidOperationException("The saved level is missing from the game catalog.");
            var level = game.levels[campaign.levelIndex];
            if (campaign.phase != CampaignPhase.Interlude || level == null || level.interludeStory == null)
                throw new InvalidOperationException("There is no active interlude. Enter from a level ending or Continue.");
            conversation = new InterludeConversation(level.interludeStory.text, campaign);
            conversation.Advance();
            SaveManager.Save();
            Render();
        }
        catch (Exception exception)
        {
            JsonUtility.FromJsonOverwrite(checkpoint, SaveManager.Campaign);
            ShowError(exception);
        }
    }

    private void Render()
    {
        var history = SaveManager.Campaign.messages;
        while (renderedMessages < history.Count) AddMessage(history[renderedMessages++]);
        foreach (Transform child in replies)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        foreach (var choice in conversation.Story.currentChoices)
        {
            int index = choice.index;
            Button(choice.text, replies, () => Choose(index));
        }
        if (!conversation.Story.canContinue && conversation.Story.currentChoices.Count == 0)
        {
            if (status != null) status.text = "Conversation complete";
            bool hasNext = SaveManager.LevelIndex + 1 < game.levels.Length && game.levels[SaveManager.LevelIndex + 1] != null;
            Button(hasNext ? "Next level" : "Finish", replies, Finish);
        }
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 0;
    }

    private void Choose(int index)
    {
        if (failed || leaving) return;
        // A malformed story must not leave half-applied effects in the cached save.
        string checkpoint = JsonUtility.ToJson(SaveManager.Campaign);
        try
        {
            conversation.Choose(index);
            SaveManager.Save();
            Render();
        }
        catch (Exception exception)
        {
            JsonUtility.FromJsonOverwrite(checkpoint, SaveManager.Campaign);
            ShowError(exception);
        }
    }

    private void Finish()
    {
        if (failed || leaving) return;
        leaving = true;
        int next = SaveManager.LevelIndex + 1;
        bool hasNext = next < game.levels.Length && game.levels[next] != null;
        SaveManager.SetLevelIndex(hasNext ? next : SaveManager.LevelIndex);
        if (hasNext) GameManager.LoadGame();
        else SceneManager.LoadScene("Main Menu");
    }

    private void ShowError(Exception exception)
    {
        failed = true;
        Debug.LogException(exception);
        foreach (Transform child in replies) child.gameObject.SetActive(false);
        status = Label("Conversation could not be loaded. See the Unity Console.", replies, 18);
    }

    private void BuildUI()
    {
        var camera = new GameObject("Overworld Camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetParent(transform, false);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.06f);
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var root = new GameObject("Messenger", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        var background = Rect("Background", root.transform);
        Stretch(background, Vector2.zero, Vector2.zero);
        background.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.045f, 0.06f);
        var panel = Rect("Conversation", background);
        panel.anchorMin = new Vector2(0.18f, 0);
        panel.anchorMax = new Vector2(0.82f, 1);
        panel.offsetMin = new Vector2(0, 24);
        panel.offsetMax = new Vector2(0, -24);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        Label("Messages", panel, 30);
        var viewport = Rect("Message history", panel);
        viewport.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.viewport = viewport;
        messages = Rect("Messages", viewport);
        messages.anchorMin = new Vector2(0, 1);
        messages.anchorMax = Vector2.one;
        messages.pivot = new Vector2(0.5f, 1);
        messages.sizeDelta = Vector2.zero;
        Stack(messages, 12);
        messages.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = messages;
        replies = Rect("Replies", panel);
        Stack(replies, 8);
        Button("Main menu", panel, () => SceneManager.LoadScene("Main Menu"));
    }

    private void AddMessage(CampaignMessage message)
    {
        var row = Rect("Message", messages);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = message.outgoing ? new RectOffset(100, 8, 0, 0) : new RectOffset(8, 100, 0, 0);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 12;
        if (!message.outgoing)
        {
            var portrait = Array.Find(portraits, p => p != null &&
                string.Equals(p.speaker, message.speaker, StringComparison.OrdinalIgnoreCase));
            if (portrait?.image != null)
            {
                var frame = Rect("Portrait", row);
                var size = frame.gameObject.AddComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = 64;
                size.minHeight = size.preferredHeight = 64;
                size.flexibleWidth = 0;
                var icon = Rect("Image", frame);
                var image = icon.gameObject.AddComponent<RawImage>();
                image.texture = portrait.image;
                image.raycastTarget = false;
                var aspect = icon.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = (float)portrait.image.width / portrait.image.height;
            }
        }
        var bubble = Rect("Bubble", row);
        bubble.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        bubble.gameObject.AddComponent<Image>().color = message.outgoing
            ? new Color(0.12f, 0.30f, 0.27f) : new Color(0.12f, 0.16f, 0.21f);
        Stack(bubble, 4).padding = new RectOffset(16, 16, 12, 12);
        Label(message.speaker, bubble, 17).color = new Color(0.55f, 0.8f, 0.75f);
        Label(message.text, bubble, 23);
    }

    private static VerticalLayoutGroup Stack(RectTransform parent, float spacing)
    {
        var layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    private static TextMeshProUGUI Label(string value, Transform parent, float size)
    {
        var label = Rect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = size;
        label.color = new Color(0.94f, 0.95f, 0.96f);
        label.richText = false;
        label.raycastTarget = false;
        return label;
    }

    private static void Button(string caption, Transform parent, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect("Button", parent);
        rect.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.26f, 0.32f);
        var size = rect.gameObject.AddComponent<LayoutElement>();
        size.minHeight = 52;
        var layout = Stack(rect, 0);
        layout.padding = new RectOffset(14, 14, 10, 10);
        Label(caption, rect, 22).alignment = TextAlignmentOptions.Center;
        rect.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(action);
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
