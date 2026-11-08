using UnityEngine;

/// <summary>
/// A guard walking back and forth between two cells on a fixed schedule.
/// Sees characters inside a cone in front of it, but not through walls.
/// </summary>
public class Guard : MonoBehaviour
{
    [Tooltip("How far the guard sees, in cells.")]
    public float viewDistance = 3f;

    [Tooltip("Total width of the view cone, in degrees.")]
    public float viewAngle = 90f;

    [Tooltip("Optional child object that is rotated to face the walking direction.")]
    public Transform facingVisual;

    [Tooltip("Tick if the image shows the guard looking toward the bottom of the picture.")]
    public bool imageFacesDown = true;

    public Vector2 DocPos { get; private set; }
    /// <summary>Walking direction in document coordinates (y down).</summary>
    public Vector2 Facing { get; private set; } = Vector2.up;

    MapBuilder map;
    Vector2Int pointA, pointB;
    float period;

    public void Init(MapBuilder map, Vector2Int start, Vector2Int patrolEnd, float period)
    {
        this.map = map;
        pointA = start;
        pointB = patrolEnd;
        this.period = Mathf.Max(0.1f, period);
        ShowAt(0f);
    }

    /// <summary>Patrol position at run time t: A to B in the first half of the period, back in the second.</summary>
    public void ShowAt(float t)
    {
        if (map == null) return;

        float half = period / 2f;
        float phase = Mathf.Repeat(t, period);
        bool outward = phase < half;
        float k = outward ? phase / half : (phase - half) / half;

        DocPos = outward ? Vector2.Lerp(pointA, pointB, k) : Vector2.Lerp(pointB, pointA, k);
        Vector2 dir = outward ? (Vector2)(pointB - pointA) : (Vector2)(pointA - pointB);
        if (dir != Vector2.zero) Facing = dir.normalized;

        transform.position = map.DocToWorld(DocPos);
        if (facingVisual != null)
        {
            var worldFacing = new Vector3(Facing.x, -Facing.y, 0f); // document y points down
            facingVisual.up = imageFacesDown ? -worldFacing : worldFacing;
        }
    }

    public bool CanSee(Vector2 target)
    {
        Vector2 toTarget = target - DocPos;
        if (toTarget.magnitude > viewDistance) return false;
        if (toTarget.sqrMagnitude > 0.0001f && Vector2.Angle(Facing, toTarget) > viewAngle / 2f) return false;
        return map.HasLineOfSight(DocPos, target);
    }

    void OnDrawGizmos()
    {
        // Draws the view cone in the Scene view.
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        Vector3 forward = new Vector3(Facing.x, -Facing.y, 0f);
        Vector3 left = Quaternion.Euler(0, 0, viewAngle / 2f) * forward * viewDistance;
        Vector3 right = Quaternion.Euler(0, 0, -viewAngle / 2f) * forward * viewDistance;
        Vector3 p = transform.position;
        Gizmos.DrawLine(p, p + left);
        Gizmos.DrawLine(p, p + right);
        Gizmos.DrawLine(p + left, p + right);
    }
}
