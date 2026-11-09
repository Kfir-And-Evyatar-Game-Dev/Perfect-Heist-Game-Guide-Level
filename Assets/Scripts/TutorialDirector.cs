using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The planning tutorial, taught one tiny step at a time: every step highlights
/// exactly one thing (a character, a button, a map cell, the camera...) and only
/// that thing can be tapped. Built on GuideRunner / GuideOverlay.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    public PlanningUI ui;
    public LevelSpawner spawner;
    public RunClock clock;
    public MapBuilder map;

    Character H => spawner.Characters.Find(c => c.Role == CharacterRole.Hacker);
    Character S => spawner.Characters.Find(c => c.Role == CharacterRole.Sneak);

    IEnumerator Start()
    {
        if (ui == null) ui = FindAnyObjectByType<PlanningUI>();
        while (ui == null || !ui.Ready) yield return null; // wait for the planning screen

        // Use the planning screen's references for anything that wasn't dragged in.
        if (spawner == null) spawner = ui.spawner;
        if (clock == null) clock = ui.clock;
        if (map == null) map = ui.map;

        if (H == null || S == null || spawner.Cameras.Count == 0)
        {
            Debug.LogWarning("Tutorial needs a Hacker, a Sneak and a camera.");
            yield break;
        }

        foreach (Character c in spawner.Characters) c.Plan.Clear();
        ui.allowRun = false;
        ui.ClosePanel();
        ui.PlanChanged();

        GuideRunner.Run(BuildSteps());
    }

    List<GuideStep> BuildSteps()
    {
        Vector2Int diamond = spawner.FindCell('$');
        Vector2Int van = spawner.FindCell('X');
        Vector2Int room = new Vector2Int(3, 3);
        Vector2Int door = new Vector2Int(7, 2);
        Vector2Int rightTop = new Vector2Int(9, 1);
        Vector2Int rightBottom = new Vector2Int(9, 4);
        SecurityCamera cam = spawner.Cameras[0];

        return new List<GuideStep>
        {
            // --- Getting to know the map
            GuideStep.Explain("This is the heist map. Your mission: steal the diamond\nand escape in the getaway van!", "Show me"),
            GuideStep.Show("This is the diamond. That's what we came for!", () => Cell(diamond)),
            GuideStep.Show("This is the getaway van. At the end, every crew member must get in.",
                           () => World(spawner.Vans[0].transform.position, new Vector2(1.7f, 1f))),
            GuideStep.Show("This is a guard. He walks up and down the blue dotted line.\nIf he sees one of your crew, the heist fails!", GuardRoute),
            GuideStep.Show("This is a security camera. It sees everyone inside the red circle.",
                           () => World(cam.transform.position, Vector2.one * (cam.Radius * 2f + 0.4f))),
            GuideStep.Show("This is your crew: the Hacker and the Sneak.\nYou will give each of them a plan.", Crew),

            // --- The Hacker
            GuideStep.Tap("Tap the Hacker to plan the Hacker's moves.", () => Char(H), () => Selected(H)),
            GuideStep.Show("This is the Hacker's timeline. Every action you add appears here,\nfrom left to right. The numbers are seconds.",
                           () => UIKit.ScreenRect(ui.TimelineRect)),
            GuideStep.Show("These are the Hacker's actions. 'Camera off' is the Hacker's\nspecial skill - only the Hacker has it.",
                           () => UIKit.ScreenRect(ui.ActionBarRect)),
            GuideStep.Explain("Our plan: the Sneak needs about 3 seconds to reach the first room.\n" +
                              "Then the Hacker switches the camera off, so the Sneak can pass.", "OK"),
            GuideStep.Tap("First the Hacker waits. Tap 'Wait +1s'.", () => Btn("wait1"), () => LastWait(H, 1)),
            GuideStep.Tap("Tap 'Wait +1s' again. (2 seconds)", () => Btn("wait1"), () => LastWait(H, 2)),
            GuideStep.Tap("And once more. (3 seconds)", () => Btn("wait1"), () => LastWait(H, 3)),
            GuideStep.Show("Look! A 3-second wait is now on the Hacker's timeline.", () => ui.LastBlockScreenRect(H)),
            GuideStep.Tap("Now tap 'Camera off'.", () => Btn("camera"), () => ui.Mode == PlanMode.ChooseCamera),
            GuideStep.Tap("Tap the security camera to switch it off.",
                          () => World(cam.transform.position, Vector2.one), () => LastIs<DisableCameraCommand>(H)),
            GuideStep.Show("At second 3 the camera switches off, for 8 seconds.", () => ui.LastBlockScreenRect(H)),
            GuideStep.Tap("Now the Hacker escapes to the van. Tap 'Walk'.", () => Btn("walk"), () => ui.Mode == PlanMode.ChooseCell),
            GuideStep.Tap("Tap the glowing spot by the van.", () => Cell(van), () => LastMoveTo(H, van)),
            GuideStep.Tap("The Hacker's plan is ready! Tap 'Done'.", () => UIKit.ScreenRect(ui.DoneButtonRect), () => !ui.PanelOpen),

            // --- The Sneak
            GuideStep.Tap("Now tap the Sneak.", () => Char(S), () => Selected(S)),
            GuideStep.Tap("The Sneak walks into the first room. Tap 'Walk'.", () => Btn("walk"), () => ui.Mode == PlanMode.ChooseCell),
            GuideStep.Tap("Tap the glowing spot.", () => Cell(room), () => LastMoveTo(S, room)),
            GuideStep.Show("Careful! The guard is walking toward the door. If the Sneak goes now,\n" +
                           "the guard will see him. We wait until the guard turns his back.", GuardRoute),
            GuideStep.Tap("Tap 'Wait +5s'.", () => Btn("wait5"), () => LastWait(S, 5)),
            GuideStep.Tap("Tap 'Wait +1s'. Now the Sneak waits 6 seconds.", () => Btn("wait1"), () => LastWait(S, 6)),
            GuideStep.Show("Look at the timeline: the Hacker's row is still here.\n" +
                           "Use it to line up your timing - the camera is off from second 3 to 11.",
                           () => UIKit.ScreenRect(ui.TimelineRect)),
            GuideStep.Tap("Tap 'Walk'.", () => Btn("walk"), () => ui.Mode == PlanMode.ChooseCell),
            GuideStep.Tap("Walk through the door to here. The camera is off\nand the guard's back is turned!",
                          () => Cell(door), () => LastMoveTo(S, door)),
            GuideStep.Tap("Tap 'Walk' again.", () => Btn("walk"), () => ui.Mode == PlanMode.ChooseCell),
            GuideStep.Tap("Tap the diamond.", () => Cell(diamond), () => LastMoveTo(S, diamond)),
            GuideStep.Tap("Tap 'Pick up' to grab the diamond.", () => Btn("pickup"), () => LastIs<PickupCommand>(S)),
            GuideStep.Explain("Got it! Now the escape. The guard turns around soon, so the Sneak\n" +
                              "runs down the far right side, where the guard can't see.", "OK"),
            GuideStep.Tap("Tap 'Walk'.", () => Btn("walk"), () => ui.Mode == PlanMode.ChooseCell),
            GuideStep.Tap("Tap the corner on the right.", () => Cell(rightTop), () => LastMoveTo(S, rightTop)),
            GuideStep.Tap("Tap 'Walk'.", () => Btn("walk"), () => ui.Mode == PlanMode.ChooseCell),
            GuideStep.Tap("Go down the right side.", () => Cell(rightBottom), () => LastMoveTo(S, rightBottom)),
            GuideStep.Tap("Tap 'Walk'.", () => Btn("walk"), () => ui.Mode == PlanMode.ChooseCell),
            GuideStep.Tap("And into the van!", () => Cell(van), () => LastMoveTo(S, van)),
            GuideStep.Tap("The Sneak's plan is ready! Tap 'Done'.", () => UIKit.ScreenRect(ui.DoneButtonRect), () => !ui.PanelOpen),

            // --- Run it
            GuideStep.Show("Your plan is ready! The colored lines show where each one will walk.", MapRect),
            GuideStep.Tap("Tap RUN and watch your plan come to life!", () => UIKit.ScreenRect(ui.RunButtonRect),
                          () => clock.State == RunState.Running, () => ui.allowRun = true),
        };
    }

    // ------------------------------------------------------------------ where to point

    Rect Cell(Vector2Int c) => World(map.DocToWorld(c), Vector2.one);
    Rect Char(Character c) => World(c.transform.position, Vector2.one * 0.95f);
    Rect World(Vector3 center, Vector2 size) => UIKit.ScreenRect(center, size);

    Rect Btn(string id)
    {
        RectTransform rt = ui.ActionButton(id);
        return rt != null ? UIKit.ScreenRect(rt) : Rect.zero;
    }

    Rect GuardRoute()
    {
        Guard g = spawner.Guards[0];
        return Union(Cell(g.PatrolStart), Cell(g.PatrolEnd));
    }

    Rect Crew()
    {
        Rect r = Char(spawner.Characters[0]);
        foreach (Character c in spawner.Characters) r = Union(r, Char(c));
        return r;
    }

    Rect MapRect() => Union(Cell(Vector2Int.zero), Cell(new Vector2Int(map.Width - 1, map.Height - 1)));

    static Rect Union(Rect a, Rect b) =>
        Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    // ------------------------------------------------------------------ checks

    bool Selected(Character c) => ui.PanelOpen && ui.Selected == c;

    static bool LastWait(Character c, float seconds) =>
        c.Plan.Count > 0 && c.Plan[c.Plan.Count - 1] is WaitCommand w && w.Seconds >= seconds - 0.01f;

    static bool LastMoveTo(Character c, Vector2Int cell) =>
        c.Plan.Count > 0 && c.Plan[c.Plan.Count - 1] is MoveCommand m && m.Target == cell;

    static bool LastIs<T>(Character c) where T : Command =>
        c.Plan.Count > 0 && c.Plan[c.Plan.Count - 1] is T;
}
