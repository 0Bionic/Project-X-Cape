using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class collision_kill : MonoBehaviour
{
    void OnCollisionEnter2D(Collision2D collision)
    {
        HitPlayer(collision.collider);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HitPlayer(other);
    }

    static void HitPlayer(Collider2D other)
    {
        Phase1Controller player = other.GetComponent<Phase1Controller>();
        if (player == null)
            player = other.GetComponentInParent<Phase1Controller>();
        if (player != null)
            player.Hit();
    }
}
