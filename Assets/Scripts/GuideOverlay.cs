using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tutorial "teacher": darkens the screen except one spot, draws a pulsing
/// halo and a bouncing arrow around it, shows a speech bubble, and only lets
/// taps reach that spot. Can also show an explanation with a "Got it" button.
/// </summary>
public class GuideOverlay : MonoBehaviour
{
    const float Padding = 12f;
    const float HaloWidth = 7f;

    RectTransform root;
    readonly Image[] dims = new Image[4];
    readonly Image[] halo = new Image[4];
    Text arrow;
    RectTransform bubble;
    Text bubbleText, hintText;
    Button bubbleButton;
    Action onBubbleButton;
    Func<Rect> holeSource;
    float hintUntil;

    public bool Blocking { get; private set; }
    public bool HasHole => holeSource != null;
    /// <summary>True when taps inside the hole reach the element underneath.</summary>
    public bool PassThrough { get; private set; }
    public Rect HoleScreenRect { get; private set; }

    public static GuideOverlay Create()
    {
        Canvas canvas = UIKit.CreateCanvas("Guide Overlay", 100); // above every other screen
        var overlay = canvas.gameObject.AddComponent<GuideOverlay>();
        overlay.Build(canvas.transform);
        overlay.Hide();
        return overlay;
    }

    void Build(Transform canvas)
    {
        Image blockerImage = UIKit.NewImage("Blocker", canvas, new Color(0, 0, 0, 0));
        root = blockerImage.rectTransform;
        UIKit.Stretch(root);
        root.gameObject.AddComponent<GuideBlocker>().overlay = this;

        for (int i = 0; i < 4; i++)
        {
            dims[i] = UIKit.NewImage("Dim", root, new Color(0, 0, 0, 0.6f));
            dims[i].raycastTarget = false;
            Pivot0(dims[i].rectTransform);
        }
        for (int i = 0; i < 4; i++)
        {
            halo[i] = UIKit.NewImage("Halo", root, UIKit.Gold);
            halo[i].raycastTarget = false;
            Pivot0(halo[i].rectTransform);
        }

        arrow = UIKit.NewText(root, "▼", 80, TextAnchor.MiddleCenter, UIKit.Gold, FontStyle.Bold);
        Pivot0(arrow.rectTransform);
        arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.sizeDelta = new Vector2(100, 100);

        Image b = UIKit.NewImage("Bubble", root, new Color(0.98f, 0.97f, 0.92f));
        bubble = b.rectTransform;
        bubbleText = UIKit.NewText(bubble, "", 38, TextAnchor.MiddleLeft, new Color(0.1f, 0.1f, 0.12f));
        UIKit.Stretch(bubbleText.rectTransform, 36, 20, 36, 20);
        hintText = UIKit.NewText(bubble, "", 30, TextAnchor.UpperCenter, new Color(1f, 0.55f, 0.4f), FontStyle.Bold);
        UIKit.Place(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(1000, 40));

        bubbleButton = UIKit.NewButton(bubble, "Got it", () => onBubbleButton?.Invoke(), UIKit.Green);
        UIKit.Place((RectTransform)bubbleButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24, 20), new Vector2(240, 80));
    }

    static void Pivot0(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = Vector2.zero;
    }

    // ------------------------------------------------------------------ public API

    /// <summary>Point at a UI element. Only taps on it get through.</summary>
    public void PointAt(RectTransform target, string message) =>
        PointAt(() => UIKit.ScreenRect(target), message);

    /// <summary>Point at any screen rectangle (e.g. a character on the map). Only taps inside it get through.</summary>
    public void PointAt(Func<Rect> screenRect, string message)
    {
        gameObject.SetActive(true);
        Blocking = true;
        PassThrough = true;
        holeSource = screenRect;
        onBubbleButton = null;
        bubbleButton.gameObject.SetActive(false);
        bubbleText.text = message;
        hintText.text = "";
    }

    /// <summary>Highlight something to explain it, with a button. The highlighted thing itself can't be tapped.</summary>
    public void Show(Func<Rect> screenRect, string message, string buttonLabel, Action onButton)
    {
        Explain(message, buttonLabel, onButton);
        holeSource = screenRect;
        PassThrough = false;
    }

    /// <summary>An explanation with a button. Everything else is blocked until it's pressed.</summary>
    public void Explain(string message, string buttonLabel, Action onButton)
    {
        gameObject.SetActive(true);
        Blocking = true;
        PassThrough = false;
        holeSource = null;
        onBubbleButton = onButton;
        bubbleButton.gameObject.SetActive(true);
        UIKit.SetLabel(bubbleButton, buttonLabel);
        bubbleText.text = message;
        hintText.text = "";
    }

    public void Hide()
    {
        Blocking = false;
        holeSource = null;
        gameObject.SetActive(false);
    }

    public void WrongTap()
    {
        hintText.text = "Not there - tap the glowing spot!";
        hintUntil = Time.unscaledTime + 1.8f;
    }

    // ------------------------------------------------------------------ layout every frame

    void LateUpdate()
    {
        if (!Blocking) return;

        Rect r = root.rect;
        Rect hole;
        if (holeSource != null)
        {
            HoleScreenRect = holeSource();
            hole = ToLocal(HoleScreenRect);
            hole = Rect.MinMaxRect(hole.xMin - Padding, hole.yMin - Padding, hole.xMax + Padding, hole.yMax + Padding);
        }
        else
        {
            hole = new Rect(r.center, Vector2.zero); // no hole: dim everything
        }

        // Four dark panels around the hole.
        SetRect(dims[0], r.xMin, r.yMin, r.width, Mathf.Max(0, hole.yMin - r.yMin));          // below
        SetRect(dims[1], r.xMin, hole.yMax, r.width, Mathf.Max(0, r.yMax - hole.yMax));        // above
        SetRect(dims[2], r.xMin, hole.yMin, Mathf.Max(0, hole.xMin - r.xMin), hole.height);    // left
        SetRect(dims[3], hole.xMax, hole.yMin, Mathf.Max(0, r.xMax - hole.xMax), hole.height); // right

        // Pulsing halo and bouncing arrow.
        bool show = holeSource != null;
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
        for (int i = 0; i < 4; i++)
        {
            halo[i].enabled = show;
            halo[i].color = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, pulse);
        }
        if (show)
        {
            float w = HaloWidth;
            SetRect(halo[0], hole.xMin - w, hole.yMin - w, hole.width + 2 * w, w);
            SetRect(halo[1], hole.xMin - w, hole.yMax, hole.width + 2 * w, w);
            SetRect(halo[2], hole.xMin - w, hole.yMin, w, hole.height);
            SetRect(halo[3], hole.xMax, hole.yMin, w, hole.height);
        }

        arrow.enabled = show;
        bool targetLow = hole.center.y < r.center.y;
        if (show)
        {
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 18f;
            bool arrowBelow = hole.yMax + 120 > r.yMax; // no room above: point up from below
            arrow.text = arrowBelow ? "▲" : "▼";
            float y = arrowBelow ? hole.yMin - 55 - bob : hole.yMax + 55 + bob;
            arrow.rectTransform.anchoredPosition = new Vector2(hole.center.x, y);
        }

        // Speech bubble on the half of the screen away from the target.
        float bubbleH = 190f;
        bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
        bubble.pivot = new Vector2(0.5f, 0.5f);
        bubble.sizeDelta = new Vector2(1250, bubbleH);
        float by = show ? (targetLow ? r.yMax - bubbleH / 2 - 40 : r.yMin + bubbleH / 2 + 60) : r.center.y;
        bubble.anchoredPosition = new Vector2(r.center.x, by);
        bubbleText.rectTransform.offsetMax = new Vector2(bubbleButton.gameObject.activeSelf ? -290 : -36, -20);

        if (hintText.text.Length > 0 && Time.unscaledTime > hintUntil) hintText.text = "";
    }

    Rect ToLocal(Rect screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen.min, null, out Vector2 a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen.max, null, out Vector2 b);
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }

    static void SetRect(Image img, float x, float y, float w, float h)
    {
        img.rectTransform.anchoredPosition = new Vector2(x, y);
        img.rectTransform.sizeDelta = new Vector2(w, h);
    }
}
