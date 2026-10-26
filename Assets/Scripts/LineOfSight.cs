using UnityEngine;

public static class LineOfSight
{
    public static bool CanSee(Transform viewer, Transform target, float viewDistance, float viewAngle)
    {
        if (viewer == null || target == null)
            return false;

        Vector2 toTarget = (Vector2)target.position - (Vector2)viewer.position;
        float distance = toTarget.magnitude;
        if (distance > viewDistance)
            return false;
        if (distance <= 0.001f)
            return true;

        Vector2 facing = Vector2.right;
        SpriteRenderer sprite = viewer.GetComponentInChildren<SpriteRenderer>();
        if (sprite != null && sprite.flipX)
            facing = Vector2.left;

        if (Vector2.Angle(facing, toTarget) > viewAngle * 0.5f)
            return false;

        RaycastHit2D[] hits = Physics2D.RaycastAll(viewer.position, toTarget / distance, distance);
        float closestDistance = float.MaxValue;
        Collider2D closest = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null)
                continue;
            if (IsViewer(hit, viewer))
                continue;
            if (hit.isTrigger && !IsPlayer(hit))
                continue;
            if (hits[i].distance < closestDistance)
            {
                closestDistance = hits[i].distance;
                closest = hit;
            }
        }

        if (closest == null)
            return false;

        return IsPlayer(closest);
    }

    static bool IsViewer(Collider2D hit, Transform viewer)
    {
        Transform hitTransform = hit.transform;
        if (hitTransform == viewer)
            return true;
        if (hitTransform.IsChildOf(viewer))
            return true;
        if (viewer.IsChildOf(hitTransform))
            return true;
        return false;
    }

    static bool IsPlayer(Collider2D hit)
    {
        if (hit.GetComponent<TopDownController>() != null)
            return true;
        if (hit.GetComponentInParent<TopDownController>() != null)
            return true;
        return false;
    }
}
