using UnityEngine;
public class CameraController : MonoBehaviour
{
    [Header("Pan")]
    [SerializeField] private float panSmoothing = 0.1f;
    [SerializeField, Min(0.01f)] private float inertiaDamping = 4f;
    [SerializeField, Min(0f)] private float inertiaStopSpeed = 0.01f;
    [Tooltip("Maximum release speed in world units per second. Zero disables coasting.")]
    [SerializeField, Min(0f)] private float maxPanVelocity = 10f;

    [SerializeField] private float referenceOrthoSize = 4f;
    [SerializeField] private float referenceScreenHeight = 1080f;

    private Camera cam;

    private Vector3 panVelocity;
    private bool isDragging;
    private bool startedOverUI;
    private float totalDragDistance;
    private Vector3 targetPosition;
    [SerializeField] private float dragThreshold = 5f;
    public float DragThreshold => dragThreshold;
    private Vector2 lastTouchPosition;
    Plane groundPlane;
    private int lastScreenHeight;
    private bool framing;
    private Vector3 framingVelocity;

    public void FramePoints(System.Collections.Generic.List<Vector3> points, float panelTop)
    {
        Rect safeArea = Rect.MinMaxRect(48f, panelTop + 48f, Screen.width - 48f, Screen.height - 100f);
        if (safeArea.width <= 0f || safeArea.height <= 0f) return;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool outside = false;
        foreach (var point in points)
        {
            Vector2 screen = cam.WorldToScreenPoint(point);
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
            outside |= !safeArea.Contains(screen);
        }
        if (!outside) return;
        Vector2 shift = safeArea.center - (min + max) * .5f;
        // Keep the source visible when the candidate spread exceeds the viewport.
        Vector2 source = cam.WorldToScreenPoint(points[0]);
        shift.x = Mathf.Clamp(shift.x, safeArea.xMin - source.x, safeArea.xMax - source.x);
        shift.y = Mathf.Clamp(shift.y, safeArea.yMin - source.y, safeArea.yMax - source.y);
        targetPosition = ClampCameraPosition(transform.position +
            GetPointerWorldPosition(safeArea.center) - GetPointerWorldPosition(safeArea.center + shift));
        panVelocity = framingVelocity = Vector3.zero;
        framing = true;
    }

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
        if (TurnManager.Instance != null && TurnManager.Instance.IsGameOver) return;
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
            isDragging = false;
            startedOverUI = UIRaycastUtility.IsPointerOverBlockingUI(Input.mousePosition);

            if (startedOverUI)
                return;

            framing = false;
            targetPosition = transform.position;
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
            panVelocity = Vector3.ClampMagnitude(panVelocity, Mathf.Max(0f, maxPanVelocity));
            if (!framing) targetPosition = transform.position;
        }
    }

    private void ApplyMovement()
    {
        if (framing)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref framingVelocity, .2f);
            if ((transform.position - targetPosition).sqrMagnitude < .0001f)
            {
                transform.position = targetPosition;
                framing = false;
            }
            return;
        }
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
            GameManager.Instance.Level.Width *
            GameManager.Instance.Level.tileSize;

        float gridHeight =
            GameManager.Instance.Level.Height *
            GameManager.Instance.Level.tileSize;

        position.x = Mathf.Clamp(position.x, -16, gridWidth - 16);
        position.z = Mathf.Clamp(position.z, -16, gridHeight - 16);

        return position;
    }
}
