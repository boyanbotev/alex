using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A square perk icon in the city panel; hovering shows the shared stats popup.
public sealed class CityPerkSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float BorderWidth = 2f;
    private Image border, fill, icon;
    private TextMeshProUGUI level, caption;
    private System.Action showPopup;
    private bool showing;

    public static CityPerkSlot Create(Transform parent, float size, float captionWidth)
    {
        var root = new GameObject("Perk Slot", typeof(RectTransform), typeof(Image));
        var slot = root.AddComponent<CityPerkSlot>();
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(size, size);
        slot.border = root.GetComponent<Image>();
        slot.fill = Child<Image>(rect, "Fill", BorderWidth);
        slot.icon = Child<Image>(rect, "Icon", size * .16f);
        slot.icon.preserveAspect = true;
        slot.level = Child<TextMeshProUGUI>(rect, "Level", 3f);
        slot.level.fontSize = 13;
        slot.level.alignment = TextAlignmentOptions.BottomRight;
        slot.caption = Child<TextMeshProUGUI>(rect, "Caption", 0f);
        var captionRect = slot.caption.rectTransform;
        captionRect.anchorMin = new Vector2(.5f, 0); captionRect.anchorMax = new Vector2(.5f, 0);
        captionRect.pivot = new Vector2(.5f, 1);
        captionRect.anchoredPosition = new Vector2(0, -2);
        captionRect.sizeDelta = new Vector2(captionWidth, 14);
        slot.caption.fontSize = 12;
        slot.caption.alignment = TextAlignmentOptions.Top;
        slot.caption.overflowMode = TextOverflowModes.Ellipsis;
        slot.caption.textWrappingMode = TextWrappingModes.NoWrap;
        return slot;
    }

    private static T Child<T>(RectTransform parent, string name, float inset) where T : Graphic
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(T));
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        var graphic = child.GetComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    public void Set(Sprite sprite, int perkLevel, Color frame, Color background, Color text,
        string captionText, bool dimmed, System.Action popup)
    {
        var style = UIStyle.Shared;
        if (style != null && style.interfaceFont != null) level.font = caption.font = style.interfaceFont;
        float alpha = dimmed ? .45f : 1f;
        border.color = new Color(frame.r, frame.g, frame.b, frame.a * alpha);
        fill.color = background;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        icon.color = new Color(1, 1, 1, alpha);
        level.text = perkLevel > 0 ? perkLevel.ToString() : "";
        level.color = text;
        caption.text = captionText ?? "";
        caption.color = text;
        caption.gameObject.SetActive(!string.IsNullOrEmpty(captionText));
        showPopup = popup;
        if (showing) showPopup?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (showPopup == null) return;
        showing = true;
        showPopup();
    }

    public void OnPointerExit(PointerEventData eventData) => HidePopup();
    private void OnDisable() => HidePopup();

    private void HidePopup()
    {
        if (!showing) return;
        showing = false;
        if (UIManager.Instance != null) UIManager.Instance.HideUnitStatsPopup();
    }
}
