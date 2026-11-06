using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class CrumblingLedge : MonoBehaviour
{
    [SerializeField, Min(0f)] float timeToBreak = 0.75f;
    [SerializeField, Min(0.01f)] float respawnTime = 3f;

    enum State { Ready, Crumbling, Hidden }

    Collider2D ledgeCollider;
    SpriteRenderer[] visuals;
    Color[] originalColors;

    State state;
    float timer;

    void Awake()
    {
        ledgeCollider = GetComponent<Collider2D>();
        ledgeCollider.isTrigger = false;

        visuals = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[visuals.Length];

        for (int i = 0; i < visuals.Length; i++)
            originalColors[i] = visuals[i].color;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryStartCrumbling(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryStartCrumbling(collision);
    }

    void TryStartCrumbling(Collision2D collision)
    {
        if (state != State.Ready)
            return;

        if (collision.collider.GetComponentInParent<Phase1Controller>() == null)
            return;

        //on the ledge, a downward contact normal means
        //the player is standing on its top
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y < -0.5f)
            {
                state = State.Crumbling;
                timer = timeToBreak;
                return;
            }
        }
    }

    void Update()
    {
        if (state == State.Ready)
            return;

        timer -= Time.deltaTime;

        if (state == State.Crumbling)
        {
            // Flash as a temporary warning effect.
            float flash = Mathf.PingPong(Time.time * 8f, 1f);

            for (int i = 0; i < visuals.Length; i++)
            {
                visuals[i].color = Color.Lerp(
                    originalColors[i],
                    Color.red,
                    flash
                );
            }

            if (timer <= 0f)
            {
                state = State.Hidden;
                timer = respawnTime;
                ledgeCollider.enabled = false;

                foreach (SpriteRenderer visual in visuals)
                    visual.enabled = false;
            }
        }
        else if (timer <= 0f)
        {
            state = State.Ready;
            ledgeCollider.enabled = true;

            for (int i = 0; i < visuals.Length; i++)
            {
                visuals[i].enabled = true;
                visuals[i].color = originalColors[i];
            }
        }
    }
}