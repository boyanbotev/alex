using System.Linq.Expressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ItemPurchaseButton : MonoBehaviour
{
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
        buttonComponent.onClick.AddListener(action);
    }
}
