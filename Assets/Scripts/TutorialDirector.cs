using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Walks the player through the tutorial one rule at a time (level design, level 0).
/// Each step shows a message, may flash a cell on the map, unlocks the buttons it
/// needs, and moves on as soon as the player's plan does what the step asks.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    public PlanningUI ui;
    public LevelSpawner spawner;
    public RunClock clock;
    public MapBuilder map;

    class Step
    {
        public string text;
        public Func<Vector2Int?> highlight;
        public Func<bool> done;        // null = wait for the Continue button
        public Action onEnter;
    }

    readonly List<Step> steps = new List<Step>();
    int index = -1;
    bool runSucceeded;
    SpriteRenderer marker;

    Character Hacker => spawner.Characters.Find(c => c.Role == CharacterRole.Hacker);
    Character Sneak => spawner.Characters.Find(c => c.Role == CharacterRole.Sneak);

    IEnumerator Start()
    {
        while (ui == null || !ui.Ready) yield return null; // wait until the planning screen exists

        // The tutorial starts with empty plans and locked buttons.
        foreach (Character c in spawner.Characters) c.Plan.Clear();
        ui.allowCameraOff = false;
        ui.allowPickup = false;
        ui.allowRun = false;
        ui.PlanChanged();

        clock.OnRunEnded += (success, reason) => { if (success) runSucceeded = true; };
        CreateMarker();
        BuildSteps();
        Next();
    }

    void BuildSteps()
    {
        Vector2Int diamond = spawner.FindCell('$');
        Vector2Int van = spawner.FindCell('X');

        steps.Add(new Step
        {
            text = "Welcome to your first heist! Every crew member has different skills.\n" +
                   "This time you plan for the Hacker and the Sneak.",
        });
        steps.Add(new Step
        {
            text = "Select the Sneak (bottom left), then tap the flashing cell to walk there.",
            highlight = () => new Vector2Int(3, 3),
            done = () => HasMoveTo(Sneak, new Vector2Int(3, 3)),
        });
        steps.Add(new Step
        {
            text = "The camera sees everything in its red circle. Select the Hacker,\n" +
                   "tap 'Wait +1s' three times, then 'Camera off' (it stays off for 8 seconds).",
            onEnter = () => ui.allowCameraOff = true,
            done = () => Has<DisableCameraCommand>(Hacker),
        });
        steps.Add(new Step
        {
            text = "The guard walks up and down. Drag along the timeline to see where he will be.\n" +
                   "Select the Sneak and add a wait (about 6 s) so he crosses behind the guard's back.",
            done = () => Has<WaitCommand>(Sneak),
        });
        steps.Add(new Step
        {
            text = "Now walk the Sneak through the door and onto the diamond.\n" +
                   "Tip: the camera is only off between seconds 3 and 11.",
            highlight = () => diamond,
            done = () => HasMoveTo(Sneak, diamond),
        });
        steps.Add(new Step
        {
            text = "Tap 'Pick up' to take the diamond.",
            onEnter = () => ui.allowPickup = true,
            done = () => Has<PickupCommand>(Sneak),
        });
        steps.Add(new Step
        {
            text = "Everyone must escape: send BOTH the Hacker and the Sneak to the getaway van.\n" +
                   "Hint: keep the Sneak 3 cells away from the guard's path (the right-hand column).",
            highlight = () => van,
            done = () => EndsAt(Hacker, van) && EndsAt(Sneak, van),
        });
        steps.Add(new Step
        {
            text = "Your plan is ready. Tap RUN and watch.\n" +
                   "If someone is seen, you come back here with your plan kept. Stuck? Tap 'Show me'.",
            onEnter = () => { ui.allowRun = true; runSucceeded = false; },
            done = () => runSucceeded,
        });
    }

    void Next()
    {
        index++;
        if (index >= steps.Count)
        {
            ui.SetMessage("Tutorial complete! You pulled off your first heist.");
            ui.OnContinue = null;
            marker.enabled = false;
            return;
        }

        Step s = steps[index];
        ui.SetMessage(s.text);
        ui.OnContinue = s.done == null ? (Action)Next : null;
        s.onEnter?.Invoke();
    }

    void Update()
    {
        if (index < 0 || index >= steps.Count) return;
        Step s = steps[index];

        Vector2Int? cell = s.highlight?.Invoke();
        marker.enabled = cell.HasValue && clock.State == RunState.Planning;
        if (cell.HasValue)
        {
            marker.transform.position = map.DocToWorld(cell.Value);
            float pulse = 0.25f + 0.25f * Mathf.Sin(Time.time * 5f);
            marker.color = new Color(1f, 0.9f, 0.2f, pulse);
        }

        if (s.done != null && s.done()) Next();
    }

    void CreateMarker()
    {
        var go = new GameObject("Tutorial Marker");
        marker = go.AddComponent<SpriteRenderer>();
        marker.sprite = map.wallLineSprite; // any plain white square
        marker.sortingOrder = 2;
        Vector2 size = marker.sprite != null ? (Vector2)marker.sprite.bounds.size : Vector2.one;
        go.transform.localScale = new Vector3(0.9f / size.x, 0.9f / size.y, 1f);
        marker.enabled = false;
    }

    static bool HasMoveTo(Character c, Vector2Int cell) =>
        c != null && c.Plan.Exists(cmd => cmd is MoveCommand m && m.Target == cell);

    static bool Has<T>(Character c) where T : Command =>
        c != null && c.Plan.Exists(cmd => cmd is T);

    static bool EndsAt(Character c, Vector2Int cell) =>
        c != null && c.Plan.Count > 0 && c.Plan[c.Plan.Count - 1] is MoveCommand m && m.Target == cell;
}
