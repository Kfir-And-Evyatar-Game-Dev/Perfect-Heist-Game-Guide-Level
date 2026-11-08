using UnityEngine;

/// <summary>
/// Keeps the whole map on screen on any screen shape (phones, tablets, PC):
/// centres the orthographic camera on the map and zooms so it fits both
/// the width and the height, with a margin. Put it on the Main Camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    public MapBuilder map;

    [Tooltip("Empty space around the map, in cells.")]
    public float margin = 0.5f;

    [Tooltip("Extra space below the map, in cells (room for the timeline UI later).")]
    public float bottomSpace = 0f;

    Camera cam;
    float lastAspect = -1f;

    void Awake() => cam = GetComponent<Camera>();

    void LateUpdate()
    {
        if (map == null || Mathf.Approximately(cam.aspect, lastAspect)) return;
        lastAspect = cam.aspect;
        Fit();
    }

    public void Fit()
    {
        float width = map.Width + margin * 2f;
        float height = map.Height + margin * 2f + bottomSpace;

        // Orthographic size is half the visible height; the visible width is that times the aspect.
        cam.orthographicSize = Mathf.Max(height / 2f, width / 2f / cam.aspect);

        Vector3 p = transform.position;
        transform.position = new Vector3(map.Width / 2f, map.Height / 2f - bottomSpace / 2f, p.z);
    }
}
