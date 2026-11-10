using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NiceOfficer : MonoBehaviour
{
    [SerializeField, Min(0f)] float chaseSpeed = 4.5f;
    [SerializeField, Min(0.1f)] float viewDistance = 6f;
    [SerializeField, Range(1f, 360f)] float viewAngle = 90f;

    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    Transform player;
    bool chasing;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        TopDownController controller = FindAnyObjectByType<TopDownController>();
        if (controller != null)
            player = controller.transform;
    }

    public void Alert()
    {
        chasing = true;
    }

    void FixedUpdate()
    {
        if (player == null)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        if (LineOfSight.CanSee(transform, player, viewDistance, viewAngle))
            chasing = true;

        if (!chasing)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 offset = (Vector2)player.position - body.position;
        Vector2 direction;
        if (offset.sqrMagnitude > 0.001f)
            direction = offset.normalized;
        else
            direction = Vector2.right;

        body.linearVelocity = direction * chaseSpeed;

        if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f)
            spriteRenderer.flipX = direction.x < 0f;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TopDownController playerHit = collision.collider.GetComponent<TopDownController>();
        if (playerHit == null)
            playerHit = collision.collider.GetComponentInParent<TopDownController>();

        if (playerHit != null)
            playerHit.CaughtByOfficer();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, viewDistance);
    }
}
