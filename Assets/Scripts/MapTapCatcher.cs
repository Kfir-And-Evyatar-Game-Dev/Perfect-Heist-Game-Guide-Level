using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A transparent full-screen UI layer behind all panels. It receives only the
/// taps/clicks that didn't hit a button or panel, i.e. taps on the map.
/// Works the same for mouse and touch.
/// </summary>
public class MapTapCatcher : MonoBehaviour, IPointerClickHandler
{
    public PlanningUI ui;

    public void OnPointerClick(PointerEventData e)
    {
        if (ui != null) ui.MapTap(e.position);
    }
}
