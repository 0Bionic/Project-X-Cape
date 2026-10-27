using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider2D))]
public class CampfireCheckpoint : MonoBehaviour
{
    [SerializeField] Transform respawnPoint;

    BoxCollider2D interactionArea;

    void Awake()
    {
        interactionArea = GetComponent<BoxCollider2D>();
        interactionArea.isTrigger = true;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        Phase1Controller player =
            other.GetComponentInParent<Phase1Controller>();

        if (player == null)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || !keyboard.eKey.isPressed)
            return;

        if (respawnPoint == null)
            return;

        player.SetRespawnPoint(respawnPoint.position);
    }
}