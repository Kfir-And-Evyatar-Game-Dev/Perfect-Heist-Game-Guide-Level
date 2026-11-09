using UnityEngine;

/// <summary>
/// Keeps the whole map visible on any screen (phones, tablets, PC). The UI tells it
/// what fraction of the screen is covered at the top and bottom (bars, the timeline
/// window), and the camera zooms and moves so the map fits exactly in the space left.
/// Changes are smoothed, so opening the timeline window slides the map up.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    [SerializeField] MapBuilder map;

    [Tooltip("Empty space around the map, in cells.")]
    [SerializeField] float margin = 0.4f;

    [Tooltip("How fast the camera follows changes (higher = faster).")]
    [SerializeField] float smoothing = 10f;

    float topFraction, bottomFraction;
    Camera cam;
    bool snapped;

    void Awake() => cam = GetComponent<Camera>();

    /// <summary>Part of the screen height (0..1) hidden by UI at the top and at the bottom.</summary>
    public void SetCovered(float top, float bottom)
    {
        topFraction = Mathf.Clamp01(top);
        bottomFraction = Mathf.Clamp01(bottom);
    }

    void LateUpdate()
    {
        if (map == null) return;

        float free = Mathf.Max(0.2f, 1f - topFraction - bottomFraction);
        float width = map.Width + margin * 2f;
        float height = map.Height + margin * 2f;

        // Orthographic size = half the visible height. The map must fit the free strip
        // in height, and the whole screen in width.
        float size = Mathf.Max(height / (2f * free), width / (2f * cam.aspect));

        // Put the map's centre in the middle of the free strip.
        float freeCenter = bottomFraction + free / 2f;          // 0 = bottom of screen, 1 = top
        float y = map.Height / 2f - (freeCenter - 0.5f) * 2f * size;
        var target = new Vector3(map.Width / 2f, y, transform.position.z);

        if (!snapped)
        {
            cam.orthographicSize = size;
            transform.position = target;
            snapped = true;
            return;
        }

        float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, k);
        transform.position = Vector3.Lerp(transform.position, target, k);
    }
}
