using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class Phase1PatrolEnemy : MonoBehaviour
{
    [Header("Patrol - From Original Position")]
    [SerializeField, Min(0f)] float patrolLeft = 1f;
    [SerializeField, Min(0f)] float patrolRight = 1f;
    [SerializeField, Min(0f)] float patrolSpeed = 1f;
    [SerializeField, Min(0f)] float patrolPauseDuration = 0.5f;

    [Header("Detection - World Units From Enemy")]
    [SerializeField, Min(0f)] float detectionLeft = 2f;
    [SerializeField, Min(0f)] float detectionRight = 2f;
    [SerializeField, Min(0f)] float detectionUp = 1f;
    [SerializeField, Min(0f)] float detectionDown = 1f;

    [Header("Chase")]
    [SerializeField, Min(0f)] float chaseSpeed = 2f;

    [Tooltip("Maximum horizontal distance from the original position.")]
    [SerializeField, Min(0.01f)] float chaseDistance = 4f;

    [SerializeField, Min(0f)] float stopDistance = 0.05f;

    [Header("Return Home")]
    [SerializeField, Min(0.01f)] float returnSpeed = 1.5f;

    [Tooltip("Detection cooldown after reaching home and at startup.")]
    [SerializeField, Min(0f)] float detectionGraceTime = 0.5f;

    enum State
    {
        Patrol,
        Chase,
        Returning
    }

    const float ArrivalTolerance = 0.02f;

    Rigidbody2D body;
    Phase1Controller player;

    Vector2 originalPosition;
    State state;

    bool movingRight = true;
    bool pausingAtEndpoint;

    float patrolPauseTimer;
    float graceTimer;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
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
        graceTimer = Mathf.Max(0f, graceTimer - dt);

        if (state == State.Patrol &&
            graceTimer <= 0f &&
            PlayerInsideDetection())
        {
            pausingAtEndpoint = false;
            patrolPauseTimer = 0f;
            state = State.Chase;
        }

        switch (state)
        {
            case State.Patrol:
                Patrol(dt);
                break;

            case State.Chase:
                Chase(dt);
                break;

            case State.Returning:
                ReturnHome(dt);
                break;
        }
    }

    bool PlayerInsideDetection()
    {
        if (player == null)
            return false;

        Vector2 offset =
            (Vector2)player.transform.position - body.position;

        return offset.x >= -detectionLeft &&
               offset.x <= detectionRight &&
               offset.y >= -detectionDown &&
               offset.y <= detectionUp;
    }

    void Patrol(float dt)
    {
        float leftEdge = originalPosition.x - patrolLeft;
        float rightEdge = originalPosition.x + patrolRight;

        if (rightEdge - leftEdge <= ArrivalTolerance)
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
            ? body.position.x >= rightEdge - ArrivalTolerance
            : body.position.x <= leftEdge + ArrivalTolerance;

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
        float leftLimit = originalPosition.x - chaseDistance;
        float rightLimit = originalPosition.x + chaseDistance;

        if (player == null ||
            body.position.x <= leftLimit + ArrivalTolerance ||
            body.position.x >= rightLimit - ArrivalTolerance)
        {
            BeginReturning();
            return;
        }

        float playerX = player.transform.position.x;

        bool playerInsideLimits =
            playerX > leftLimit && playerX < rightLimit;

        if (playerInsideLimits &&
            Mathf.Abs(playerX - body.position.x) <= stopDistance)
        {
            SetHorizontalSpeed(0f);
            return;
        }

        float destination = Mathf.Clamp(
            playerX,
            leftLimit,
            rightLimit
        );

        MoveHorizontally(destination, chaseSpeed, dt);
    }

    void BeginReturning()
    {
        state = State.Returning;
        pausingAtEndpoint = false;
        patrolPauseTimer = 0f;
        SetHorizontalSpeed(0f);
    }

    void ReturnHome(float dt)
    {
        if (Mathf.Abs(body.position.x - originalPosition.x)
            <= ArrivalTolerance)
        {
            SetHorizontalSpeed(0f);

            movingRight = true;
            graceTimer = detectionGraceTime;
            state = State.Patrol;
            return;
        }

        MoveHorizontally(originalPosition.x, returnSpeed, dt);
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

    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying
            ? new Vector3(
                originalPosition.x,
                originalPosition.y,
                transform.position.z
            )
            : transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            origin + Vector3.left * patrolLeft,
            origin + Vector3.right * patrolRight
        );

        Gizmos.color = Color.red;

        Vector3 left = origin + Vector3.left * chaseDistance;
        Vector3 right = origin + Vector3.right * chaseDistance;

        Gizmos.DrawLine(left, right);
        Gizmos.DrawLine(
            left + Vector3.down * 0.2f,
            left + Vector3.up * 0.2f
        );
        Gizmos.DrawLine(
            right + Vector3.down * 0.2f,
            right + Vector3.up * 0.2f
        );

        Vector3 detectionOrigin = Application.isPlaying && body != null
            ? new Vector3(body.position.x, body.position.y, transform.position.z)
            : transform.position;

        Vector3 detectionCenter = detectionOrigin + new Vector3(
            (detectionRight - detectionLeft) * 0.5f,
            (detectionUp - detectionDown) * 0.5f,
            0f
        );

        Vector3 detectionSize = new Vector3(
            detectionLeft + detectionRight,
            detectionUp + detectionDown,
            0f
        );

        Gizmos.color = new Color(1f, 1f, 0f, 0.12f);
        Gizmos.DrawCube(detectionCenter, detectionSize);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(detectionCenter, detectionSize);
    }
}