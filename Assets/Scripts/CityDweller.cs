using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CityDweller : MonoBehaviour
{
    [SerializeField, Min(0f)] float evadeSpeed = 3.5f;
    [SerializeField, Min(0.1f)] float viewDistance = 5f;
    [SerializeField, Range(1f, 360f)] float viewAngle = 120f;
    [SerializeField, Min(0.1f)] float alertRadius = 6f;

    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    Transform player;

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

        bool seesPlayer = LineOfSight.CanSee(transform, player, viewDistance, viewAngle);
        if (!seesPlayer)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 offset = (Vector2)player.position - body.position;
        Vector2 direction;
        if (offset.sqrMagnitude > 0.001f)
            direction = -offset.normalized;
        else
            direction = Vector2.left;

        body.linearVelocity = direction * evadeSpeed;
        AlertOfficers();

        if (spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f)
            spriteRenderer.flipX = direction.x < 0f;
    }

    void AlertOfficers()
    {
        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, alertRadius);
        for (int i = 0; i < nearby.Length; i++)
        {
            NiceOfficer officer = nearby[i].GetComponent<NiceOfficer>();
            if (officer == null)
                officer = nearby[i].GetComponentInParent<NiceOfficer>();
            if (officer != null)
                officer.Alert();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, alertRadius);
    }
}
