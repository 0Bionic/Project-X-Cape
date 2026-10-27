using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Phase1Controller : MonoBehaviour
{
    const string IdleSheetPath = "Assets/Sprites/3 Dude_Monster/Dude_Monster_Idle_4.png";
    const string RunSpritePath = "Assets/Sprites/3 Dude_Monster/Dude_Monster_Run_6.png";
    const string JumpSpritePath = "Assets/Sprites/3 Dude_Monster/Dude_Monster_Jump_8.png";
    const string HurtSpritePath = "Assets/Sprites/3 Dude_Monster/Dude_Monster_Hurt_4.png";

    [SerializeField, Min(0f)] float maxSpeed = 6f;
    [SerializeField, Min(0f)] float acceleration = 24f;
    [SerializeField, Min(0f)] float deceleration = 36f;
    [SerializeField, Min(1f)] float runFrameRate = 12f;
    [SerializeField, Min(1f)] float idleFrameRate = 6f;
    [SerializeField, Min(1f)] float jumpFrameRate = 12f;
    [SerializeField, Min(1f)] float hurtFrameRate = 8f;
    [SerializeField, Min(0f)] float jumpHeight = 0.4f;
    [SerializeField, Min(0.01f)] float gravity = 12f;
    [SerializeField, Min(0f)] float dashSpeed = 14f;
    [SerializeField, Min(0f)] float dashAcceleration = 90f;

    [SerializeField] Sprite[] idleFrames;
    [SerializeField] Sprite[] runFrames;
    [SerializeField] Sprite[] jumpFrames;
    [SerializeField] Sprite[] hurtFrames;

    static readonly Color DashTint = new Color(0.35f, 0.7f, 1f, 1f);
    static readonly Color HurtTint = new Color(1f, 0.2f, 0.2f, 1f);

    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    ParticleSystem dashParticles;
    Vector3 spriteRestLocalPosition;
    Color spriteColor = Color.white;
    Vector2 moveInput;
    Vector2 lastMoveDirection = Vector2.right;
    Vector2 dashDirection = Vector2.right;
    float runTime;
    float idleTime;
    float dashTime;
    float hurtTime;
    float jumpSpeed;
    Vector3 spawnPosition;
    bool jumping;
    bool hurting;
    bool dashing;
    bool grounded;
    bool dashReady = true;
    bool leftGroundAfterDash;
    readonly ContactPoint2D[] groundContacts = new ContactPoint2D[8];

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        if (spriteRenderer.transform == transform)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            SpriteRenderer visualRenderer = visual.AddComponent<SpriteRenderer>();
            visualRenderer.sprite = spriteRenderer.sprite;
            visualRenderer.color = spriteRenderer.color;
            visualRenderer.flipX = spriteRenderer.flipX;
            visualRenderer.flipY = spriteRenderer.flipY;
            visualRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            visualRenderer.sortingOrder = spriteRenderer.sortingOrder;
            spriteRenderer.enabled = false;
            spriteRenderer = visualRenderer;
        }

        spriteRestLocalPosition = spriteRenderer.transform.localPosition;
        spriteColor = spriteRenderer.color;
        EnsureBodyCollider();
        CreateDashParticles();
        EnsureSprites();
        if (idleFrames != null && idleFrames.Length > 0)
            spriteRenderer.sprite = idleFrames[0];

        spawnPosition = transform.position;
    }

    void OnDisable()
    {
        if (body != null)
            body.linearVelocity = Vector2.zero;

        EndDash();
    }

    void Update()
    {
        moveInput = ReadMoveInput();
        if (!hurting && moveInput.sqrMagnitude > 0f)
            lastMoveDirection = moveInput.normalized;

        if (!hurting && dashReady && WasDashPressed())
            StartDash();
        else if (!hurting && !dashing && !jumping && WasJumpPressed())
            Jump();

        UpdateFacing();
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        if (hurting)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        float dt = Time.fixedDeltaTime;
        Vector2 velocity = body.linearVelocity;

        if (dashing)
            velocity.x = Mathf.MoveTowards(velocity.x, dashDirection.x * dashSpeed, dashAcceleration * dt);
        else
        {
            float xRate;
            if (Mathf.Abs(moveInput.x) > 0f)
                xRate = acceleration;
            else
                xRate = deceleration;
            velocity.x = Mathf.MoveTowards(velocity.x, moveInput.x * maxSpeed, xRate * dt);

            if (moveInput.y < 0f)
                velocity.y = Mathf.MoveTowards(velocity.y, -maxSpeed, acceleration * dt);
        }

        bool levelDash = dashing && Mathf.Abs(dashDirection.y) <= 0.01f;
        if (!levelDash)
            velocity.y -= gravity * dt;
        float fallLimit;
        if (dashing)
            fallLimit = dashSpeed;
        else
            fallLimit = maxSpeed;
        if (velocity.y < -fallLimit)
            velocity.y = -fallLimit;

        if (jumping && velocity.y <= 0f)
            jumping = false;

        body.linearVelocity = velocity;
        UpdateGrounded();
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

    static bool WasJumpPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }

    static bool WasDashPressed()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.rightButton.wasPressedThisFrame;
    }

    void UpdateFacing()
    {
        float direction;
        if (dashing)
            direction = dashDirection.x;
        else
            direction = moveInput.x;
        if (direction > 0f)
            spriteRenderer.flipX = false;
        else if (direction < 0f)
            spriteRenderer.flipX = true;
    }

    void StartDash()
    {
        if (moveInput.sqrMagnitude > 0f)
            dashDirection = moveInput.normalized;
        else
            dashDirection = lastMoveDirection;
        if (dashDirection.sqrMagnitude < 0.01f)
            dashDirection = Vector2.right;

        dashing = true;
        jumping = false;
        dashReady = false;
        leftGroundAfterDash = !grounded;
        dashTime = 0f;

        Vector2 velocity = body.linearVelocity;
        velocity.x = dashDirection.x * dashSpeed;
        velocity.y = dashDirection.y * dashSpeed;
        body.linearVelocity = velocity;

        spriteRenderer.color = DashTint;
        PlayDashParticles();
    }

    public void Hit()
    {
        if (hurting)
            return;

        EndDash();
        hurting = true;
        hurtTime = 0f;
        dashReady = true;
        body.linearVelocity = Vector2.zero;
        if (spriteRenderer != null)
            spriteRenderer.color = HurtTint;
    }

    void Jump()
    {
        jumping = true;
        jumpSpeed = Mathf.Max(maxSpeed, Mathf.Sqrt(2f * gravity * Mathf.Max(jumpHeight, 0.01f)));
        Vector2 velocity = body.linearVelocity;
        velocity.y = jumpSpeed;
        body.linearVelocity = velocity;
    }

    void EndDash()
    {
        dashing = false;
        jumping = false;
        dashTime = 0f;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = spriteColor;
            spriteRenderer.transform.localPosition = spriteRestLocalPosition;
        }

        if (dashParticles != null)
            dashParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void EndDashBoost()
    {
        dashing = false;
        dashTime = 0f;
        if (spriteRenderer != null)
            spriteRenderer.color = spriteColor;

        if (dashParticles != null)
            dashParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void UpdateAnimation()
    {
        spriteRenderer.transform.localPosition = spriteRestLocalPosition;

        if (hurting)
        {
            hurtTime += Time.deltaTime;
            spriteRenderer.color = HurtTint;
            if (hurtFrames != null && hurtFrames.Length > 0)
            {
                int frame = Mathf.FloorToInt(hurtTime * hurtFrameRate);
                if (frame >= hurtFrames.Length)
                    FinishHurt();
                else
                {
                    spriteRenderer.sprite = hurtFrames[frame];
                    return;
                }
            }
            else
                FinishHurt();
        }

        if (dashing)
        {
            dashTime += Time.deltaTime;
            if (dashTime >= CurrentDashDuration)
            {
                EndDashBoost();
                if (grounded && !leftGroundAfterDash)
                    dashReady = true;
            }
            else
            {
                PlayRun();
                return;
            }
        }

        if (jumping && jumpFrames != null && jumpFrames.Length > 0)
        {
            spriteRenderer.sprite = jumpFrames[JumpFrame()];
            return;
        }

        if (Mathf.Abs(moveInput.x) > 0f || moveInput.y < 0f)
        {
            PlayRun();
            return;
        }

        runTime = 0f;
        if (idleFrames == null || idleFrames.Length == 0)
            return;

        idleTime += Time.deltaTime;
        int idleFrame = (int)(idleTime * idleFrameRate) % idleFrames.Length;
        spriteRenderer.sprite = idleFrames[idleFrame];
    }

    void FinishHurt()
    {
        hurting = false;
        hurtTime = 0f;
        body.linearVelocity = Vector2.zero;
        transform.position = spawnPosition;
        if (spriteRenderer != null)
            spriteRenderer.color = spriteColor;
    }

    public void SetRespawnPoint(Vector3 position)
    {
    if (hurting)
        return;

    spawnPosition = new Vector3(
        position.x,
        position.y,
        transform.position.z
    );
    }

    void PlayRun()
    {
        idleTime = 0f;
        if (runFrames == null || runFrames.Length == 0)
            return;

        runTime += Time.deltaTime;
        int frame = (int)(runTime * runFrameRate) % runFrames.Length;
        spriteRenderer.sprite = runFrames[frame];
    }

    void EnsureBodyCollider()
    {
        if (GetComponent<Collider2D>() != null)
            return;

        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
        if (spriteRenderer.sprite == null)
            return;

        Bounds spriteBounds = spriteRenderer.sprite.bounds;
        box.size = spriteBounds.size;
        box.offset = spriteBounds.center;
    }

    void LateUpdate()
    {
    if (spriteRenderer == null || spriteRenderer.sprite == null)
        return;

    BoxCollider2D box = GetComponent<BoxCollider2D>();
    if (box == null)
        return;

    float colliderBottom = box.offset.y - box.size.y * 0.5f;
    float spriteBottom = spriteRenderer.sprite.bounds.min.y
                         * spriteRenderer.transform.localScale.y;

    Vector3 position = spriteRestLocalPosition;
    position.y = colliderBottom - spriteBottom;
    spriteRenderer.transform.localPosition = position;
    }

    void UpdateGrounded()
    {
        bool wasGrounded = grounded;
        grounded = CheckGrounded();

        if (!grounded)
            leftGroundAfterDash = true;

        if (!dashReady && grounded && leftGroundAfterDash && !wasGrounded)
        {
            leftGroundAfterDash = false;
            dashReady = true;
            if (dashing)
                EndDashBoost();
        }
    }

    bool CheckGrounded()
    {
        int count = body.GetContacts(groundContacts);
        for (int i = 0; i < count; i++)
        {
            if (groundContacts[i].normal.y > 0.5f)
                return true;
        }

        Collider2D collider = GetComponent<Collider2D>();
        if (collider == null)
            return false;

        Bounds bounds = collider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.02f);
        Vector2 size = new Vector2(Mathf.Max(0.02f, bounds.size.x * 0.75f), 0.04f);
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, 0.1f);
        return hit.collider != null && hit.rigidbody != body;
    }

    float DashDuration
    {
        get
        {
            if (jumpFrames != null && jumpFrames.Length > 0)
                return jumpFrames.Length / jumpFrameRate;
            return 0.45f;
        }
    }

    float CurrentDashDuration
    {
        get
        {
            if (Mathf.Abs(dashDirection.y) <= 0.01f)
                return DashDuration * 0.5f;
            return DashDuration;
        }
    }

    int JumpFrame()
    {
        int count = jumpFrames.Length;
        int risingFrames = Mathf.Max(1, count / 2);
        float speed = Mathf.Max(0.01f, jumpSpeed);
        float rise = 1f - Mathf.Clamp01(body.linearVelocity.y / speed);
        return Mathf.Clamp(Mathf.FloorToInt(rise * risingFrames), 0, risingFrames - 1);
    }

    void CreateDashParticles()
    {
        GameObject particleObject = new GameObject("Dash Particles");
        particleObject.transform.SetParent(transform, false);
        dashParticles = particleObject.AddComponent<ParticleSystem>();
        dashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = dashParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = 0.16f;
        main.startSpeed = 1f;
        main.startSize = 0.06f;
        main.startColor = new Color(0.75f, 0.77f, 0.8f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.maxParticles = 20;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = dashParticles.emission;
        emission.rateOverTime = 10f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3) });

        ParticleSystem.ShapeModule shape = dashParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 16f;
        shape.radius = 0.05f;
        shape.radiusThickness = 0f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = dashParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.92f, 0.93f, 0.95f), 0f),
                new GradientColorKey(new Color(0.58f, 0.6f, 0.64f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.35f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = dashParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

        ParticleSystemRenderer particleRenderer = dashParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.velocityScale = 0.02f;
        particleRenderer.lengthScale = 1.1f;
        particleRenderer.cameraVelocityScale = 0f;
        particleRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        particleRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader != null)
            particleRenderer.material = new Material(shader);
    }

    void PlayDashParticles()
    {
        float scale = Mathf.Max(0.01f, transform.lossyScale.x);
        ParticleSystem.MainModule main = dashParticles.main;
        main.startSize = 0.07f * scale;
        main.startSpeed = 1.1f * scale;

        ParticleSystem.ShapeModule shape = dashParticles.shape;
        shape.radius = 0.04f * scale;

        Vector3 trailDirection = new Vector3(-dashDirection.x, -dashDirection.y, 0f);
        dashParticles.transform.rotation = Quaternion.LookRotation(trailDirection);
        dashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        dashParticles.Play(true);
    }

    void EnsureSprites()
    {
#if UNITY_EDITOR
        if (idleFrames == null || idleFrames.Length == 0)
            idleFrames = LoadSprites(IdleSheetPath);
        if (runFrames == null || runFrames.Length == 0)
            runFrames = LoadSprites(RunSpritePath);
        if (jumpFrames == null || jumpFrames.Length == 0)
            jumpFrames = LoadSprites(JumpSpritePath);
        if (hurtFrames == null || hurtFrames.Length == 0)
            hurtFrames = LoadSprites(HurtSpritePath);
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureSprites();
    }

    static Sprite[] LoadSprites(string path)
    {
        Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        int count = 0;
        foreach (Object asset in assets)
        {
            if (asset is Sprite)
                count++;
        }

        Sprite[] sprites = new Sprite[count];
        int index = 0;
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite)
                sprites[index++] = sprite;
        }

        System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
        return sprites;
    }
#endif
}
