using TMPro;
using UnityEngine;

public class BuildingInfoPopup : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI buildingNameText;
    [SerializeField] TextMeshProUGUI descriptionText;
    [SerializeField] TextMeshProUGUI costText;

    public void Populate(BuildingData building)
    {
        buildingNameText.text = building.buildingName;
        descriptionText.text = building.description;
        costText.text = building.cost + " stars";
    }
}