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
        TopDownController player = other.GetComponent<TopDownController>();
        if (player == null)
            player = other.GetComponentInParent<TopDownController>();
        if (player != null)
            player.Hit();
    }
}
