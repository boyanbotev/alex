using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Optional explicit binding for scene/prefab elements outside a styled surface.
public sealed class UIStyleBinding : MonoBehaviour
{
    public enum Role { Panel, SecondaryPanel, Text, MutedText, Accent, PrimaryButton, SecondaryButton }
    public Role role;
    public bool dark;

    private void OnEnable() => Apply();

    [ContextMenu("Apply UI Style")]
    public void Apply()
    {
        var style = UIStyle.Shared;
        if (style == null) return;
        var palette = dark ? style.dark : style.light;
        if (role == Role.PrimaryButton || role == Role.SecondaryButton)
        {
            if (TryGetComponent<Button>(out var button)) style.Button(button, palette, role == Role.PrimaryButton);
        }
        else if (TryGetComponent<TMP_Text>(out var text))
        {
            style.Text(text, palette);
            if (role == Role.MutedText) text.color = palette.mutedText;
            else if (role == Role.Accent) text.color = palette.accent;
        }
        else if (TryGetComponent<Graphic>(out var graphic))
            graphic.color = role == Role.Accent ? palette.accent : role == Role.SecondaryPanel ? palette.secondaryPanel : palette.panel;
    }
}
