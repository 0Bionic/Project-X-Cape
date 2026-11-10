using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] SpriteRenderer mapBounds;
    [SerializeField, Min(0.1f)] float zoom = 8f;

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = zoom;
    }

    void Start()
    {
        if (target == null)
        {
            TopDownController player = FindAnyObjectByType<TopDownController>();
            if (player != null)
                target = player.transform;
        }

        if (mapBounds == null)
            mapBounds = FindPhase2Map();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 position = transform.position;
        position.x = target.position.x;
        position.y = target.position.y;

        if (mapBounds != null)
        {
            Bounds bounds = mapBounds.bounds;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            float minX = bounds.min.x + halfWidth;
            float maxX = bounds.max.x - halfWidth;
            float minY = bounds.min.y + halfHeight;
            float maxY = bounds.max.y - halfHeight;

            if (minX > maxX)
                position.x = bounds.center.x;
            else
                position.x = Mathf.Clamp(position.x, minX, maxX);

            if (minY > maxY)
                position.y = bounds.center.y;
            else
                position.y = Mathf.Clamp(position.y, minY, maxY);
        }

        // Snap to pixel grid (sprites use 100 pixels per unit) to reduce shimmer.
        float pixel = 0.01f;
        position.x = Mathf.Round(position.x / pixel) * pixel;
        position.y = Mathf.Round(position.y / pixel) * pixel;
        transform.position = position;
    }

    static SpriteRenderer FindPhase2Map()
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer.sprite == null)
                continue;
            if (renderer.sprite.name == "Phase2_0")
                return renderer;
            if (renderer.gameObject.name == "Phase2_0" || renderer.gameObject.name == "Phase2")
                return renderer;
        }

        return null;
    }
}
