using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class Phase1CameraFollow : MonoBehaviour
{
    [SerializeField] Phase1Controller player;

    [Header("Normal View")]
    [SerializeField, Min(0f)]
    float heightAbovePlayer = 0.15f;

    [Header("Hold W to Look Up")]
    [SerializeField, Range(0.05f, 0.45f)]
    float lookUpPlayerScreenHeight = 0.15f;

    [Header("Hold S to Look Down")]
    [SerializeField, Range(0.55f, 0.95f)]
    float lookDownPlayerScreenHeight = 0.85f;

    [Header("Transition")]
    [Tooltip("Seconds from normal view to either look direction. " +
             "Up to down takes twice this duration.")]
    [SerializeField, Min(0f)]
    float transitionDuration = 0.4f;

    Camera cam;
    Collider2D playerCollider;

    // -1 = looking down, 0 = normal, 1 = looking up.
    float lookBlend;
    float defaultOrthographicSize;
    float zoomStartSize;
    float zoomTargetSize;
    float zoomDuration;
    float zoomElapsed;
    bool isZooming;

    void Awake()
    {
        cam = GetComponent<Camera>();
        defaultOrthographicSize = cam.orthographicSize;
        zoomTargetSize = defaultOrthographicSize;
    }

    public void SetOrthographicSize(float targetSize, float duration = 1f)
    {
        if (cam == null)
            return;

        zoomStartSize = cam.orthographicSize;
        zoomTargetSize = Mathf.Max(1f, targetSize);
        zoomDuration = Mathf.Max(0f, duration);
        zoomElapsed = 0f;

        if (zoomDuration <= 0f)
        {
            cam.orthographicSize = zoomTargetSize;
            isZooming = false;
        }
        else
        {
            isZooming = true;
        }
    }

    public void ResetOrthographicSize(float duration = 1f)
    {
        SetOrthographicSize(defaultOrthographicSize, duration);
    }

    void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<Phase1Controller>();

        if (player != null)
            playerCollider = player.GetComponent<Collider2D>();

        FollowPlayer();
    }

    void LateUpdate()
    {
        if (isZooming && cam != null)
        {
            zoomElapsed += Time.deltaTime;
            float t = zoomDuration > 0f ? Mathf.Clamp01(zoomElapsed / zoomDuration) : 1f;
            cam.orthographicSize = Mathf.Lerp(zoomStartSize, zoomTargetSize, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f)
                isZooming = false;
        }

        Keyboard keyboard = Keyboard.current;

        bool lookingUp = keyboard != null && keyboard.wKey.isPressed;
        bool lookingDown = keyboard != null && keyboard.sKey.isPressed;

        float targetBlend = 0f;

        if (lookingUp && !lookingDown)
            targetBlend = 1f;
        else if (lookingDown && !lookingUp)
            targetBlend = -1f;

        if (transitionDuration <= 0f)
        {
            lookBlend = targetBlend;
        }
        else
        {
            lookBlend = Mathf.MoveTowards(
                lookBlend,
                targetBlend,
                Time.deltaTime / transitionDuration
            );
        }

        FollowPlayer();
    }

    void FollowPlayer()
    {
        if (player == null)
            return;

        Vector3 center = playerCollider != null
            ? playerCollider.bounds.center
            : player.transform.position;

        float lookUpOffset = Mathf.Max(
            heightAbovePlayer,
            cam.orthographicSize * (1f - 2f * lookUpPlayerScreenHeight)
        );

        float lookDownOffset = cam.orthographicSize
                               * (1f - 2f * lookDownPlayerScreenHeight);

        float destinationOffset = lookBlend >= 0f
            ? lookUpOffset
            : lookDownOffset;

        float easedBlend = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.Abs(lookBlend)
        );

        float verticalOffset = Mathf.Lerp(
            heightAbovePlayer,
            destinationOffset,
            easedBlend
        );

        transform.position = new Vector3(
            center.x,
            center.y + verticalOffset,
            transform.position.z
        );
    }
}