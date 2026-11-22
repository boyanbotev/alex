using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemPurchaseButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private void Awake()
    {
        var style = UIStyle.Shared;
        if (style != null) style.Button(GetComponent<Button>(), style.Gameplay);
    }
    private FactionUnit hoverUnit;
    private bool showingUnit;

    private System.Action hoverPopup;

    public void SetHoverUnit(FactionUnit unit) => hoverUnit = unit;
    // For buttons that are not units, e.g. the perk upgrade; hidden with the unit popup.
    public void SetHoverPopup(System.Action show) => hoverPopup = show;

    public void RefreshSpawnAvailability(City city)
    {
        if (hoverUnit != null)
            GetComponent<Button>().interactable = city.CanSpawnUnit(hoverUnit, hoverUnit.unitData.cost);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverUnit == null && hoverPopup == null) return;
        showingUnit = true;
        if (hoverUnit != null) UIManager.Instance.ShowUnitStatsPopup(hoverUnit.unitData);
        else hoverPopup();
    }

    public void OnPointerExit(PointerEventData eventData) => HideUnit();

    private void OnDisable() => HideUnit();

    private void HideUnit()
    {
        if (!showingUnit) return;
        showingUnit = false;
        if (UIManager.Instance != null) UIManager.Instance.HideUnitStatsPopup();
    }
    [SerializeField] TextMeshProUGUI ItemText;
    [SerializeField] TextMeshProUGUI CostText;

    public void AddText(string text)
    {
        ItemText.text = text;
    }

    public void AddCost(int cost) 
    { 
        CostText.text = CurrencyUI.Amount(cost);
    }

    public void AddListener(UnityAction action)
    {
        Button buttonComponent = GetComponent<Button>();
        buttonComponent.onClick.RemoveAllListeners();
        buttonComponent.onClick.AddListener(action);
    }

    public void AddRefund(int amount)
    {
        CostText.text = CurrencyUI.Refund(amount);
    }
}
