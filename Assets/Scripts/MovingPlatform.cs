using System.Collections.Generic;
using UnityEngine;

//move after the player's FixedUpdate, so jumping is already applied
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class MovingPlatform : MonoBehaviour
{
    public enum MovementAxis { Horizontal, Vertical }

    [SerializeField] PhysicsMaterial2D surfaceMaterial;

    [SerializeField] MovementAxis axis;

    [SerializeField, Min(0f)] float speed = 1f;

    [Header("Horizontal")]
    [SerializeField, Min(0f)] float leftDistance = 1f;
    [SerializeField, Min(0f)] float rightDistance = 1f;

    [Header("Vertical")]
    [SerializeField, Min(0f)] float downDistance = 1f;
    [SerializeField, Min(0f)] float upDistance = 1f;

    [Header("Endpoints")]
    [SerializeField, Min(0f)] float pauseDuration = 0.3f;

    Rigidbody2D body;
    Collider2D platformCollider;
    Vector2 origin;

    bool movingPositive = true;
    float pauseTimer;

    readonly HashSet<Rigidbody2D> riders = new();
    readonly List<Rigidbody2D> removeRiders = new();

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        platformCollider = GetComponent<Collider2D>();
        if (surfaceMaterial != null)
            platformCollider.sharedMaterial = surfaceMaterial;

        origin = body.position;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        platformCollider.isTrigger = false;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (pauseTimer > 0f)
        {
            pauseTimer -= dt;
            return;
        }

        Vector2 direction = axis == MovementAxis.Horizontal
            ? Vector2.right
            : Vector2.up;

        float negativeDistance = axis == MovementAxis.Horizontal
            ? leftDistance
            : downDistance;

        float positiveDistance = axis == MovementAxis.Horizontal
            ? rightDistance
            : upDistance;

        if (negativeDistance + positiveDistance <= 0f || speed <= 0f)
            return;

        Vector2 destination = origin + direction * (
            movingPositive ? positiveDistance : -negativeDistance
        );

        Vector2 next = Vector2.MoveTowards(
            body.position,
            destination,
            speed * dt
        );

        Vector2 displacement = next - body.position;

        CarryRiders(displacement);
        body.MovePosition(next);

        if ((next - destination).sqrMagnitude <= 0.000001f)
        {
            movingPositive = !movingPositive;
            pauseTimer = pauseDuration;
        }
    }

    void CarryRiders(Vector2 displacement)
    {
        removeRiders.Clear();

        foreach (Rigidbody2D rider in riders)
        {
            if (rider == null)
            {
                removeRiders.Add(rider);
                continue;
            }

            Collider2D riderCollider = rider.GetComponent<Collider2D>();

            //positive vertical velocity means the player is jumping away
            if (riderCollider == null ||
                !platformCollider.IsTouching(riderCollider) ||
                rider.linearVelocity.y > 0.1f)
            {
                removeRiders.Add(rider);
                continue;
            }

            rider.position += displacement;
        }

        foreach (Rigidbody2D rider in removeRiders)
            riders.Remove(rider);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        RegisterRider(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        RegisterRider(collision);
    }

    void RegisterRider(Collision2D collision)
    {
        Phase1Controller player =
            collision.collider.GetComponentInParent<Phase1Controller>();

        if (player == null)
            return;

        Rigidbody2D rider = player.GetComponent<Rigidbody2D>();

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y < -0.5f)
            {
                riders.Add(rider);
                return;
            }
        }

        riders.Remove(rider);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.rigidbody != null)
            riders.Remove(collision.rigidbody);
    }

    void OnDisable()
    {
        riders.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying
            ? new Vector3(origin.x, origin.y, transform.position.z)
            : transform.position;

        Vector3 direction = axis == MovementAxis.Horizontal
            ? Vector3.right
            : Vector3.up;

        float negativeDistance = axis == MovementAxis.Horizontal
            ? leftDistance
            : downDistance;

        float positiveDistance = axis == MovementAxis.Horizontal
            ? rightDistance
            : upDistance;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            center - direction * negativeDistance,
            center + direction * positiveDistance
        );
    }
}