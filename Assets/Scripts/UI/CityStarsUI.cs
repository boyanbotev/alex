using TMPro;
using UnityEngine;

public class CityStarsUI : MonoBehaviour
{
    [SerializeField] TextMeshPro text;
    public void Set(City city, int stars)
    {
        string perks = CityPerkIcons.Row(city);
        text.text = $"{city.cityName} · {stars} stars" + (perks.Length > 0 ? "\n<size=160%>" + perks + "</size>" : "");
    }
}
