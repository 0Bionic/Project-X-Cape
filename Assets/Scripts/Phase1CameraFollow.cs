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

    void Awake()
    {
        cam = GetComponent<Camera>();
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