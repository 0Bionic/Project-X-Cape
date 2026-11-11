using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class ElonRockProjectile : MonoBehaviour
{
    [SerializeField] float maxLifetime = 5f;
    [SerializeField] string bossBoundaryLayerName = "BossBoundary";

    Rigidbody2D rb;
    int bossBoundaryLayer = -1;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        col.isTrigger = true; // Dont let the rock collide with ElonMah and Minions

        bossBoundaryLayer = LayerMask.NameToLayer(bossBoundaryLayerName);
    }

    public void Launch(Vector2 startPos, Vector2 targetPos, float extraArcHeight, float spinSpeed)
    {
        transform.position = startPos;
        rb.position = startPos;

        float g = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        if (g < 0.01f)
            g = 9.81f;

        // Create upward arc for rock
        float maxY = Mathf.Max(startPos.y, targetPos.y) + Mathf.Max(0.5f, extraArcHeight);
        float vy = Mathf.Sqrt(2f * g * Mathf.Max(0.1f, maxY - startPos.y));

        float timeUp = vy / g;
        float timeDown = Mathf.Sqrt(2f * Mathf.Max(0.01f, maxY - targetPos.y) / g);
        float totalTime = Mathf.Max(0.15f, timeUp + timeDown);

        float vx = (targetPos.x - startPos.x) / totalTime;

        rb.linearVelocity = new Vector2(vx, vy);
        rb.angularVelocity = (vx >= 0f ? -1f : 1f) * spinSpeed;

        Destroy(gameObject, maxLifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<ElonMuskController>() != null ||
            other.GetComponentInParent<BossMinion>() != null ||
            other.GetComponentInParent<ElonRockProjectile>() != null ||
            other.GetComponentInParent<CampfireCheckpoint>() != null)
            return;

        if (bossBoundaryLayer != -1 && other.gameObject.layer == bossBoundaryLayer)
            return;

        Phase1Controller player = other.GetComponentInParent<Phase1Controller>();
        if (player != null)
        {
            player.Hit();
            Destroy(gameObject);
            return;
        }

        // Ignore non-solid triggers 
        if (other.isTrigger)
            return;

        // Let rocks pass through one-way platforms from down to up movements like players, and destroy when falling on platforms
        if (other.GetComponent<PlatformEffector2D>() != null && rb.linearVelocity.y > 0f)
            return;

        Destroy(gameObject);
    }
}
