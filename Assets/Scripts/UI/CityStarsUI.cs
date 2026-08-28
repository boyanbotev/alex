using TMPro;
using UnityEngine;

public class CityStarsUI : MonoBehaviour
{
    [SerializeField] TextMeshPro text;
    public void Set(int stars)
    {
        text.text = $"{stars} stars";
    }
}
