using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemPurchaseButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private FactionUnit hoverUnit;
    private bool showingUnit;

    public void SetHoverUnit(FactionUnit unit) => hoverUnit = unit;

    public void RefreshSpawnAvailability(City city)
    {
        if (hoverUnit != null)
            GetComponent<Button>().interactable = city.CanSpawnUnit(hoverUnit, hoverUnit.unitData.cost);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverUnit == null) return;
        showingUnit = true;
        UIManager.Instance.ShowUnitStatsPopup(hoverUnit.unitData);
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
        CostText.text = cost + " stars";
    }

    public void AddListener(UnityAction action)
    {
        Button buttonComponent = GetComponent<Button>();
        buttonComponent.onClick.RemoveAllListeners();
        buttonComponent.onClick.AddListener(action);
    }

    public void AddRefund(int amount)
    {
        CostText.text = "+" + amount + " stars";
    }
}
