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

    string[] Rows => layout.Replace("\r", "").Split('\n');

    public int Width => Rows[0].Length;
    public int Height => Rows.Length;

    /// <summary>Document coordinates (x right, y down) to a Unity tilemap cell.</summary>
    public Vector3Int DocToCell(int x, int y) => new Vector3Int(x, Height - 1 - y, 0);

    /// <summary>World position of the centre of a document cell.</summary>
    public Vector3 DocToWorld(int x, int y) => floorTilemap.GetCellCenterWorld(DocToCell(x, y));

    /// <summary>The layout character at a document cell, e.g. '$' for the target.</summary>
    public char CharAt(int x, int y) => Rows[y][x];

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
}
