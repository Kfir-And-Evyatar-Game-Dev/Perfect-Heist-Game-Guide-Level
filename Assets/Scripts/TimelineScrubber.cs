using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Press or drag on the timeline to preview the plan at that second.</summary>
public class TimelineScrubber : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public PlanningUI ui;

    public void OnPointerDown(PointerEventData e) => ui.ScrubTo(e.position);
    public void OnDrag(PointerEventData e) => ui.ScrubTo(e.position);
}
