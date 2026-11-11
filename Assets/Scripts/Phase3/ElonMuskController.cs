using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class ElonMuskController : MonoBehaviour
{
    public enum BossStage
    {
        HumanIntro = 0, // Post-M2: Normal body intro cutscene -> transforms into CarMode
        CarMode = 1,    // Current M2: Chasing/zooming toward player, throwing rocks, spawning minions
        RocketMode = 2, // Final Stage (after 40/100 dmg, <= 60 HP): Flying rocket, projectiles, hunter drones
        Defeated = 3    // 0 HP: Face falls off rocket cutscene -> player flies away on rocket
    }

    [Header("Boss Stage & Health")]
    [SerializeField] BossStage currentStage = BossStage.CarMode;
    [SerializeField, Min(1f)] float maxHealth = 100f;
    [SerializeField] float currentHealth = 100f;
    [Tooltip("Damage taken in Car Mode before transforming into Rocket Mode (40 dmg = triggers at 60 HP).")]
    [SerializeField, Min(1f)] float carToRocketDamageThreshold = 40f;

    [Header("Target")]
    [SerializeField] Transform player;

    [Header("Car Mode — Player Chase & Animation")]
    [SerializeField, Min(0.1f)] float carMaxSpeed = 8f;
    [SerializeField, Min(0.1f)] float carAcceleration = 10f;
    [SerializeField, Min(0.1f)] float carDeceleration = 14f;
    [SerializeField, Min(0f)] float stopDistance = 2.5f;
    [SerializeField, Range(0.5f, 1f)] float carTopSpeedRatio = 0.85f;
    [SerializeField] float carGravityScale = 1f;
    [Tooltip("Tilt angle when car hits top speed flame (LEFT_Stage_4_1 / 4_2).")]
    [SerializeField] float carTopSpeedTiltAngle = -8f;
    [SerializeField] float rotationSmoothSpeed = 180f;

    [Header("Rocket Mode — Aerial Tracking (Stage 2)")]
    [SerializeField] float rocketHoverHeight = 4.5f;
    [SerializeField, Min(0.1f)] float rocketFlySpeed = 6f;
    [SerializeField, Min(0.1f)] float rocketAcceleration = 10f;

    [Header("Combat & Spawning Hooks (For Future Implementation)")]
    [SerializeField] Transform firePoint;
    [SerializeField, Min(0.1f)] float rockThrowCooldown = 2.5f;
    [SerializeField, Min(0.1f)] float minionSpawnCooldown = 10f;
    [SerializeField, Min(0.1f)] float rocketShootCooldown = 1.5f;
    [SerializeField, Min(0.1f)] float droneSendCooldown = 5f;

    [Header("Car Mode — Rock Throw")]
    [SerializeField] ElonRockProjectile rockPrefab;
    [Tooltip("X = forward distance along facing direction, Y = height above car center.")]
    [SerializeField] Vector2 rockSpawnOffset = new Vector2(0.5f, 0.85f);
    [SerializeField, Min(0.5f)] float rockArcHeight = 3.5f;
    [SerializeField] float rockSpinSpeed = 360f;

    [Header("Car Mode — Minion Waves")]
    [SerializeField] GameObject minionPrefab;
    [SerializeField, Min(1)] int minionsPerWave = 2;
    [SerializeField, Min(1)] int maxActiveMinions = 4;
    [SerializeField] Transform[] minionSpawnPoints;
    [Tooltip("Used if Minion Spawn Points is empty: spawns left/right of Elon inside these X bounds.")]
    [SerializeField] float minionSpawnOffsetX = 3.5f;
    [SerializeField] float minionSpawnHeight = 1.5f;
    [SerializeField] float minionMinArenaX = -17f;
    [SerializeField] float minionMaxArenaX = 28f;
    [SerializeField] bool clearMinionsOnStageChange = true;

    [Header("Visual & Animator References")]
    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Transform visualTransform;

    Rigidbody2D body;
    float arenaBaseY;
    bool facingRight;
    bool isBrakingOrStopped = true;

    float attackTimer;
    float spawnTimer;
    float firePointAbsX;
    readonly List<GameObject> activeMinions = new List<GameObject>();

    static readonly int BossStageHash = Animator.StringToHash("BossStage");
    static readonly int IsAcceleratingHash = Animator.StringToHash("IsAccelerating");
    static readonly int AtTopSpeedHash = Animator.StringToHash("AtTopSpeed");
    static readonly int TransformTriggerHash = Animator.StringToHash("TransformStage");

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.linearDamping = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (visualTransform == null && animator != null)
            visualTransform = animator.transform;

        if (firePoint != null)
            firePointAbsX = Mathf.Abs(firePoint.localPosition.x);

        arenaBaseY = body.position.y;
        currentHealth = maxHealth;
        ApplyStagePhysics(currentStage);
    }

    void Start()
    {
        if (player == null)
        {
            Phase1Controller p1 = FindAnyObjectByType<Phase1Controller>();
            if (p1 != null)
                player = p1.transform;
        }

        if (animator != null)
            animator.SetInteger(BossStageHash, (int)currentStage);

        if (currentStage == BossStage.HumanIntro)
            PlayIntroCutscene();
    }

    void Update()
    {
        CheckStageTransitionsByHealth();
        UpdateFacing();
        UpdateAnimation();
        UpdateCombatTimers(Time.deltaTime);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        switch (currentStage)
        {
            case BossStage.HumanIntro:
            case BossStage.Defeated:
                SetHorizontalVelocity(0f);
                break;

            case BossStage.CarMode:
                FixedUpdateCarMode(dt);
                break;

            case BossStage.RocketMode:
                FixedUpdateRocketMode(dt);
                break;
        }
    }
    
    // Player tracking

    void FixedUpdateCarMode(float dt)
    {
        if (player == null)
        {
            isBrakingOrStopped = true;
            SetHorizontalVelocity(Mathf.MoveTowards(body.linearVelocity.x, 0f, carDeceleration * dt));
            return;
        }

        float deltaX = player.position.x - body.position.x;
        float distanceToStop = Mathf.Abs(deltaX) - stopDistance;
        float dir = Mathf.Sign(deltaX);

        float currentSpeed = Mathf.Abs(body.linearVelocity.x);
        float brakingDistance = (currentSpeed * currentSpeed) / (2f * Mathf.Max(0.1f, carDeceleration));

        if (distanceToStop <= 0.05f)
        {
            // Close enough to player -> brake to idle
            isBrakingOrStopped = true;
            SetHorizontalVelocity(Mathf.MoveTowards(body.linearVelocity.x, 0f, carDeceleration * dt));
        }
        else if (distanceToStop <= brakingDistance)
        {
            // Approaching player -> decelerate (triggers ElonCar_SlowDown)
            isBrakingOrStopped = true;
            SetHorizontalVelocity(Mathf.MoveTowards(body.linearVelocity.x, dir * 0.5f, carDeceleration * dt));
        }
        else
        {
            // Accelerate toward player (triggers ElonCar_Accelerate -> ElonCar_TopSpeed)
            isBrakingOrStopped = false;
            SetHorizontalVelocity(Mathf.MoveTowards(body.linearVelocity.x, dir * carMaxSpeed, carAcceleration * dt));
        }
    }

    void FixedUpdateRocketMode(float dt)
    {
        if (player == null)
        {
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, Vector2.zero, rocketAcceleration * dt);
            return;
        }

        Vector2 targetPos = new Vector2(player.position.x, arenaBaseY + rocketHoverHeight);
        Vector2 toTarget = targetPos - body.position;

        Vector2 desiredVelocity = toTarget.magnitude > 0.25f
            ? toTarget.normalized * rocketFlySpeed
            : Vector2.zero;

        body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, desiredVelocity, rocketAcceleration * dt);
    }

    void SetHorizontalVelocity(float vx)
    {
        Vector2 velocity = body.linearVelocity;
        velocity.x = vx;
        body.linearVelocity = velocity;
    }

    // ---------------------------------------------------------
    // FACING / FLIPPING
    // ---------------------------------------------------------

    void UpdateFacing()
    {
        float vx = body.linearVelocity.x;

        if (Mathf.Abs(vx) > 0.05f)
            facingRight = vx > 0f;
        else if (player != null)
            facingRight = player.position.x > transform.position.x;

        // LEFT_Stage_0..7 sprites face LEFT by default, so flipX = true when facing RIGHT
        if (spriteRenderer != null)
            spriteRenderer.flipX = facingRight;

        if (firePoint != null)
        {
            Vector3 pos = firePoint.localPosition;
            pos.x = facingRight ? firePointAbsX : -firePointAbsX;
            firePoint.localPosition = pos;
        }
    }

    // Health and Stage Transitions
    public void TakeDamage(float damage)
    {
        if (currentStage == BossStage.HumanIntro || currentStage == BossStage.Defeated)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        CheckStageTransitionsByHealth();
    }

    void CheckStageTransitionsByHealth()
    {
        if (currentStage == BossStage.CarMode &&
            currentHealth <= maxHealth - carToRocketDamageThreshold)
        {
            SetBossStage(BossStage.RocketMode);
        }
        else if (currentStage == BossStage.RocketMode && currentHealth <= 0f)
        {
            SetBossStage(BossStage.Defeated);
        }
    }

    public void SetBossStage(BossStage newStage)
    {
        if (currentStage == newStage)
            return;

        currentStage = newStage;
        attackTimer = 0f;
        spawnTimer = 0f;
        ApplyStagePhysics(currentStage);

        if (animator != null)
        {
            animator.SetInteger(BossStageHash, (int)currentStage);
            animator.SetTrigger(TransformTriggerHash);
        }

        if (currentStage != BossStage.CarMode && clearMinionsOnStageChange)
            ClearActiveMinions();

        if (currentStage == BossStage.RocketMode)
            TransformToRocket();
        else if (currentStage == BossStage.Defeated)
            PlayDefeatCutscene();
    }

    void ApplyStagePhysics(BossStage stage)
    {
        if (body == null)
            return;

        switch (stage)
        {
            case BossStage.HumanIntro:
            case BossStage.CarMode:
                body.gravityScale = carGravityScale;
                break;

            case BossStage.RocketMode:
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;
                break;

            case BossStage.Defeated:
                body.linearVelocity = Vector2.zero;
                body.gravityScale = carGravityScale;
                break;
        }
    }

    // Animation setup

    void UpdateAnimation()
    {
        if (animator == null)
            return;

        switch (currentStage)
        {
            case BossStage.HumanIntro:
                ResetVisualRotation();
                break;

            case BossStage.CarMode:
                UpdateCarAnimation();
                break;

            case BossStage.RocketMode:
                UpdateRocketAnimation();
                break;

            case BossStage.Defeated:
                ResetVisualRotation();
                break;
        }
    }

    void UpdateCarAnimation()
    {
        float speed = Mathf.Abs(body.linearVelocity.x);
        bool isAccelerating = !isBrakingOrStopped && speed > 0.05f;
        bool atTopSpeed = isAccelerating && speed >= carMaxSpeed * carTopSpeedRatio;

        animator.SetBool(IsAcceleratingHash, isAccelerating);
        animator.SetBool(AtTopSpeedHash, atTopSpeed);

        if (visualTransform != null)
        {
            float targetZ = 0f;
            if (atTopSpeed)
            {
                float dirSign = facingRight ? -1f : 1f;
                targetZ = carTopSpeedTiltAngle * dirSign;
            }

            visualTransform.localRotation = Quaternion.RotateTowards(
                visualTransform.localRotation,
                Quaternion.Euler(0f, 0f, targetZ),
                rotationSmoothSpeed * Time.deltaTime
            );
        }
    }

    void UpdateRocketAnimation()
    {
        float speed = body.linearVelocity.magnitude;
        animator.SetBool(IsAcceleratingHash, speed > 0.1f);
        animator.SetBool(AtTopSpeedHash, speed >= rocketFlySpeed * 0.85f);
    }

    void ResetVisualRotation()
    {
        if (visualTransform != null)
            visualTransform.localRotation = Quaternion.identity;
    }

    // COMBAT & CUTSCENE HOOKS (Empty Placeholders to Implement Later)

    void UpdateCombatTimers(float dt)
    {
        if (player == null)
            return;

        if (currentStage == BossStage.CarMode)
        {
            attackTimer += dt;
            if (attackTimer >= rockThrowCooldown)
            {
                attackTimer = 0f;
                ThrowRock();
            }

            spawnTimer += dt;
            if (spawnTimer >= minionSpawnCooldown)
            {
                spawnTimer = 0f;
                SpawnMinions();
            }
        }
        else if (currentStage == BossStage.RocketMode)
        {
            attackTimer += dt;
            if (attackTimer >= rocketShootCooldown)
            {
                attackTimer = 0f;
                ShootProjectile();
            }

            spawnTimer += dt;
            if (spawnTimer >= droneSendCooldown)
            {
                spawnTimer = 0f;
                SendHunterDrones();
            }
        }
    }

    void PlayIntroCutscene()
    {
        // TODO : Normal human intro cutscene, then call SetBossStage
    }

    void ThrowRock()
    {
        if (rockPrefab == null || player == null)
            return;

        Vector2 spawnPos = firePoint != null
            ? (Vector2)firePoint.position
            : (Vector2)transform.position + new Vector2(
                facingRight ? rockSpawnOffset.x : -rockSpawnOffset.x,
                rockSpawnOffset.y);

        ElonRockProjectile rock = Instantiate(rockPrefab, spawnPos, Quaternion.identity);
        rock.Launch(spawnPos, player.position, rockArcHeight, rockSpinSpeed);
    }

    void SpawnMinions()
    {
        if (minionPrefab == null)
            return;

        activeMinions.RemoveAll(m => m == null);
        int openSlots = maxActiveMinions - activeMinions.Count;
        if (openSlots <= 0)
            return;

        int spawnCount = Mathf.Min(minionsPerWave, openSlots);

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPos;
            if (minionSpawnPoints != null && minionSpawnPoints.Length > 0)
            {
                Transform point = minionSpawnPoints[i % minionSpawnPoints.Length];
                spawnPos = point != null ? point.position : transform.position;
            }
            else
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                float spawnX = Mathf.Clamp(
                    transform.position.x + side * minionSpawnOffsetX,
                    minionMinArenaX,
                    minionMaxArenaX);
                spawnPos = new Vector3(spawnX, arenaBaseY + minionSpawnHeight, 0f);
            }

            GameObject minion = Instantiate(minionPrefab, spawnPos, Quaternion.identity);
            activeMinions.Add(minion);
        }
    }

    void TransformToRocket()
    {
        // TODO (Stage 2): Car-to-Rocket transformation VFX/SFX when Car takes 40 dmg
    }

    void ShootProjectile()
    {
        // TODO (Rocket Mode): Fire aerial projectiles down toward player
    }

    void SendHunterDrones()
    {
        // TODO (Rocket Mode): Send AI-targeting hunter drones after player
    }

    void ClearActiveMinions()
    {
        for (int i = 0; i < activeMinions.Count; i++)
        {
            if (activeMinions[i] != null)
                Destroy(activeMinions[i]);
        }
        activeMinions.Clear();
    }

    void PlayDefeatCutscene()
    {
        // TODO (0 HP): Face falls off rocket cutscene -> enable player escape on rocket
    }
}