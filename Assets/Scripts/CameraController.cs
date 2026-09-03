using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private GridGenerator gridGenerator;

    [Header("Pan")]
    [SerializeField] private float panSmoothing = 0.1f;

    private Camera cam;

    private Vector3 dragStartMouseWorld;
    private bool isDragging;
    private bool startedOverUI;
    private float totalDragDistance;
    private Vector3 targetPosition;
    [SerializeField] private float dragThreshold = 5f;
    private Vector2 lastTouchPosition;
    Plane groundPlane;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        targetPosition = transform.position;
        groundPlane = new Plane(Vector3.up, Vector3.zero);
    }

    private void Update()
    {
        HandlePan();
        ApplyMovement();
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(0))
        {
            startedOverUI = EventSystem.current.IsPointerOverGameObject();

            if (startedOverUI)
                return;

            lastTouchPosition = Input.mousePosition;
            dragStartMouseWorld = GetMouseWorldPosition();
            totalDragDistance = 0f;
        }

        if (Input.GetMouseButton(0) && !startedOverUI)
        {
            Vector2 currentPos = Input.mousePosition;

            float delta = Vector2.Distance(currentPos, lastTouchPosition);
            totalDragDistance += delta;

            if (totalDragDistance > dragThreshold)
            {
                isDragging = true;
                Vector3 currentWorldPos = GetMouseWorldPosition();
                Vector3 diff = dragStartMouseWorld - currentWorldPos;
                targetPosition += diff;

                dragStartMouseWorld = currentWorldPos;
            }
            lastTouchPosition = currentPos;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }

    private void ApplyMovement()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * panSmoothing
        );

        transform.position = ClampCameraPosition(transform.position);
    }

    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return Vector3.zero;
    }

    private Vector3 ClampCameraPosition(Vector3 position)
    {
        float gridWidth =
            gridGenerator.boardSettings.width *
            gridGenerator.tileSize;

        float gridHeight =
            gridGenerator.boardSettings.height *
            gridGenerator.tileSize;

        position.x = Mathf.Clamp(position.x, -gridWidth, 0);
        position.z = Mathf.Clamp(position.z, -gridHeight, 0);

        return position;
    }
}