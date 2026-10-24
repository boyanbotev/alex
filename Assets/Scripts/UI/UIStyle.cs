using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IUIStyleTarget { void ApplyStyle(); }

[CreateAssetMenu(menuName = "Game/UI Style", fileName = "UI Style")]
public sealed class UIStyle : ScriptableObject
{
    private static UIStyle shared;
    public static UIStyle Shared => shared != null ? shared : shared = Resources.Load<UIStyle>("UI Style");

    [Serializable]
    public sealed class Palette
    {
        public Color background, panel, secondaryPanel, text, mutedText, accent;
        public Color primaryButton, primaryButtonText, secondaryButton, secondaryButtonText;
        public Color captureButton = new Color32(184, 155, 96, 255);
        public Color captureButtonText = new Color32(23, 34, 49, 255);
        public Color incomingMessage, outgoingMessage, speakerName;
        public ColorBlock buttonStates = ColorBlock.defaultColorBlock;
    }

    public Palette light = new Palette();
    public Palette dark = new Palette();
    public bool darkGameplay;
    public TMP_FontAsset interfaceFont;
    public TMP_FontAsset narrativeFont;
    public Color hudOverlayColor = new Color(0.06f, 0.09f, 0.13f, 0.65f);
    public Palette Gameplay => darkGameplay ? dark : light;

    [ContextMenu("Apply to Loaded UI")]
    public void ApplyToLoadedUI()
    {
        // Explicit refresh only; no per-frame scene searches or palette polling.
        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var behaviour in behaviours)
            if (!(behaviour is UIStyleBinding) && behaviour is IUIStyleTarget target) target.ApplyStyle();
        foreach (var behaviour in behaviours)
            if (behaviour is UIStyleBinding binding && !binding.TryGetComponent<Button>(out _)) binding.Apply();
        foreach (var behaviour in behaviours)
            if (behaviour is UIStyleBinding binding && binding.TryGetComponent<Button>(out _)) binding.Apply();
    }

    public void Text(TMP_Text label, Palette palette, bool narrative = false)
    {
        label.color = palette.text;
        var font = narrative ? narrativeFont : interfaceFont;
        if (font != null) label.font = font;
    }

    public void Button(Button button, Palette palette, bool primary = true)
    {
        if (button.targetGraphic == null) button.targetGraphic = button.GetComponent<Image>();
        if (button.targetGraphic != null)
            button.targetGraphic.color = primary ? palette.primaryButton : palette.secondaryButton;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = palette.buttonStates;
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            Text(label, palette);
            label.color = primary ? palette.primaryButtonText : palette.secondaryButtonText;
        }
        // Close buttons use a shaped icon as their target graphic, rather than a filled rectangle.
        if (button.name == "Close" && button.GetComponentInChildren<TMP_Text>(true) == null && button.targetGraphic != null)
            button.targetGraphic.color = palette.secondaryButtonText;
    }

    public void CaptureButton(Button button, Palette palette)
    {
        Button(button, palette);
        if (button.targetGraphic != null) button.targetGraphic.color = palette.captureButton;
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            label.color = palette.captureButtonText;
    }

    // Apply only to a known UI surface: preserve icons, artwork and transparent masks.
    public void Surface(Component root, Palette palette)
    {
        if (root == null) return;
        if (root.TryGetComponent<Image>(out var image)) image.color = palette.panel;
        foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true)) Text(label, palette);
        foreach (var button in root.GetComponentsInChildren<Button>(true))
            Button(button, palette, button.name != "Close" && button.name != "Cancel");
    }
}
