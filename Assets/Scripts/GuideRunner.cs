using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One tutorial step: what to say, where to point, and when it's done.</summary>
public class GuideStep
{
    public string text;
    /// <summary>Screen area to highlight. Null = an explanation with a button instead.</summary>
    public Func<Rect> target;
    /// <summary>Checked every frame; the step ends when it returns true.</summary>
    public Func<bool> done;
    public string buttonLabel = "Got it";
    public Action onEnter;

    public static GuideStep Explain(string text, string button = "Got it", Action onEnter = null) =>
        new GuideStep { text = text, buttonLabel = button, onEnter = onEnter };

    public static GuideStep Tap(string text, Func<Rect> target, Func<bool> done, Action onEnter = null) =>
        new GuideStep { text = text, target = target, done = done, onEnter = onEnter };

    /// <summary>Highlight something and explain it; continues on the button.</summary>
    public static GuideStep Show(string text, Func<Rect> target, string button = "Got it", Action onEnter = null) =>
        new GuideStep { text = text, target = target, buttonLabel = button, onEnter = onEnter };
}

/// <summary>Runs a list of guide steps on a GuideOverlay, one after another.</summary>
public class GuideRunner : MonoBehaviour
{
    GuideOverlay overlay;
    List<GuideStep> steps;
    int index = -1;
    Action onFinished;

    public bool Running => steps != null && index >= 0 && index < steps.Count;

    public static GuideRunner Run(List<GuideStep> steps, Action onFinished = null)
    {
        var runner = new GameObject("Guide").AddComponent<GuideRunner>();
        runner.overlay = GuideOverlay.Create();
        runner.steps = steps;
        runner.onFinished = onFinished;
        runner.Next();
        return runner;
    }

    void Next()
    {
        index++;
        if (index >= steps.Count)
        {
            overlay.Hide();
            onFinished?.Invoke();
            return;
        }

        GuideStep s = steps[index];
        s.onEnter?.Invoke();
        if (s.target != null && s.done == null) overlay.Show(s.target, s.text, s.buttonLabel, Next);
        else if (s.target != null) overlay.PointAt(s.target, s.text);
        else overlay.Explain(s.text, s.buttonLabel, Next);
    }

    void Update()
    {
        if (!Running) return;
        GuideStep s = steps[index];
        if (s.done != null && s.done()) Next();
    }

    void OnDestroy()
    {
        if (overlay != null) Destroy(overlay.gameObject);
    }
}
