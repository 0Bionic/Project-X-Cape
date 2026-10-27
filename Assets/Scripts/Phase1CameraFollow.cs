using UnityEngine;

[RequireComponent(typeof(Camera))]
public class Phase1CameraFollow : MonoBehaviour
{
    [SerializeField] Phase1Controller player;

    void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<Phase1Controller>();

        FollowPlayer();
    }

    void LateUpdate()
    {
        FollowPlayer();
    }

    void FollowPlayer()
    {
        if (player == null)
            return;

        // Center on the character's body, rather than its feet.
        Collider2D playerCollider = player.GetComponent<Collider2D>();

        Vector3 center = playerCollider != null
            ? playerCollider.bounds.center
            : player.transform.position;

        transform.position = new Vector3(
            center.x,
            center.y,
            transform.position.z
        );
    }
}