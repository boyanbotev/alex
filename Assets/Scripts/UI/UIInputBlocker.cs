using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public sealed class UIInputBlocker : MonoBehaviour
{
    private void Awake()
    {
        EnsureRaycastTarget();
    }

    private void Reset()
    {
        EnsureRaycastTarget();
    }

    private void OnValidate()
    {
        EnsureRaycastTarget();
    }

    private void EnsureRaycastTarget()
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.raycastTarget = true;
        }
    }
}
