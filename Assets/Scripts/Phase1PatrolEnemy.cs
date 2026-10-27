using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class Phase1PatrolEnemy : MonoBehaviour
{
    [Header("Patrol — From Original Position")]
    [SerializeField, Min(0f)] float patrolLeft = 1f;
    [SerializeField, Min(0f)] float patrolRight = 1f;
    [SerializeField, Min(0f)] float patrolSpeed = 1f;
    [SerializeField, Min(0f)] float patrolPauseDuration = 0.5f;

    float patrolPauseTimer;
    bool pausingAtEndpoint;

    [Header("Chase")]
    [SerializeField, Min(0f)] float detectionRadius = 2f;
    [SerializeField, Min(0f)] float chaseSpeed = 2f;

    [Tooltip("Maximum horizontal distance from the original position.")]
    [SerializeField, Min(0.01f)] float chaseDistance = 4f;

    [SerializeField, Min(0f)] float stopDistance = 0.05f;

    [Header("Respawn")]
    [SerializeField, Min(0f)] float respawnDelay = 1f;

    [Tooltip("Prevents immediately chasing again after respawning.")]
    [SerializeField, Min(0f)] float detectionGraceTime = 0.5f;

    enum State
    {
        Patrol,
        Chase,
        Hidden
    }

    Rigidbody2D body;
    SpriteRenderer[] visuals;
    Phase1Controller player;

    Vector2 originalPosition;
    State state;
    bool movingRight = true;
    float respawnTimer;
    float graceTimer;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        visuals = GetComponentsInChildren<SpriteRenderer>();

        originalPosition = body.position;

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 1f;
        body.linearDamping = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        GetComponent<BoxCollider2D>().isTrigger = false;
        graceTimer = detectionGraceTime;
    }

    void Start()
    {
        player = FindAnyObjectByType<Phase1Controller>();
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (state == State.Hidden)
        {
            respawnTimer -= dt;

            if (respawnTimer <= 0f)
                Respawn();

            return;
        }

        graceTimer = Mathf.Max(0f, graceTimer - dt);

        if (state == State.Patrol)
        {
            if (player != null && graceTimer <= 0f &&
                Vector2.Distance(body.position, player.transform.position)
                <= detectionRadius)
            {
                state = State.Chase;
            }
        }

        if (state == State.Chase)
            Chase(dt);
        else
            Patrol(dt);
    }

    void Patrol(float dt)
    {
        float leftEdge = originalPosition.x - patrolLeft;
        float rightEdge = originalPosition.x + patrolRight;

        const float tolerance = 0.02f;

        if (rightEdge - leftEdge <= tolerance)
        {
            SetHorizontalSpeed(0f);
            return;
        }

        if (pausingAtEndpoint)
        {
            SetHorizontalSpeed(0f);
            patrolPauseTimer -= dt;

            if (patrolPauseTimer > 0f)
                return;

            pausingAtEndpoint = false;
            movingRight = !movingRight;
        }

        bool reachedEndpoint = movingRight
            ? body.position.x >= rightEdge - tolerance
            : body.position.x <= leftEdge + tolerance;

        if (reachedEndpoint)
        {
            if (patrolPauseDuration > 0f)
            {
                pausingAtEndpoint = true;
                patrolPauseTimer = patrolPauseDuration;
                SetHorizontalSpeed(0f);
                return;
            }

            movingRight = !movingRight;
        }

        float destination = movingRight ? rightEdge : leftEdge;
        MoveHorizontally(destination, patrolSpeed, dt);
    }

    void Chase(float dt)
    {
        if (player == null)
        {
            Despawn();
            return;
        }

        float leftLimit = originalPosition.x - chaseDistance;
        float rightLimit = originalPosition.x + chaseDistance;

        const float boundaryTolerance = 0.02f;

        if (body.position.x <= leftLimit + boundaryTolerance ||
            body.position.x >= rightLimit - boundaryTolerance)
        {
            Despawn();
            return;
        }

        float playerX = player.transform.position.x;

        float destination = Mathf.Clamp(
            playerX,
            leftLimit,
            rightLimit
        );

        bool playerInsideLimits =
            playerX > leftLimit && playerX < rightLimit;

        // Only stop near the player when they are inside the chase area.
        // Otherwise, continue to the boundary so despawning can occur.
        if (playerInsideLimits &&
            Mathf.Abs(playerX - body.position.x) <= stopDistance)
        {
            SetHorizontalSpeed(0f);
            return;
        }

        MoveHorizontally(destination, chaseSpeed, dt);
    }

    void MoveHorizontally(float destination, float speed, float dt)
    {
        float nextX = Mathf.MoveTowards(
            body.position.x,
            destination,
            speed * dt
        );

        SetHorizontalSpeed((nextX - body.position.x) / dt);
    }

    void SetHorizontalSpeed(float speed)
    {
        Vector2 velocity = body.linearVelocity;
        velocity.x = speed;
        body.linearVelocity = velocity;
    }

    void Despawn()
    {
        state = State.Hidden;
        respawnTimer = respawnDelay;

        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        SetVisible(false);
    }

    void Respawn()
    {
        body.simulated = false;

        body.position = originalPosition;
        transform.position = new Vector3(
            originalPosition.x,
            originalPosition.y,
            transform.position.z
        );

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        movingRight = true;
        pausingAtEndpoint = false;
        patrolPauseTimer = 0f;
        graceTimer = detectionGraceTime;
        state = State.Patrol;

        body.simulated = true;
        SetVisible(true);
    }

    void SetVisible(bool visible)
    {
        foreach (SpriteRenderer visual in visuals)
            visual.enabled = visible;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying
            ? new Vector3(
                originalPosition.x,
                originalPosition.y,
                transform.position.z
            )
            : transform.position;

        //green: patrol endpoints.
        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            origin + Vector3.left * patrolLeft,
            origin + Vector3.right * patrolRight
        );

        //red: maximum horizontal chase limits
        Gizmos.color = Color.red;
        Vector3 left = origin + Vector3.left * chaseDistance;
        Vector3 right = origin + Vector3.right * chaseDistance;

        Gizmos.DrawLine(left, right);
        Gizmos.DrawLine(left + Vector3.down * 0.2f,
                        left + Vector3.up * 0.2f);
        Gizmos.DrawLine(right + Vector3.down * 0.2f,
                        right + Vector3.up * 0.2f);

        // Yellow: detection radius follows the enemy
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}