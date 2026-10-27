using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class TopDownController : MonoBehaviour
{
    [SerializeField, Min(0f)] float moveSpeed = 6f;
    [SerializeField, Min(0f)] float reloadDelay = 0.4f;

    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    Vector2 moveInput;
    bool dead;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (dead)
            return;

        moveInput = ReadMoveInput();
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        if (spriteRenderer != null && Mathf.Abs(moveInput.x) > 0.01f)
            spriteRenderer.flipX = moveInput.x < 0f;
    }

    void FixedUpdate()
    {
        if (dead)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        body.linearVelocity = moveInput * moveSpeed;
    }

    public void CaughtByOfficer()
    {
        if (dead)
            return;

        dead = true;
        moveInput = Vector2.zero;
        body.linearVelocity = Vector2.zero;

        if (spriteRenderer != null)
            spriteRenderer.color = Color.red;

        Invoke(nameof(ReloadPhase), reloadDelay);
    }

    void ReloadPhase()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    static Vector2 ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            input.y -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            input.x += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            input.x -= 1f;

        return input;
    }
}
