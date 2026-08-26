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

        // Get the ground position of each screen edge.
        //
        // We only care about the edges, not whether the entire
        // camera footprint fits inside the grid.

        Vector3 left = GetGroundPoint(new Vector3(0f, 0.5f));
        Vector3 right = GetGroundPoint(new Vector3(1f, 0.5f));
        Vector3 bottom = GetGroundPoint(new Vector3(0.5f, 0f));
        Vector3 top = GetGroundPoint(new Vector3(0.5f, 1f));

        // Calculate how much each screen edge is offset from
        // the camera position.

        float leftOffsetX = left.x - transform.position.x;
        float rightOffsetX = right.x - transform.position.x;

        float bottomOffsetZ = bottom.z - transform.position.z;
        float topOffsetZ = top.z - transform.position.z;

        /*
         * Horizontal bounds
         *
         * When panning left:
         *     the LEFT edge of the screen must not go past
         *     the left edge of the grid.
         *
         * When panning right:
         *     the RIGHT edge of the screen must not go past
         *     the right edge of the grid.
         */

        float minX = gridMinX - leftOffsetX;
        float maxX = gridMaxX - rightOffsetX;

        /*
         * Vertical bounds
         *
         * When panning down:
         *     the BOTTOM edge must not go past the bottom
         *     of the grid.
         *
         * When panning up:
         *     the TOP edge must not go past the top
         *     of the grid.
         */

        float minZ = gridMinZ - bottomOffsetZ;
        float maxZ = gridMaxZ - topOffsetZ;

        /*
         * If the camera footprint is larger than the grid,
         * the bounds can become inverted.
         *
         * In that case there is no position satisfying both
         * edges simultaneously. Instead, keep the camera
         * centred over the grid on that axis.
         */

        if (minX <= maxX)
        {
            position.x = Mathf.Clamp(position.x, minX, maxX);
        }
        else
        {
            position.x = (minX + maxX) * 0.5f;
        }

        if (minZ <= maxZ)
        {
            position.z = Mathf.Clamp(position.z, minZ, maxZ);
        }
        else
        {
            position.z = (minZ + maxZ) * 0.5f;
        }

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