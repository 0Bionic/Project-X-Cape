using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FallingSpike : MonoBehaviour
{
    [Header("Per-Spike Settings")]
    [SerializeField, Min(0.01f)] float fallSpeed = 3f;
    [SerializeField, Min(0.01f)] float respawnDelay = 5f;
    //[SerializeField, Min(0.01f)] float detectionRange = 1.5f;
    [SerializeField, Min(0.01f)] float maxFallDistance = 10f;
    [SerializeField, Min(0f)] float embeddedDuration = 2f;

    [Header("Detection Area — World Units")]
    [SerializeField, Min(0f)] float detectionLeft = 0.5f;
    [SerializeField, Min(0f)] float detectionRight = 0.5f;
    [SerializeField, Min(0f)] float detectionUp = 0.25f;
    [SerializeField, Min(0f)] float detectionDown = 2f;

    float embeddedTimer;

    enum State
    {
        Waiting,
        Falling,
        Embedded,
        Hidden
    }

    Rigidbody2D body;
    Collider2D spikeCollider;
    SpriteRenderer[] visuals;
    Phase1Controller player;

    Vector2 startPosition;
    float respawnTimer;
    State state = State.Waiting;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spikeCollider = GetComponent<Collider2D>();
        visuals = GetComponentsInChildren<SpriteRenderer>();

        startPosition = body.position;

        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.bodyType = RigidbodyType2D.Kinematic;

        //waiting spikes cannot block or hurt the player
        spikeCollider.enabled = false;
    }

    void Start()
    {
        player = FindAnyObjectByType<Phase1Controller>();
    }

    void FixedUpdate()
    {
        switch (state)
        {
            case State.Waiting:
                if (player == null)
                    return;

                Vector2 offset =
                    (Vector2)player.transform.position - startPosition;

                bool playerInRange =
                    offset.x >= -detectionLeft &&
                    offset.x <= detectionRight &&
                    offset.y >= -detectionDown &&
                    offset.y <= detectionUp;

                if (playerInRange)
                    StartFalling();

                break;

            case State.Falling:
                body.linearVelocity = Vector2.down * fallSpeed;

                if (startPosition.y - body.position.y >= maxFallDistance)
                    HideSpike();

                break;

            case State.Embedded:
                embeddedTimer -= Time.fixedDeltaTime;

                if (embeddedTimer <= 0f)
                    HideSpike();

                break;

            case State.Hidden:
                respawnTimer -= Time.fixedDeltaTime;

                if (respawnTimer <= 0f)
                    Respawn();

                break;
        }
    }

    void StartFalling()
    {
        state = State.Falling;

        body.bodyType = RigidbodyType2D.Dynamic;
        spikeCollider.enabled = true;
        body.linearVelocity = Vector2.down * fallSpeed;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (state != State.Falling)
            return;

        Phase1Controller hitPlayer =
            collision.collider.GetComponentInParent<Phase1Controller>();

        if (hitPlayer != null)
        {
            hitPlayer.Hit();
            HideSpike();
            return;
        }

        EmbedSpike();
    }

    void EmbedSpike()
    {
        state = State.Embedded;
        embeddedTimer = embeddedDuration;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.bodyType = RigidbodyType2D.Kinematic;

        //keep the sprite visible but prevent it from blocking
        //or damaging the player after landing
        spikeCollider.enabled = false;

        if (embeddedDuration <= 0f)
            HideSpike();
    }

    void HideSpike()
    {
        state = State.Hidden;
        respawnTimer = respawnDelay;

        body.linearVelocity = Vector2.zero;
        body.bodyType = RigidbodyType2D.Kinematic;
        spikeCollider.enabled = false;

        SetVisible(false);
    }

    void Respawn()
    {
        body.linearVelocity = Vector2.zero;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.position = startPosition;

        spikeCollider.enabled = false;
        SetVisible(true);

        state = State.Waiting;
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
                startPosition.x,
                startPosition.y,
                transform.position.z
            )
            : transform.position;

        Vector3 detectionCenter = origin + new Vector3(
            (detectionRight - detectionLeft) * 0.5f,
            (detectionUp - detectionDown) * 0.5f,
            0f
        );

        Vector3 detectionSize = new Vector3(
            detectionLeft + detectionRight,
            detectionUp + detectionDown,
            0f
        );

        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawCube(detectionCenter, detectionSize);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(detectionCenter, detectionSize);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            origin,
            origin + Vector3.down * maxFallDistance
        );
    }
}