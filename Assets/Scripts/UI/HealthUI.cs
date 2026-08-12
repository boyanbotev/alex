using UnityEngine;
using TMPro;

public class HealthUI : MonoBehaviour
{
    [SerializeField] TextMeshPro text;

    public void Set(int health)
    {
        text.text = health.ToString();
    }
}
