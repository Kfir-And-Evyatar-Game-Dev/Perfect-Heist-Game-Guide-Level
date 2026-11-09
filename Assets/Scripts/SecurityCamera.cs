using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A security camera that sees every character inside its radius (not through walls).
/// Detection uses a trigger collider, so it relies on 2D physics collisions.
/// The hacker can switch it off for a few seconds.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class SecurityCamera : MonoBehaviour
{
    public static readonly List<SecurityCamera> All = new List<SecurityCamera>();

    [Tooltip("Child sprite (a circle) that shows the viewing radius.")]
    [SerializeField] SpriteRenderer rangeRenderer;
    [SerializeField] Color activeColor = new Color(1f, 0f, 0f, 0.25f);
    [SerializeField] Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

    public Vector2Int Cell { get; private set; }
    public float Radius { get; private set; }

    MapBuilder map;
    float disabledUntil = -1f;
    VisionCone cone;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public static SecurityCamera At(Vector2Int cell) => All.Find(c => c.Cell == cell);

    public void Init(MapBuilder map, Vector2Int cell, float radius)
    {
        this.map = map;
        Cell = cell;
        Radius = radius;

        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radius;

        // The view is drawn as rays that stop at walls, instead of a plain circle.
        if (rangeRenderer != null)
        {
            Material mat = rangeRenderer.sharedMaterial;
            rangeRenderer.enabled = false;
            cone = VisionCone.Create("Camera view", map, () => Cell, null, radius, 360f, activeColor, mat, 2);
            cone.transform.SetParent(transform.parent, true);
        }

        ResetState();
    }

    public bool IsDisabledAt(float t) => t < disabledUntil;

    public void DisableFor(float seconds) => disabledUntil = RunClock.Instance.RunTime + seconds;

    public void ResetState() => disabledUntil = -1f;

    void Update()
    {
        if (cone != null && RunClock.Instance != null)
            cone.SetColor(IsDisabledAt(RunClock.Instance.CurrentTime) ? disabledColor : activeColor);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!other.TryGetComponent(out Character c)) return;

        RunClock clock = RunClock.Instance;
        if (clock == null || clock.State != RunState.Running) return;
        if (IsDisabledAt(clock.RunTime)) return;

        // The collider has the character's own radius added; check the exact design radius.
        if (Vector2.Distance(Cell, c.DocPos) > Radius) return;
        if (!map.HasLineOfSight(Cell, c.DocPos)) return;

        clock.ReportDetected(c, "the camera");
    }
}
