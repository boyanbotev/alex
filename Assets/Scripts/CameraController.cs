using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private GridGenerator gridGenerator;

    [Header("Screen Padding")]
    [SerializeField] private float horizontalPadding = 1f;
    [SerializeField] private float verticalPadding = 1f;

    [Header("Pan")]
    [SerializeField] private float panSmoothing = 0.1f;

    private Camera cam;

    private Vector3 dragStartMouseWorld;
    private Vector3 dragStartCameraPosition;
    private bool isDragging;
    private bool startedOverUI;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        HandlePan();
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(0))
        {
            startedOverUI = EventSystem.current.IsPointerOverGameObject();

            if (startedOverUI)
                return;

            dragStartMouseWorld = GetMouseWorldPosition();
            dragStartCameraPosition = transform.position;
            isDragging = true;
        }

        if (Input.GetMouseButton(0) && isDragging && !startedOverUI)
        {
            Vector3 currentMouseWorld = GetMouseWorldPosition();
            Vector3 mouseDelta = currentMouseWorld - dragStartMouseWorld;

            Vector3 targetPosition = dragStartCameraPosition - mouseDelta;

            targetPosition = ClampCameraPosition(targetPosition);

            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                panSmoothing
            );
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

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

        float gridMinX = -horizontalPadding;
        float gridMaxX = gridWidth + horizontalPadding;

        float gridMinZ = -verticalPadding;
        float gridMaxZ = gridHeight + verticalPadding;

        // Get the camera's visible footprint on the ground.
        Vector3[] corners =
        {
            GetGroundPoint(new Vector3(0f, 0f)),
            GetGroundPoint(new Vector3(1f, 0f)),
            GetGroundPoint(new Vector3(0f, 1f)),
            GetGroundPoint(new Vector3(1f, 1f))
        };

        float minOffsetX = float.MaxValue;
        float maxOffsetX = float.MinValue;
        float minOffsetZ = float.MaxValue;
        float maxOffsetZ = float.MinValue;

        foreach (Vector3 corner in corners)
        {
            // The corner is expressed relative to the current camera.
            float offsetX = corner.x - transform.position.x;
            float offsetZ = corner.z - transform.position.z;

            minOffsetX = Mathf.Min(minOffsetX, offsetX);
            maxOffsetX = Mathf.Max(maxOffsetX, offsetX);

            minOffsetZ = Mathf.Min(minOffsetZ, offsetZ);
            maxOffsetZ = Mathf.Max(maxOffsetZ, offsetZ);
        }

        // Camera position must be far enough into the grid that
        // the visible footprint doesn't go beyond its edges.

        float maxX = gridMinX - minOffsetX;
        float minX = gridMaxX - maxOffsetX;

        float maxZ = gridMinZ - minOffsetZ;
        float minZ = gridMaxZ - maxOffsetZ;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.z = Mathf.Clamp(position.z, minZ, maxZ);

        return position;
    }

    private Vector3 GetGroundPoint(Vector3 viewportPosition)
    {
        Ray ray = cam.ViewportPointToRay(viewportPosition);

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return transform.position;
    }
}