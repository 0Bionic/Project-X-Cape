using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class BossMinion : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)] float moveSpeed = 3.5f;
    [SerializeField, Min(0.1f)] float acceleration = 14f;
    [SerializeField, Min(0f)] float stopDistance = 0.2f;
    [SerializeField] bool spriteFacesRight = true;

    [Header("Health & Lifetime")]
    [SerializeField, Min(1f)] float maxHealth = 20f;
    [Tooltip("Auto-despawn timer in seconds (set 0 once Glorb can defeat minions directly).")]
    [SerializeField, Min(0f)] float autoDespawnSeconds = 12f;

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    Transform target;
    float currentHealth;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        currentHealth = maxHealth;

        if (autoDespawnSeconds > 0f)
            Destroy(gameObject, autoDespawnSeconds);
    }

    void Start()
    {
        Phase1Controller player = FindAnyObjectByType<Phase1Controller>();
        if (player != null)
            target = player.transform;
    }

    void Update()
    {
        if (spriteRenderer == null)
            return;

        float vx = rb.linearVelocity.x;
        bool movingRight = Mathf.Abs(vx) > 0.05f
            ? vx > 0f
            : (target != null && target.position.x > transform.position.x);

        spriteRenderer.flipX = spriteFacesRight ? !movingRight : movingRight;
    }

    void FixedUpdate()
    {
        if (target == null)
            return;

        float deltaX = target.position.x - rb.position.x;
        float desiredVx = Mathf.Abs(deltaX) > stopDistance
            ? Mathf.Sign(deltaX) * moveSpeed
            : 0f;

        Vector2 vel = rb.linearVelocity;
        vel.x = Mathf.MoveTowards(vel.x, desiredVx, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = vel;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0f)
            Destroy(gameObject);
    }
}
