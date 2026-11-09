using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Builds the floor and walls of a level from a text layout,
/// using the same grid notation as the level design document.
/// Row 0 of the layout is the TOP of the map (y grows downward in the document),
/// while Unity's Y grows upward, so rows are flipped when placing tiles.
/// </summary>
public class MapBuilder : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap floorTilemap;
    public Tilemap wallsTilemap;

    [Header("Tiles")]
    public TileBase floorTile;
    public TileBase wallTile;

    [Header("Layout ('#' = wall, everything else = floor)")]
    [TextArea(8, 20)]
    public string layout =
        "###########\n" +
        "#...&#1.$.#\n" +
        "#....D....#\n" +
        "#....#....#\n" +
        "#....#....#\n" +
        "##D#####D##\n" +
        "..P......X.";

    [Tooltip("Move the Main Camera to the middle of the map after building.")]
    public bool centerCamera = true;

    [Header("Thin walls (looks only: a wall cell still blocks movement and sight)")]
    [Tooltip("Draw walls as thin lines through the middle of the wall cells instead of full tiles.")]
    public bool thinWalls = true;
    [Range(0.05f, 1f)] public float wallThickness = 0.25f;
    [Tooltip("Any plain white square sprite, e.g. Sprites/Wall.")]
    public Sprite wallLineSprite;
    public Color wallColor = new Color(0.22f, 0.25f, 0.32f);

    const string WallVisualsName = "Wall Visuals";

    string[] Rows => layout.Replace("\r", "").Split('\n');

    public int Width => Rows[0].Length;
    public int Height => Rows.Length;

    /// <summary>Document coordinates (x right, y down) to a Unity tilemap cell.</summary>
    public Vector3Int DocToCell(int x, int y) => new Vector3Int(x, Height - 1 - y, 0);

    /// <summary>World position of the centre of a document cell.</summary>
    public Vector3 DocToWorld(int x, int y) => floorTilemap.GetCellCenterWorld(DocToCell(x, y));

    /// <summary>World position of a (possibly fractional) document position.</summary>
    public Vector3 DocToWorld(Vector2 doc) =>
        floorTilemap.transform.TransformPoint(new Vector3(doc.x + 0.5f, Height - 1 - doc.y + 0.5f, 0f));

    /// <summary>The document cell under a world position (for taps on the map).</summary>
    public Vector2Int WorldToDoc(Vector3 world)
    {
        Vector3 local = floorTilemap.transform.InverseTransformPoint(world);
        return new Vector2Int(Mathf.FloorToInt(local.x), Height - 1 - Mathf.FloorToInt(local.y));
    }

    /// <summary>The layout character at a document cell, e.g. '$' for the target.</summary>
    public char CharAt(int x, int y) => Rows[y][x];

    public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.y < Height && c.x < Rows[c.y].Length;

    /// <summary>A cell characters can stand on: inside the map and not a wall.</summary>
    public bool IsWalkable(Vector2Int c) => InBounds(c) && CharAt(c.x, c.y) != '#';

    static readonly Vector2Int[] Directions =
        { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    /// <summary>
    /// Shortest 4-direction path between two document cells (breadth-first search).
    /// Includes both ends. Returns null when the target can't be reached.
    /// </summary>
    public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to)
    {
        if (!IsWalkable(from) || !IsWalkable(to)) return null;

        var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [from] = from };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            if (cell == to) break;
            foreach (Vector2Int d in Directions)
            {
                Vector2Int next = cell + d;
                if (IsWalkable(next) && !cameFrom.ContainsKey(next))
                {
                    cameFrom[next] = cell;
                    queue.Enqueue(next);
                }
            }
        }

        if (!cameFrom.ContainsKey(to)) return null;

        var path = new List<Vector2Int>();
        for (Vector2Int c = to; c != from; c = cameFrom[c]) path.Add(c);
        path.Add(from);
        path.Reverse();
        return path;
    }

    /// <summary>True when no wall cell lies on the straight line between two document positions.</summary>
    public bool HasLineOfSight(Vector2 a, Vector2 b)
    {
        float distance = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.1f));
        for (int i = 1; i < steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            var cell = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            if (!InBounds(cell) || CharAt(cell.x, cell.y) == '#') return false;
        }
        return true;
    }

    [ContextMenu("Build Map")]
    public void BuildMap()
    {
        if (floorTilemap == null || wallsTilemap == null || floorTile == null || wallTile == null)
        {
            Debug.LogError("MapBuilder: assign both Tilemaps and both Tiles in the Inspector first.", this);
            return;
        }

#if UNITY_EDITOR
        Undo.RegisterCompleteObjectUndo(new Object[] { floorTilemap, wallsTilemap }, "Build Map");
#endif

        floorTilemap.ClearAllTiles();
        wallsTilemap.ClearAllTiles();

        string[] rows = Rows;
        for (int y = 0; y < rows.Length; y++)
        {
            for (int x = 0; x < rows[y].Length; x++)
            {
                Vector3Int cell = DocToCell(x, y);
                floorTilemap.SetTile(cell, floorTile);
                if (rows[y][x] == '#')
                    wallsTilemap.SetTile(cell, wallTile);
            }
        }

        BuildThinWalls();

        if (centerCamera && Camera.main != null)
        {
            Transform cam = Camera.main.transform;
#if UNITY_EDITOR
            Undo.RecordObject(cam, "Center Camera");
#endif
            cam.position = new Vector3(Width / 2f, Height / 2f, cam.position.z);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        Debug.Log($"MapBuilder: built a {Width}x{Height} map.", this);
    }

    bool IsWallCell(int x, int y) => InBounds(new Vector2Int(x, y)) && CharAt(x, y) == '#';

    /// <summary>
    /// Replaces the full wall tiles with thin bars: a small square in the middle of
    /// every wall cell, joined to each neighbouring wall cell by a bar.
    /// </summary>
    void BuildThinWalls()
    {
        Transform old = transform.Find(WallVisualsName);
        if (old != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.DestroyObjectImmediate(old.gameObject);
            else
#endif
                Destroy(old.gameObject);
        }

        var wallRenderer = wallsTilemap.GetComponent<TilemapRenderer>();
        bool drawThin = thinWalls && wallLineSprite != null;
        if (thinWalls && wallLineSprite == null)
            Debug.LogWarning("MapBuilder: assign Wall Line Sprite to draw thin walls. Showing full wall tiles instead.", this);
        if (wallRenderer != null) wallRenderer.enabled = !drawThin;
        if (!drawThin) return;

        var root = new GameObject(WallVisualsName).transform;
        root.SetParent(transform, false);
#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Thin Walls");
#endif

        float t = wallThickness;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!IsWallCell(x, y)) continue;
                Vector3 here = DocToWorld(new Vector2(x, y));
                AddBar(root, here, new Vector2(t, t));
                if (IsWallCell(x + 1, y))   // joined to the right
                    AddBar(root, DocToWorld(new Vector2(x + 0.5f, y)), new Vector2(1f + t, t));
                if (IsWallCell(x, y + 1))   // joined downward (document y grows down)
                    AddBar(root, DocToWorld(new Vector2(x, y + 0.5f)), new Vector2(t, 1f + t));
            }
        }
    }

    void AddBar(Transform parent, Vector3 worldPos, Vector2 size)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;

        Vector2 spriteSize = wallLineSprite.bounds.size;
        go.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = wallLineSprite;
        sr.color = wallColor;
        sr.sortingOrder = 1;
    }
}
