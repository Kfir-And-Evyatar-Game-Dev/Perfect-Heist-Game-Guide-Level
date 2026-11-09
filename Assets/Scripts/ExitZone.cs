using UnityEngine;

/// <summary>
/// The getaway van. Characters that finish their plan inside its trigger climb in.
/// Once everyone is inside with the diamond, it drives off the right edge of the map.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ExitZone : MonoBehaviour
{
    [Tooltip("Rotation of the parked van. The image points up; -90 turns it to face right, along the street.")]
    public float parkedAngle = -90f;

    [Tooltip("Acceleration when driving away, in cells per second squared.")]
    public float acceleration = 6f;

    MapBuilder map;
    Vector3 parkedPosition;
    float driveStart = -1f;

    public bool IsDriving => driveStart >= 0f;

    void Awake() => GetComponent<Collider2D>().isTrigger = true;

    public void Init(MapBuilder map)
    {
        this.map = map;
        parkedPosition = transform.position;
        transform.rotation = Quaternion.Euler(0f, 0f, parkedAngle);
        ResetState();
    }

    public void StartDriving(float runTime)
    {
        if (driveStart < 0f) driveStart = runTime;
    }

    /// <summary>Moves the van for the current run time (starts slowly, then speeds up).</summary>
    public void Simulate(float runTime)
    {
        if (driveStart < 0f) return;
        float t = runTime - driveStart;
        float distance = 0.5f * acceleration * t * t;
        transform.position = parkedPosition + Vector3.right * distance;
    }

    /// <summary>True once the van is past the right edge of the map.</summary>
    public bool HasLeftMap => map != null &&
        transform.position.x > map.DocToWorld(new Vector2(map.Width + 1, 0)).x;

    public void ResetState()
    {
        driveStart = -1f;
        if (map != null) transform.position = parkedPosition;
    }
}
