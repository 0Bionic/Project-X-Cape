using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NiceOfficer : MonoBehaviour
{
    enum Steering
    {
        Chase,
        Evade
    }

    [SerializeField, Min(0f)] float chaseSpeed = 3.5f;
    [SerializeField, Min(0f)] float evadeSpeed = 4.5f;
    [SerializeField, Min(0.1f)] float catchDistance = 1.25f;
    [SerializeField, Min(0.1f)] float resumeChaseDistance = 4f;

    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    Transform player;
    Steering steering = Steering.Chase;

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

    void FixedUpdate()
    {
        if (player == null)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 offset = (Vector2)player.position - body.position;
        float distance = offset.magnitude;

        if (steering == Steering.Chase && distance <= catchDistance)
            steering = Steering.Evade;
        else if (steering == Steering.Evade && distance >= resumeChaseDistance)
            steering = Steering.Chase;

        Vector2 direction;
        if (distance > 0.001f)
            direction = offset / distance;
        else
            direction = Vector2.right;

        if (steering == Steering.Evade)
            direction = -direction;

        float speed;
        if (steering == Steering.Chase)
            speed = chaseSpeed;
        else
            speed = evadeSpeed;
        body.linearVelocity = direction * speed;

        if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f)
            spriteRenderer.flipX = direction.x < 0f;
    }

    void OnValidate()
    {
        if (resumeChaseDistance < catchDistance)
            resumeChaseDistance = catchDistance;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, catchDistance);
        Gizmos.color = new Color(0.3f, 0.75f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, resumeChaseDistance);
    }
}
