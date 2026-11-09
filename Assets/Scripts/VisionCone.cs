using System;
using UnityEngine;

/// <summary>
/// Draws what a camera or guard can see: a fan of rays that stop at the walls,
/// filled as a coloured mesh. Rebuilt every frame, so it follows a moving,
/// turning guard. Uses the same wall test as the detection code.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VisionCone : MonoBehaviour
{
    MapBuilder map;
    Func<Vector2> origin;    // document coordinates
    Func<Vector2> facing;    // document direction (y down); ignored for a full circle
    float radius;
    float angle;             // total width in degrees; 360 = all around
    int rays;
    Mesh mesh;
    MeshRenderer meshRenderer;
    Color color;

    public static VisionCone Create(string name, MapBuilder map, Func<Vector2> origin, Func<Vector2> facing,
                                    float radius, float angle, Color color, Material material, int sortingOrder)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        var cone = go.AddComponent<VisionCone>();
        cone.map = map;
        cone.origin = origin;
        cone.facing = facing;
        cone.radius = radius;
        cone.angle = angle;
        cone.rays = Mathf.Max(8, Mathf.CeilToInt(angle / 2f)); // a ray every 2 degrees
        cone.mesh = new Mesh { name = name };
        cone.mesh.MarkDynamic();
        go.GetComponent<MeshFilter>().sharedMesh = cone.mesh;
        cone.meshRenderer = go.GetComponent<MeshRenderer>();
        cone.meshRenderer.sharedMaterial = material;   // a sprite material, so vertex colours show
        cone.meshRenderer.sortingOrder = sortingOrder;
        cone.color = color;
        cone.Rebuild();
        return cone;
    }

    public void SetColor(Color c) => color = c;
    public void SetVisible(bool visible) => meshRenderer.enabled = visible;

    void LateUpdate() => Rebuild();

    void Rebuild()
    {
        if (map == null || origin == null) return;

        Vector2 o = origin();
        bool full = angle >= 359.9f;
        float startDeg = 0f;
        if (!full)
        {
            Vector2 f = facing != null ? facing() : Vector2.right;
            startDeg = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg - angle / 2f;
        }

        int count = full ? rays : rays + 1;
        var vertices = new Vector3[count + 1];
        var colors = new Color[count + 1];
        vertices[0] = map.DocToWorld(o);
        colors[0] = color;

        for (int i = 0; i < count; i++)
        {
            float deg = startDeg + angle * i / rays;
            var dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
            float dist = map.RayDistance(o, dir, radius);
            vertices[i + 1] = map.DocToWorld(o + dir * dist);
            colors[i + 1] = new Color(color.r, color.g, color.b, color.a * 0.55f); // fade toward the edge
        }

        int tris = full ? count : count - 1;
        var triangles = new int[tris * 3];
        for (int i = 0; i < tris; i++)
        {
            int a = i + 1, b = (i + 1) % count + 1;
            // Document y points down, so the fan is mirrored in world space; keep both windings visible.
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = b;
            triangles[i * 3 + 2] = a;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }
}
