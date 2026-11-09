using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Full-screen invisible layer of the tutorial guide. It swallows every tap
/// except taps inside the highlighted hole, which pass through to the real button.
/// </summary>
public class GuideBlocker : MonoBehaviour, ICanvasRaycastFilter, IPointerClickHandler
{
    public GuideOverlay overlay;

    /// <summary>Unity asks this for every tap: false = "let the tap through".</summary>
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (overlay == null || !overlay.Blocking) return false;
        return !(overlay.HasHole && overlay.PassThrough && overlay.HoleScreenRect.Contains(screenPoint));
    }

    public void OnPointerClick(PointerEventData e) => overlay.WrongTap();
}
