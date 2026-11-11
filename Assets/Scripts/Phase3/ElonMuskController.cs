using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class ElonMuskController : MonoBehaviour, IDamageable
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

    [Header("Rocket Mode — Aerial Patrol (Stage 2)")]
    [SerializeField] float rocketHoverHeight = 4.5f;
    [SerializeField, Min(0.1f)] float rocketFlySpeed = 6f;
    [SerializeField, Min(0.1f)] float rocketAcceleration = 10f;
    [SerializeField] float rocketMinArenaX = -15f;
    [SerializeField] float rocketMaxArenaX = 26f;
    [SerializeField] float rocketMinHoverY = 8.5f;
    [SerializeField] float rocketMaxHoverY = 12.5f;
    [SerializeField, Min(0f)] float rocketMinPlayerDistanceX = 6f;
    [SerializeField, Min(0.4f)] float rocketWaypointMinInterval = 1.6f;
    [SerializeField, Min(0.4f)] float rocketWaypointMaxInterval = 3.2f;
    [SerializeField, Min(0f)] float rocketBobAmplitude = 0.7f;
    [SerializeField, Min(0.1f)] float rocketBobFrequency = 1.8f;
    [SerializeField, Min(0f)] float rocketMaxTiltAngle = 8f;

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

    [Header("Rocket Mode — Collider & Attacks")]
    [SerializeField] Vector3 rocketVisualScale = new Vector3(1.85f, 1.85f, 1f);
    [SerializeField] Vector2 rocketColliderSize = new Vector2(2.8f, 6.2f);
    [SerializeField] Vector2 rocketColliderOffset = Vector2.zero;
    [SerializeField] string bossBoundaryLayerName = "BossBoundary";
    [SerializeField] ElonRocketProjectile rocketProjectilePrefab;
    [SerializeField, Min(1f)] float rocketProjectileSpeed = 14f;
    [SerializeField, Min(1)] int missileCountPerShower = 9;
    [SerializeField, Min(0.02f)] float missileStepDelay = 0.16f;
    [SerializeField] float missileSpawnY = 14f;
    [SerializeField] float missileStartX = -15.5f;
    [SerializeField] float missileEndX = 27f;
    [SerializeField] Vector3 fallbackMissileScale = new Vector3(1.35f, 0.45f, 1f);
    [SerializeField] ElonHunterDrone hunterDronePrefab;
    [SerializeField, Min(1)] int dronesPerWave = 1;
    [SerializeField, Min(1)] int maxActiveDrones = 3;
    [SerializeField] Vector2 droneSpawnOffset = new Vector2(2.2f, -0.5f);
    [SerializeField, Min(0.5f)] float droneLaserSpeed = 5.5f;
    [SerializeField, Min(0.1f)] float droneLaserFireInterval = 2f;
    [SerializeField] bool droneLasersBlockedByPlatforms;

    [Header("Rocket Mode — Camera Zoom")]
    [SerializeField] Phase1CameraFollow cameraFollow;
    [SerializeField, Min(1f)] float rocketCameraSize = 13.5f;
    [SerializeField, Min(0f)] float cameraZoomDuration = 1.2f;

    [Header("Visual & Animator References")]
    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Transform visualTransform;

    Rigidbody2D body;
    BoxCollider2D bodyCollider;
    float arenaBaseY;
    bool facingRight;
    bool isBrakingOrStopped = true;

    float attackTimer;
    float spawnTimer;
    float firePointAbsX;
    readonly List<GameObject> activeMinions = new List<GameObject>();
    readonly List<GameObject> activeDrones = new List<GameObject>();
    static Sprite cachedFallbackSprite;
    static Sprite cachedGlowSprite;
    Coroutine missileShowerCoroutine;
    bool isMissileShowerActive;
    BossStage activeAppliedStage;
    Vector2 carColliderSize;
    Vector2 carColliderOffset;
    Vector3 baseVisualScale = Vector3.one;
    int bossBoundaryLayer = -1;
    Vector2 rocketWaypoint;
    float rocketWaypointTimer;
    float rocketBobPhase;
    Color baseSpriteColor = Color.white;
    float hitFlashTimer;

    static readonly int BossStageHash = Animator.StringToHash("BossStage");
    static readonly int IsAcceleratingHash = Animator.StringToHash("IsAccelerating");
    static readonly int AtTopSpeedHash = Animator.StringToHash("AtTopSpeed");
    static readonly int TransformTriggerHash = Animator.StringToHash("TransformStage");

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.linearDamping = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
            baseSpriteColor = spriteRenderer.color;
        if (visualTransform == null && animator != null)
            visualTransform = animator.transform;
        if (visualTransform != null)
            baseVisualScale = visualTransform.localScale;

        carColliderSize = bodyCollider.size;
        carColliderOffset = bodyCollider.offset;
        bossBoundaryLayer = LayerMask.NameToLayer(bossBoundaryLayerName);

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

        if (cameraFollow == null)
            cameraFollow = FindAnyObjectByType<Phase1CameraFollow>();

        if (currentStage == BossStage.HumanIntro)
            PlayIntroCutscene();

        ApplyStageTransition(currentStage);
    }

    void Update()
    {

        if (currentStage != activeAppliedStage)
            ApplyStageTransition(currentStage);

        CheckStageTransitionsByHealth();
        if (hitFlashTimer > 0f)
        {
            hitFlashTimer -= Time.deltaTime;
            if (hitFlashTimer <= 0f && spriteRenderer != null)
                spriteRenderer.color = baseSpriteColor;
        }

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
            // Near Player => Brake to idle
            isBrakingOrStopped = true;
            SetHorizontalVelocity(Mathf.MoveTowards(body.linearVelocity.x, 0f, carDeceleration * dt));
        }
        else if (distanceToStop <= brakingDistance)
        {   
            // Arriving to player => Decelerate
            isBrakingOrStopped = true;
            SetHorizontalVelocity(Mathf.MoveTowards(body.linearVelocity.x, dir * 0.5f, carDeceleration * dt));
        }
        else
        {
            // Accelerating towards player => Do Accelerating -> Max Speed transition
            isBrakingOrStopped = false;
            SetHorizontalVelocity(Mathf.MoveTowards(body.linearVelocity.x, dir * carMaxSpeed, carAcceleration * dt));
        }
    }

    void FixedUpdateRocketMode(float dt)
    {
        float minHoverY = GetRocketMinHoverY();
        rocketWaypointTimer -= dt;

        if (rocketWaypointTimer <= 0f || ((Vector2)rocketWaypoint - body.position).sqrMagnitude <= 1.25f)
            PickNextRocketWaypoint();

        float bobY = Mathf.Sin(Time.time * rocketBobFrequency + rocketBobPhase) * rocketBobAmplitude;
        float desiredY = Mathf.Max(minHoverY, rocketWaypoint.y + bobY);
        Vector2 targetPos = new Vector2(rocketWaypoint.x, desiredY);
        Vector2 toTarget = targetPos - body.position;

        Vector2 desiredVelocity = toTarget.magnitude > 0.25f
            ? toTarget.normalized * rocketFlySpeed
            : Vector2.zero;


        if (body.position.y < minHoverY && desiredVelocity.y < rocketFlySpeed * 0.5f)
            desiredVelocity.y = rocketFlySpeed * 0.65f;

        body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, desiredVelocity, rocketAcceleration * dt);
    }

    float GetRocketMinHoverY()
    {
        return Mathf.Max(rocketMinHoverY, arenaBaseY + rocketHoverHeight);
    }

    void PickNextRocketWaypoint(bool preferFarFromPlayer = false)
    {
        float minX = Mathf.Min(rocketMinArenaX, rocketMaxArenaX);
        float maxX = Mathf.Max(rocketMinArenaX, rocketMaxArenaX);
        float minY = GetRocketMinHoverY();
        float maxY = Mathf.Max(minY, rocketMaxHoverY);

        float nextX = Random.Range(minX, maxX);
        if (player != null && rocketMinPlayerDistanceX > 0f)
        {
            // Keep elon Hovering across arena in Rocket mode instead of chasing glorb
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float candidateX = Random.Range(minX, maxX);
                float distToPlayer = Mathf.Abs(candidateX - player.position.x);
                float distToCurrent = Mathf.Abs(candidateX - rocketWaypoint.x);
                bool farEnoughFromPlayer = distToPlayer >= rocketMinPlayerDistanceX;
                bool shiftsPosition = preferFarFromPlayer || distToCurrent >= 5f;

                if (farEnoughFromPlayer && shiftsPosition)
                {
                    nextX = candidateX;
                    break;
                }
            }

            if (Mathf.Abs(nextX - player.position.x) < rocketMinPlayerDistanceX)
            {
                float arenaMid = (minX + maxX) * 0.5f;
                float awayDir = player.position.x >= arenaMid ? -1f : 1f;
                nextX = Mathf.Clamp(player.position.x + awayDir * rocketMinPlayerDistanceX, minX, maxX);
            }
        }

        float nextY = Random.Range(minY, maxY);
        rocketWaypoint = new Vector2(nextX, nextY);

        float minWait = Mathf.Min(rocketWaypointMinInterval, rocketWaypointMaxInterval);
        float maxWait = Mathf.Max(rocketWaypointMinInterval, rocketWaypointMaxInterval);
        rocketWaypointTimer = Random.Range(minWait, maxWait);
    }

    void SetHorizontalVelocity(float vx)
    {
        Vector2 velocity = body.linearVelocity;
        velocity.x = vx;
        body.linearVelocity = velocity;
    }

    // Flipping face based on orientation (Same script as Phase1Controller)

    void UpdateFacing()
    {
        float vx = body.linearVelocity.x;

        if (Mathf.Abs(vx) > 0.05f)
            facingRight = vx > 0f;
        else if (player != null)
            facingRight = player.position.x > transform.position.x;

        if (spriteRenderer != null)
            spriteRenderer.flipX = facingRight;

        if (firePoint != null)
        {
            Vector3 pos = firePoint.localPosition;
            pos.x = facingRight ? firePointAbsX : -firePointAbsX;
            firePoint.localPosition = pos;
        }
    }

    // Updating health, and checking for boss transition
    public void TakeDamage(float damage)
    {
        if (currentStage == BossStage.HumanIntro || currentStage == BossStage.Defeated)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.35f, 0.35f, baseSpriteColor.a);
            hitFlashTimer = 0.08f;
        }

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
        if (currentStage == newStage && activeAppliedStage == newStage)
            return;

        currentStage = newStage;
        ApplyStageTransition(currentStage);
    }

    void ApplyStageTransition(BossStage targetStage)
    {
        activeAppliedStage = targetStage;
        attackTimer = 0f;
        spawnTimer = 0f;
        ApplyStagePhysics(targetStage);

        if (animator != null)
        {
            animator.SetInteger(BossStageHash, (int)targetStage);
            animator.SetTrigger(TransformTriggerHash);
        }

        if (targetStage != BossStage.CarMode && clearMinionsOnStageChange)
            ClearGameObjectList(activeMinions);

        bool isRocket = targetStage == BossStage.RocketMode;
        UpdateCollisionsBasedOnBossState(isRocket);

        if (isRocket)
            TransformToRocket();
        else if (targetStage == BossStage.CarMode || targetStage == BossStage.HumanIntro)
            RestoreCarBoundsAndScale();

        if (cameraFollow != null)
        {
            if (isRocket)
                cameraFollow.SetOrthographicSize(rocketCameraSize, cameraZoomDuration);
            else if (targetStage != BossStage.Defeated)
                cameraFollow.ResetOrthographicSize(cameraZoomDuration);
        }

        if (targetStage == BossStage.Defeated)
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

    
    // Updating animation based on boss state
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

            RotateVisualTowards(targetZ);
        }
    }

    void UpdateRocketAnimation()
    {
        float speed = body.linearVelocity.magnitude;
        animator.SetBool(IsAcceleratingHash, speed > 0.1f);
        animator.SetBool(AtTopSpeedHash, speed >= rocketFlySpeed * 0.85f);

        float targetZ = Mathf.Clamp(-body.linearVelocity.x * 1.25f, -rocketMaxTiltAngle, rocketMaxTiltAngle);
        RotateVisualTowards(targetZ);
    }

    void RotateVisualTowards(float targetZ)
    {
        if (visualTransform == null)
            return;

        visualTransform.localRotation = Quaternion.RotateTowards(
            visualTransform.localRotation,
            Quaternion.Euler(0f, 0f, targetZ),
            rotationSmoothSpeed * Time.deltaTime);
    }

    void ResetVisualRotation()
    {
        if (visualTransform != null)
            visualTransform.localRotation = Quaternion.identity;
    }

    // Update boss combat timer (Spawn mechanics)
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
            if (!isMissileShowerActive)
            {
                attackTimer += dt;
                if (attackTimer >= rocketShootCooldown)
                {
                    attackTimer = 0f;
                    ShootProjectile();
                }
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
        ResetVisualRotation();
        rocketBobPhase = Random.Range(0f, Mathf.PI * 2f);
        PickNextRocketWaypoint(preferFarFromPlayer: true);
        if (visualTransform != null)
            visualTransform.localScale = rocketVisualScale;

        if (bodyCollider != null)
        {
            bodyCollider.size = rocketColliderSize;
            bodyCollider.offset = rocketColliderOffset;
        }
    }

    void RestoreCarBoundsAndScale()
    {
        if (visualTransform != null)
            visualTransform.localScale = baseVisualScale;

        if (bodyCollider != null && carColliderSize.sqrMagnitude > 0f)
        {
            bodyCollider.size = carColliderSize;
            bodyCollider.offset = carColliderOffset;
        }
    }

    void UpdateCollisionsBasedOnBossState(bool ignore)
    {
        if (bodyCollider == null)
            return;

        Collider2D[] allColliders = FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider2D col = allColliders[i];
            if (col == null || col == bodyCollider || col.isTrigger)
                continue;

            bool isCarBoundary = bossBoundaryLayer != -1 && col.gameObject.layer == bossBoundaryLayer;
            bool isOneWayPlatform = col.GetComponent<PlatformEffector2D>() != null;

            if (isCarBoundary || isOneWayPlatform)
                Physics2D.IgnoreCollision(bodyCollider, col, ignore);
        }
    }

    void ShootProjectile()
    {
        if (player == null)
            return;

        StopMissileShower();
        missileShowerCoroutine = StartCoroutine(MissileShowerRoutine());
    }

    IEnumerator MissileShowerRoutine()
    {
        isMissileShowerActive = true;

        int count = Mathf.Max(1, missileCountPerShower);
        float minX = Mathf.Min(missileStartX, missileEndX);
        float maxX = Mathf.Max(missileStartX, missileEndX);

        for (int i = 0; i < count; i++)
        {
            if (currentStage != BossStage.RocketMode)
                break;

            float t = count == 1 ? 0.5f : (float)i / (count - 1);
            float spawnX = Mathf.Lerp(minX, maxX, t);
            SpawnVerticalMissile(new Vector2(spawnX, missileSpawnY));

            if (i < count - 1 && missileStepDelay > 0f)
                yield return new WaitForSeconds(missileStepDelay);
        }

        missileShowerCoroutine = null;
        isMissileShowerActive = false;
    }

    void SpawnVerticalMissile(Vector2 spawnPos)
    {
        int order = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 3;
        ElonRocketProjectile shot = rocketProjectilePrefab != null
            ? Instantiate(rocketProjectilePrefab, spawnPos, Quaternion.identity)
            : CreateFallbackObject<ElonRocketProjectile>(
                "ElonVerticalMissile",
                spawnPos,
                fallbackMissileScale,
                new Color(1f, 0.35f, 0.1f, 1f),
                0.45f,
                order);

        // One way platforms disallow any bombs to pass through them (acts as shelter)
        shot.Launch(Vector2.down, rocketProjectileSpeed, overrideBlockedByPlatforms: true);
    }

    void StopMissileShower()
    {
        if (missileShowerCoroutine != null)
        {
            StopCoroutine(missileShowerCoroutine);
            missileShowerCoroutine = null;
        }
        isMissileShowerActive = false;
    }

    void SendHunterDrones()
    {
        activeDrones.RemoveAll(d => d == null);
        int openSlots = maxActiveDrones - activeDrones.Count;
        if (openSlots <= 0)
            return;

        int order = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 3;
        int spawnCount = Mathf.Min(dronesPerWave, openSlots);
        for (int i = 0; i < spawnCount; i++)
        {
            float side = (activeDrones.Count % 2 == 0) ? -1f : 1f;
            Vector3 spawnPos = transform.position + new Vector3(side * droneSpawnOffset.x, droneSpawnOffset.y, 0f);

            ElonHunterDrone drone = hunterDronePrefab != null
                ? Instantiate(hunterDronePrefab, spawnPos, Quaternion.identity)
                : CreateFallbackObject<ElonHunterDrone>(
                    "ElonHunterDrone",
                    spawnPos,
                    new Vector3(0.9f, 0.9f, 1f),
                    new Color(0.85f, 0.25f, 1f, 1f),
                    0.5f,
                    order);

            drone.ConfigureCombat(droneLaserSpeed, droneLaserFireInterval, droneLasersBlockedByPlatforms, side);
            activeDrones.Add(drone.gameObject);
        }
    }

    void PlayDefeatCutscene()
    {
        StopMissileShower();
        ClearGameObjectList(activeDrones);
        // TODO (0 HP): Face falls off rocket cutscene -> enable player escape on rocket to his home world : GAME ENDS (Ending cutscene maybe?)
    }

    void ClearGameObjectList(List<GameObject> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                Destroy(list[i]);
        }
        list.Clear();
    }

    public static T CreateFallbackObject<T>(
        string objName,
        Vector3 spawnPos,
        Vector3 scale,
        Color color,
        float colliderRadius,
        int sortingOrder = 3)
        where T : Component
    {
        GameObject go = new GameObject(objName);
        go.transform.position = spawnPos;
        go.transform.localScale = scale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetFallbackSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = colliderRadius;
        col.isTrigger = true;

        return go.AddComponent<T>();
    }

    public static Sprite GetSoftGlowSprite()
    {
        if (cachedGlowSprite != null)
            return cachedGlowSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxRadius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist01 = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), center) / maxRadius);
                float alpha = Mathf.Pow(1f - dist01, 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        cachedGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedGlowSprite;
    }

    static Sprite GetFallbackSprite()
    {
        if (cachedFallbackSprite != null)
            return cachedFallbackSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        cachedFallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return cachedFallbackSprite;
    }
}