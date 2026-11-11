using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class PlayerBullet : MonoBehaviour
{
    [SerializeField] float defaultSpeed = 28f;
    [SerializeField] float defaultDamage = 5f;
    [SerializeField] float maxLifetime = 2.5f;
    [SerializeField] string bossBoundaryLayerName = "BossBoundary";

    Rigidbody2D rb;
    float damage;
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

        damage = defaultDamage;
        bossBoundaryLayer = LayerMask.NameToLayer(bossBoundaryLayerName);
    }

    public void Launch(Vector2 direction, float speed, float bulletDamage, float lifetime)
    {
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        damage = bulletDamage > 0f ? bulletDamage : defaultDamage;
        float finalSpeed = speed > 0f ? speed : defaultSpeed;
        float finalLifetime = lifetime > 0f ? lifetime : maxLifetime;

        transform.right = dir;
        rb.linearVelocity = dir * finalSpeed;

        Destroy(gameObject, finalLifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hitConsumed)
            return;

        if (other.GetComponentInParent<Phase1Controller>() != null ||
            other.GetComponentInParent<Phase3PlayerCombat>() != null ||
            other.GetComponentInParent<PlayerBullet>() != null ||
            other.GetComponentInParent<ElonRockProjectile>() != null ||
            other.GetComponentInParent<CampfireCheckpoint>() != null)
            return;

        if (bossBoundaryLayer != -1 && other.gameObject.layer == bossBoundaryLayer)
            return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            hitConsumed = true;
            damageable.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (other.isTrigger)
            return;

        // Let the bullets pass through the floating platforms (Jump through mechanic)
        if (other.GetComponent<PlatformEffector2D>() != null)
            return;

        hitConsumed = true;
        Destroy(gameObject);
    }
}
