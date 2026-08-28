using TMPro;
using UnityEngine;

public class PopulationUI : MonoBehaviour
{
    [SerializeField] TextMeshPro text;
    public void Set(int population, int max)
    {
        text.text = $"{population}/{max}";
    }
}
