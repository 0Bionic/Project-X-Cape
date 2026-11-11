using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class ElonHunterDrone : MonoBehaviour, IDamageable
{
    [Header("Tracking and Hovering Mechanic")]
    [SerializeField, Min(0.1f)] float flySpeed = 6.5f;
    [SerializeField, Min(0.1f)] float steeringAcceleration = 14f;
    [SerializeField] float hoverOffsetY = 3.2f;
    [SerializeField, Min(0f)] float preferredHorizontalGap = 4.5f;
    [SerializeField, Min(0f)] float bobAmplitude = 0.35f;
    [SerializeField, Min(0.1f)] float bobFrequency = 2.4f;

    [Header("Laser Attack Mehanic")]
    [SerializeField] ElonRocketProjectile laserPrefab;
    [SerializeField, Min(0.5f)] float laserBulletSpeed = 5.5f;
    [SerializeField, Min(0.1f)] float laserFireInterval = 2f;
    [SerializeField] bool lasersBlockedByPlatforms;
    [SerializeField] Vector3 fallbackLaserScale = new Vector3(0.8f, 0.2f, 1f);
    [SerializeField] Color laserCoreColor = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField] Color laserGlowColor = new Color(1f, 0.05f, 0.05f, 0.42f);

    [Header("Health and Lifetime stats")]
    [SerializeField, Min(1f)] float maxHealth = 15f;
    [SerializeField, Min(0f)] float maxLifetime = 20f;

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    Transform target;
    float currentHealth;
    Color baseColor = Color.white;
    float hitFlashTimer;
    float preferredSide = 1f;
    bool hasConfiguredSide;
    float fireTimer;
    float bobPhase;

    public void ConfigureCombat(float bulletSpeed, float fireInterval, bool blockedByPlatforms, float sideOffset = 0f)
    {
        laserBulletSpeed = Mathf.Max(0.5f, bulletSpeed);
        laserFireInterval = Mathf.Max(0.1f, fireInterval);
        lasersBlockedByPlatforms = blockedByPlatforms;
        fireTimer = laserFireInterval * 0.5f;

        if (!Mathf.Approximately(sideOffset, 0f))
        {
            preferredSide = Mathf.Sign(sideOffset);
            hasConfiguredSide = true;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        col.isTrigger = true; // Lets drones fly smoothly over platforms toward Glorb

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            baseColor = spriteRenderer.color;

        currentHealth = maxHealth;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        fireTimer = laserFireInterval * 0.5f;
        if (maxLifetime > 0f)
            Destroy(gameObject, maxLifetime);
    }

    void Start()
    {
        Phase1Controller player = FindAnyObjectByType<Phase1Controller>();
        if (player != null)
        {
            target = player.transform;
            if (!hasConfiguredSide)
                preferredSide = transform.position.x < target.position.x ? -1f : 1f;
        }
    }

    void Update()
    {
        if (spriteRenderer != null && hitFlashTimer > 0f)
        {
            hitFlashTimer -= Time.deltaTime;
            if (hitFlashTimer <= 0f)
                spriteRenderer.color = baseColor;
        }

        if (target == null)
            return;

        if (spriteRenderer != null)
            spriteRenderer.flipX = target.position.x < transform.position.x;

        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            fireTimer = laserFireInterval;
            FireLaser();
        }
    }

    void FixedUpdate()
    {
        if (target == null)
            return;

        float hoverY = target.position.y + hoverOffsetY + Mathf.Sin(Time.time * bobFrequency + bobPhase) * bobAmplitude;
        Vector2 hoverPoint = new Vector2(target.position.x + preferredSide * preferredHorizontalGap, hoverY);
        Vector2 toHoverPoint = hoverPoint - rb.position;
        float distance = toHoverPoint.magnitude;

        Vector2 desiredVelocity = distance > 0.25f
            ? toHoverPoint.normalized * Mathf.Min(flySpeed, distance * 3f)
            : Vector2.zero;

        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, desiredVelocity, steeringAcceleration * Time.fixedDeltaTime);
    }

    void FireLaser()
    {
        Vector2 spawnPos = transform.position;
        Vector2 dir = ((Vector2)target.position - spawnPos).normalized;
        int order = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 4;

        ElonRocketProjectile laser;
        if (laserPrefab != null)
        {
            laser = Instantiate(laserPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            laser = ElonMuskController.CreateFallbackObject<ElonRocketProjectile>(
                "ElonDroneLaser",
                spawnPos,
                fallbackLaserScale,
                laserCoreColor,
                0.35f,
                order);

            GameObject glow = new GameObject("LaserGlow");
            glow.transform.SetParent(laser.transform, false);
            glow.transform.localScale = new Vector3(2f, 3.2f, 1f);

            SpriteRenderer glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = ElonMuskController.GetSoftGlowSprite();
            glowRenderer.color = laserGlowColor;
            glowRenderer.sortingOrder = order - 1;
        }

        laser.Launch(dir, laserBulletSpeed, lasersBlockedByPlatforms);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Phase1Controller player = other.GetComponentInParent<Phase1Controller>();
        if (player != null)
            player.Hit();
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.35f, 0.35f, baseColor.a);
            hitFlashTimer = 0.08f;
        }
    }
}
