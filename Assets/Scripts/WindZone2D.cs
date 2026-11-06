using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(BoxCollider2D))]
public class WindZone2D : MonoBehaviour
{
    public enum WindMode { Push, JumpBoost }

    [SerializeField] WindMode mode;

    [Header("Area — World Units")]
    [SerializeField, Min(0.01f)] float width = 2f;
    [SerializeField, Min(0.01f)] float height = 2f;

    [Header("Push")]
    [SerializeField] Vector2 direction = Vector2.right;
    [SerializeField, Min(0f)] float strength = 20f;

    [Header("Jump Assistance")]
    [SerializeField, Min(1f)] float jumpHeightMultiplier = 2f;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] float activeDuration = 2f;

    [Tooltip("Inactive time between gusts. Zero means always active.")]
    [SerializeField, Min(0f)] float timeBetweenActivations = 0f;

    static readonly List<WindZone2D> zones = new();

    BoxCollider2D area;
    Phase1Controller player;
    Collider2D playerCollider;
    Rigidbody2D playerBody;

    bool active = true;
    float timer;

    public bool IsActive =>
        timeBetweenActivations <= 0f || active;

    void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        ConfigureArea();

        timer = activeDuration;
    }

    void OnEnable()
    {
        zones.Add(this);
    }

    void OnDisable()
    {
        zones.Remove(this);
    }

    void Start()
    {
        player = FindAnyObjectByType<Phase1Controller>();

        if (player != null)
        {
            playerCollider = player.GetComponent<Collider2D>();
            playerBody = player.GetComponent<Rigidbody2D>();
        }
    }

    void FixedUpdate()
    {
        UpdateCycle(Time.fixedDeltaTime);

        if (mode != WindMode.Push ||
            !IsActive ||
            playerBody == null ||
            !Contains(playerCollider))
        {
            return;
        }

        //mass-independent acceleration
        //physics applies the force after the players controller 
        //has assigned its velocity
        Vector2 force = direction.normalized * strength * playerBody.mass;
        playerBody.AddForce(force, ForceMode2D.Force);
    }

    void UpdateCycle(float dt)
    {
        if (timeBetweenActivations <= 0f)
        {
            active = true;
            return;
        }

        timer -= dt;

        if (timer <= 0f)
        {
            active = !active;
            timer = active ? activeDuration : timeBetweenActivations;
        }
    }

    bool Contains(Collider2D other)
    {
        if (area == null || other == null ||
            !area.enabled || !other.enabled)
        {
            return false;
        }

        return area.Distance(other).isOverlapped;
    }

    public static float GetJumpHeightMultiplier(Collider2D playerCollider)
    {
        float multiplier = 1f;

        foreach (WindZone2D zone in zones)
        {
            if (zone != null &&
                zone.mode == WindMode.JumpBoost &&
                zone.IsActive &&
                zone.Contains(playerCollider))
            {
                //overlapping helping zones use the strongest boost
                multiplier = Mathf.Max(
                    multiplier,
                    zone.jumpHeightMultiplier
                );
            }
        }

        return multiplier;
    }

    void ConfigureArea()
    {
        if (area == null)
            area = GetComponent<BoxCollider2D>();

        area.isTrigger = true;
        area.offset = Vector2.zero;

        //compensate for object scale so dimensions remain world units
        Vector3 scale = transform.lossyScale;

        area.size = new Vector2(
            width / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
            height / Mathf.Max(Mathf.Abs(scale.y), 0.0001f)
        );
    }

    void OnValidate()
    {
        ConfigureArea();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Application.isPlaying && !IsActive
            ? Color.gray
            : mode == WindMode.Push ? Color.cyan : Color.green;

        Matrix4x4 previousMatrix = Gizmos.matrix;

        Gizmos.matrix = Matrix4x4.TRS(
            transform.position,
            transform.rotation,
            Vector3.one
        );

        Gizmos.DrawWireCube(
            Vector3.zero,
            new Vector3(width, height, 0f)
        );

        Gizmos.matrix = previousMatrix;
    }
}