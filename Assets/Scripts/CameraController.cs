using UnityEngine;
public class CameraController : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private GridGenerator gridGenerator;

    [Header("Pan")]
    [SerializeField] private float panSmoothing = 0.1f;
    [SerializeField, Min(0.01f)] private float inertiaDamping = 4f;
    [SerializeField, Min(0f)] private float inertiaStopSpeed = 0.01f;

    [SerializeField] private float referenceOrthoSize = 4f;
    [SerializeField] private float referenceScreenHeight = 1080f;

    private Camera cam;

    private Vector3 panVelocity;
    private bool isDragging;
    private bool startedOverUI;
    private float totalDragDistance;
    private Vector3 targetPosition;
    [SerializeField] private float dragThreshold = 5f;
    private Vector2 lastTouchPosition;
    Plane groundPlane;
    private int lastScreenHeight;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        //Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        targetPosition = transform.position;
        groundPlane = new Plane(Vector3.up, Vector3.zero);

        ApplyCameraScale();
    }

    private void Update()
    {
        if (GridGenerator.Instance == null || !GridGenerator.Instance.IsReady) return;
        if (Screen.height != lastScreenHeight)
            ApplyCameraScale();

        HandlePan();
        ApplyMovement();
    }

    private void ApplyCameraScale()
    {
        lastScreenHeight = Screen.height;

        if (cam.orthographic)
        {
            cam.orthographicSize =
                referenceOrthoSize * Screen.height / referenceScreenHeight;
        }
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(0))
        {
            panVelocity = Vector3.zero;
            targetPosition = transform.position;
            isDragging = false;
            startedOverUI = UIRaycastUtility.IsPointerOverBlockingUI(Input.mousePosition);

            if (startedOverUI)
                return;

            lastTouchPosition = Input.mousePosition;
            totalDragDistance = 0f;
        }

        bool released = Input.GetMouseButtonUp(0);
        if ((Input.GetMouseButton(0) || released) && !startedOverUI)
        {
            Vector2 currentPos = Input.mousePosition;

            float delta = Vector2.Distance(currentPos, lastTouchPosition);
            totalDragDistance += delta;

            if (totalDragDistance > dragThreshold)
            {
                isDragging = true;

                Vector3 diff = GetPointerWorldPosition(lastTouchPosition)
                    - GetPointerWorldPosition(currentPos);
                targetPosition = ClampCameraPosition(targetPosition + diff);

                if (!released || delta > 0f)
                    panVelocity = Time.deltaTime > 0f ? diff / Time.deltaTime : Vector3.zero;
            }
            lastTouchPosition = currentPos;
        }

        if (released)
        {
            isDragging = false;
            targetPosition = transform.position;
        }
    }

    private void ApplyMovement()
    {
        if (!Input.GetMouseButton(0))
        {
            float damping = Mathf.Max(0.01f, inertiaDamping);
            float decay = Mathf.Exp(-damping * Time.deltaTime);
            Vector3 nextPosition = transform.position + panVelocity * ((1f - decay) / damping);
            targetPosition = ClampCameraPosition(nextPosition);
            panVelocity *= decay;
            if (targetPosition.x != nextPosition.x) panVelocity.x = 0f;
            if (targetPosition.z != nextPosition.z) panVelocity.z = 0f;
            if (panVelocity.sqrMagnitude <= inertiaStopSpeed * inertiaStopSpeed)
                panVelocity = Vector3.zero;
            transform.position = targetPosition;
            return;
        }

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * panSmoothing
        );

        transform.position = ClampCameraPosition(transform.position);
    }

    private Vector3 GetPointerWorldPosition(Vector2 screenPosition)
    {
        Ray ray = cam.ScreenPointToRay(screenPosition);

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