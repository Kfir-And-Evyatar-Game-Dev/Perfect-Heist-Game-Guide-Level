using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Small helpers for building Unity UI from code, shared by all screens.</summary>
public static class UIKit
{
    public static readonly Color Background = new Color(0.06f, 0.07f, 0.10f);
    public static readonly Color Panel = new Color(0.10f, 0.11f, 0.15f, 0.96f);
    public static readonly Color Card = new Color(0.15f, 0.17f, 0.23f);
    public static readonly Color ButtonColor = new Color(0.22f, 0.25f, 0.32f);
    public static readonly Color Green = new Color(0.16f, 0.56f, 0.27f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.30f);
    public static readonly Color Muted = new Color(0.62f, 0.65f, 0.72f);

    static Font font;
    public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    /// <summary>A screen-space canvas scaled for a 1920x1080 reference (fits phones and PC).</summary>
    public static Canvas CreateCanvas(string name, int sortingOrder = 0)
    {
        EnsureEventSystem();
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image NewImage(string name, Transform parent, Color color)
    {
        var img = NewRect(name, parent).gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static Image NewSprite(string name, Transform parent, Sprite sprite, Color tint)
    {
        Image img = NewImage(name, parent, tint);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    public static Text NewText(Transform parent, string text, int size, TextAnchor anchor, Color color, FontStyle style = FontStyle.Normal)
    {
        RectTransform rt = NewRect("Text", parent);
        Stretch(rt);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = color;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    public static Button NewButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick,
                                   Color? color = null, int fontSize = 32)
    {
        Image img = NewImage(label, parent, color ?? ButtonColor);
        var b = img.gameObject.AddComponent<Button>();
        if (onClick != null) b.onClick.AddListener(onClick);
        NewText(img.transform, label, fontSize, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        return b;
    }

    public static void SetLabel(Button b, string label) => b.GetComponentInChildren<Text>().text = label;

    public static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>Anchor at a point of the parent (0..1), with a pivot, position and size in pixels.</summary>
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    /// <summary>Screen-space rectangle of a UI element (any screen-space-overlay canvas).</summary>
    public static Rect ScreenRect(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    /// <summary>Screen-space rectangle around a point in the game world (for map objects).</summary>
    public static Rect ScreenRect(Vector3 worldCenter, Vector2 worldSize)
    {
        Camera cam = Camera.main;
        Vector3 a = cam.WorldToScreenPoint(worldCenter - (Vector3)(worldSize / 2f));
        Vector3 b = cam.WorldToScreenPoint(worldCenter + (Vector3)(worldSize / 2f));
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
}
