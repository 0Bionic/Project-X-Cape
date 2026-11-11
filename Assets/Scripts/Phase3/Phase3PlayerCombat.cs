using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Phase3PlayerCombat : MonoBehaviour
{
    [Header("Ranged Melee Attack")]
    [SerializeField] Key meleeKey = Key.F;
    [SerializeField, Min(0.1f)] float meleeDamage = 15f;
    [SerializeField, Min(0.05f)] float meleeCooldown = 0.35f;
    [SerializeField, Min(0.1f)] float meleeReach = 1.8f;
    [SerializeField, Min(0.2f)] float meleeRadius = 1.5f;
    [SerializeField] Vector2 meleeVertOffset = new Vector2(0f, 0.1f);
    [SerializeField] bool aimMeleeToMouse;
    [SerializeField] Color meleeAttackColor = new Color(0.55f, 0.92f, 1f, 0.9f);    // Temp cuz of no assets

    [Header("Gun : Press left Click to shoot")]
    [SerializeField] PlayerBullet bulletPrefab;
    [SerializeField] Transform firePoint;
    [SerializeField, Min(0.1f)] float bulletDamage = 5f;
    [SerializeField, Min(1f)] float bulletSpeed = 28f;
    [SerializeField, Min(0.05f)] float firingCooldown = 0.2f;
    [SerializeField, Min(0.1f)] float bulletLifetime = 2.5f;
    [SerializeField, Min(0f)] float muzzleOffset = 0.8f;
    [SerializeField] Vector2 fallbackBulletScale = new Vector2(0.55f, 0.22f);       // Temp cuz of no assets
    [SerializeField] Color fallbackBulletColor = new Color(1f, 0.85f, 0.2f, 1f);    // Temp cuz of no assets

    SpriteRenderer activeSpriteRenderer;
    Camera mainCam;
    float meleeTimer;
    float fireTimer;

    static Sprite cachedBulletSprite;
    static Sprite cachedSlashSprite;

    void Start()
    {
        mainCam = Camera.main;
        UpdateActiveSpriteRender();
    }

    void Update()
    {
        meleeTimer = Mathf.Max(0f, meleeTimer - Time.deltaTime);
        fireTimer = Mathf.Max(0f, fireTimer - Time.deltaTime);

        if (activeSpriteRenderer == null || !activeSpriteRenderer.enabled)
            UpdateActiveSpriteRender();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[meleeKey].wasPressedThisFrame && meleeTimer <= 0f)
        {
            DoMeleeAttack();
            meleeTimer = meleeCooldown;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed && fireTimer <= 0f)
        {
            ShootBullet(mouse);
            fireTimer = firingCooldown;
        }
    }

    void UpdateActiveSpriteRender()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].enabled)
            {
                activeSpriteRenderer = renderers[i];
                return;
            }
        }
    }

    Vector2 GetDirectionFacing()
    {
        if (activeSpriteRenderer != null)
            return activeSpriteRenderer.flipX ? Vector2.left : Vector2.right;

        return Vector2.right;
    }

    Vector2 GetMouseAimDirection(Vector2 origin, Mouse mouse)
    {
        if (mainCam == null)
            mainCam = Camera.main;

        if (mainCam == null || mouse == null)
            return GetDirectionFacing();

        Vector2 screenPos = mouse.position.ReadValue();
        float depth = Mathf.Abs(transform.position.z - mainCam.transform.position.z);
        Vector3 worldPos = mainCam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        Vector2 dir = (Vector2)worldPos - origin;

        return dir.sqrMagnitude > 0.001f ? dir.normalized : GetDirectionFacing();
    }

    void DoMeleeAttack()
    {
        Vector2 dir;
        if (aimMeleeToMouse && Mouse.current != null)
        {
            Vector2 aim = GetMouseAimDirection(transform.position, Mouse.current);
            dir = aim.x >= 0f ? Vector2.right : Vector2.left;
        }
        else
        {
            dir = GetDirectionFacing();
        }

        Vector2 center = (Vector2)transform.position + meleeVertOffset + dir * meleeReach;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, meleeRadius);

        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target != null && damagedTargets.Add(target))
                target.TakeDamage(meleeDamage);
        }

        SpawnSlashVisual(center, dir);
    }

    void ShootBullet(Mouse mouse)
    {
        Vector2 baseOrigin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        Vector2 dir = GetMouseAimDirection(baseOrigin, mouse);
        Vector2 spawnPos = firePoint != null ? baseOrigin : baseOrigin + dir * muzzleOffset;

        PlayerBullet bullet = bulletPrefab != null
            ? Instantiate(bulletPrefab, spawnPos, Quaternion.identity)
            : CreateBulletTemp(spawnPos);

        bullet.Launch(dir, bulletSpeed, bulletDamage, bulletLifetime);
    }

    // Temp Bullets No Assets

    PlayerBullet CreateBulletTemp(Vector2 spawnPos)
    {
        GameObject boooolet = new GameObject("PlayerBullet");
        boooolet.transform.position = spawnPos;
        boooolet.transform.localScale = new Vector3(fallbackBulletScale.x, fallbackBulletScale.y, 1f);

        SpriteRenderer sr = boooolet.AddComponent<SpriteRenderer>();
        sr.sprite = GetBulletSprite();
        sr.color = fallbackBulletColor;
        sr.sortingOrder = activeSpriteRenderer != null ? activeSpriteRenderer.sortingOrder + 1 : 2;

        CircleCollider2D col = boooolet.AddComponent<CircleCollider2D>();
        col.radius = 0.45f;
        col.isTrigger = true;

        return boooolet.AddComponent<PlayerBullet>();
    }

    void SpawnSlashVisual(Vector2 center, Vector2 dir)
    {
        GameObject slash = new GameObject("MeleeSlashVFX");
        slash.transform.position = center;
        slash.transform.localScale = new Vector3(
            (dir.x >= 0f ? 1f : -1f) * meleeRadius * 1.6f,
            meleeRadius * 1.6f,
            1f);

        SpriteRenderer sr = slash.AddComponent<SpriteRenderer>();
        sr.sprite = GetSlashSprite();
        sr.color = meleeAttackColor;
        sr.sortingOrder = activeSpriteRenderer != null ? activeSpriteRenderer.sortingOrder + 2 : 5;

        Destroy(slash, 0.12f);
    }

    static Sprite GetBulletSprite()
    {
        if (cachedBulletSprite != null)
            return cachedBulletSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        cachedBulletSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return cachedBulletSprite;
    }

    static Sprite GetSlashSprite()
    {
        if (cachedSlashSprite != null)
            return cachedSlashSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1)) * 2f - 1f;
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                float dist = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = (nx > -0.1f && dist >= 0.48f && dist <= 0.92f)
                    ? Mathf.Clamp01((nx + 0.1f) * 1.4f)
                    : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        cachedSlashSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedSlashSprite;
    }

    void OnDrawGizmosSelected()
    {
        Vector2 dir = Application.isPlaying ? GetDirectionFacing() : Vector2.right;
        Vector2 center = (Vector2)transform.position + meleeVertOffset + dir * meleeReach;
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.85f);
        Gizmos.DrawWireSphere(center, meleeRadius);
    }
}
