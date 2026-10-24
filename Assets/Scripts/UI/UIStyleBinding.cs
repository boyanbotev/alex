using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Optional explicit binding for scene/prefab elements outside a styled surface.
public sealed class UIStyleBinding : MonoBehaviour
{
    public enum Role { Panel, SecondaryPanel, Text, MutedText, Accent, PrimaryButton, SecondaryButton,
        Background, IncomingMessage, OutgoingMessage, SpeakerName, NarrativeText }
    public Role role;
    public bool dark;
    public bool gameplay;

    public static void Bind(Component target, Role role, bool dark = false, bool gameplay = false)
    {
        var binding = target.GetComponent<UIStyleBinding>();
        if (binding == null) binding = target.gameObject.AddComponent<UIStyleBinding>();
        binding.role = role;
        binding.dark = dark;
        binding.gameplay = gameplay;
        binding.Apply();
    }

    private void OnEnable() => Apply();

    [ContextMenu("Apply UI Style")]
    public void Apply()
    {
        var style = UIStyle.Shared;
        if (style == null) return;
        var palette = gameplay ? style.Gameplay : dark ? style.dark : style.light;
        if (role == Role.PrimaryButton || role == Role.SecondaryButton)
        {
            if (TryGetComponent<Button>(out var button)) style.Button(button, palette, role == Role.PrimaryButton);
        }
        else if (TryGetComponent<TMP_Text>(out var text))
        {
            // The owning button applies its label colour, including after re-enabling a screen.
            if (text.GetComponentInParent<Button>() != null) return;
            style.Text(text, palette, role == Role.NarrativeText);
            if (role == Role.MutedText) text.color = palette.mutedText;
            else if (role == Role.Accent) text.color = palette.accent;
            else if (role == Role.SpeakerName) text.color = palette.speakerName;
        }
        else if (TryGetComponent<Graphic>(out var graphic))
            graphic.color = role switch
            {
                Role.Accent => palette.accent,
                Role.SecondaryPanel => palette.secondaryPanel,
                Role.Background => palette.background,
                Role.IncomingMessage => palette.incomingMessage,
                Role.OutgoingMessage => palette.outgoingMessage,
                _ => palette.panel
            };
    }
}
