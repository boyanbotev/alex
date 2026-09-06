using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UIRaycastUtility
{
    public static bool IsPointerOverBlockingUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        GraphicRaycaster[] raycasters = Object.FindObjectsByType<GraphicRaycaster>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (GraphicRaycaster raycaster in raycasters)
        {
            if (raycaster != null && raycaster.isActiveAndEnabled)
            {
                raycaster.Raycast(pointerData, raycastResults);
            }
        }

        foreach (RaycastResult result in raycastResults)
        {
            if (result.gameObject == null)
            {
                continue;
            }

            if (result.gameObject.GetComponentInParent<UIInputBlocker>() != null)
            {
                return true;
            }
        }

        return false;
    }
}
