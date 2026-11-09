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

    [Tooltip("Extra space below the map, in cells (room for the planning panel).")]
    public float bottomSpace = 0f;

    [Tooltip("Extra space above the map, in cells (room for the message bar).")]
    public float topSpace = 0f;

    Camera cam;
    float lastAspect = -1f;

    void Awake() => cam = GetComponent<Camera>();

    void LateUpdate()
    {
        if (map == null || Mathf.Approximately(cam.aspect, lastAspect)) return;
        lastAspect = cam.aspect;
        Fit();
    }

    /// <summary>Change the reserved space (e.g. from the UI) and refit right away.</summary>
    public void SetSpace(float top, float bottom)
    {
        topSpace = top;
        bottomSpace = bottom;
        lastAspect = -1f;
    }

    public void Fit()
    {
        if (cam == null) cam = GetComponent<Camera>();
        float width = map.Width + margin * 2f;
        float height = map.Height + margin * 2f + bottomSpace + topSpace;

        // Orthographic size is half the visible height; the visible width is that times the aspect.
        cam.orthographicSize = Mathf.Max(height / 2f, width / 2f / cam.aspect);

        Vector3 p = transform.position;
        float centerY = map.Height / 2f - bottomSpace / 2f + topSpace / 2f;
        transform.position = new Vector3(map.Width / 2f, centerY, p.z);
    }
}
