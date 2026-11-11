using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class ElonRocketProjectile : MonoBehaviour
{
    [SerializeField] float defaultSpeed = 14f;
    [SerializeField] float maxLifetime = 4f;
    [SerializeField] bool blockedByOneWayPlatforms;
    [SerializeField] string bossBoundaryLayerName = "BossBoundary";

    bool? OVERRIDE_blockedByPlatforms;

    Rigidbody2D rb;
    int bossBoundaryLayer = -1;
    bool hitConsumed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;

        bossBoundaryLayer = LayerMask.NameToLayer(bossBoundaryLayerName);
    }

    public void Launch(Vector2 direction, float speed, bool? overrideBlockedByPlatforms = null)
    {
        OVERRIDE_blockedByPlatforms = overrideBlockedByPlatforms;
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        float finalSpeed = speed > 0f ? speed : defaultSpeed;

        transform.right = dir;
        rb.linearVelocity = dir * finalSpeed;

        Destroy(gameObject, maxLifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hitConsumed)
            return;

        if (other.GetComponentInParent<ElonMuskController>() != null ||
            other.GetComponentInParent<BossMinion>() != null ||
            other.GetComponentInParent<ElonHunterDrone>() != null ||
            other.GetComponentInParent<ElonRockProjectile>() != null ||
            other.GetComponentInParent<ElonRocketProjectile>() != null ||
            other.GetComponentInParent<PlayerBullet>() != null ||
            other.GetComponentInParent<CampfireCheckpoint>() != null)
            return;

        if (bossBoundaryLayer != -1 && other.gameObject.layer == bossBoundaryLayer)
            return;

        Phase1Controller player = other.GetComponentInParent<Phase1Controller>();
        if (player != null)
        {
            hitConsumed = true;
            player.Hit();
            Destroy(gameObject);
            return;
        }

        if (other.isTrigger)
            return;

        bool blockPlatforms = OVERRIDE_blockedByPlatforms ?? blockedByOneWayPlatforms;
        if (!blockPlatforms && other.GetComponent<PlatformEffector2D>() != null)
            return;

        hitConsumed = true;
        Destroy(gameObject);
    }
}
