using TMPro;
using UnityEngine;

public class CityStarsUI : MonoBehaviour
{
    [SerializeField] TextMeshPro text;
    public void Set(string cityName, int stars)
    {
        text.text = $"{cityName} · {stars} stars";
    }
}
