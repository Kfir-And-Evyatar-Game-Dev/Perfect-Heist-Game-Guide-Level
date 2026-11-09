using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RunState { Planning, Running, Ended }

/// <summary>
/// The run clock: switches between planning and running, advances run time in
/// fixed steps (so every run of the same plan is identical), and decides
/// success or failure.
/// </summary>
public class RunClock : MonoBehaviour
{
    public static RunClock Instance { get; private set; }

    [Tooltip("Tutorial: a failed run never disqualifies, it returns to planning with the plan kept.")]
    public bool tutorialMode = true;

    [Tooltip("Real seconds to show what went wrong before returning to planning.")]
    public float failPause = 2f;

    public RunState State { get; private set; } = RunState.Planning;

    /// <summary>Seconds since "Run" was pressed.</summary>
    public float RunTime { get; private set; }

    /// <summary>The time shown while planning (for a future timeline scrubber).</summary>
    public float PreviewTime { get; set; }

    public float CurrentTime => State == RunState.Planning ? PreviewTime : RunTime;

    public event Action OnRunStarted;
    /// <summary>(success, reason)</summary>
    public event Action<bool, string> OnRunEnded;

    readonly List<Character> characters = new List<Character>();
    readonly List<Guard> guards = new List<Guard>();
    readonly List<SecurityCamera> cameras = new List<SecurityCamera>();
    readonly List<Diamond> diamonds = new List<Diamond>();
    readonly List<ExitZone> vans = new List<ExitZone>();

    void Awake() => Instance = this;

    public void SetActors(IEnumerable<Character> chars, IEnumerable<Guard> gs,
                          IEnumerable<SecurityCamera> cams, IEnumerable<Diamond> ds,
                          IEnumerable<ExitZone> vs)
    {
        characters.Clear(); characters.AddRange(chars);
        guards.Clear(); guards.AddRange(gs);
        cameras.Clear(); cameras.AddRange(cams);
        diamonds.Clear(); diamonds.AddRange(ds);
        vans.Clear(); vans.AddRange(vs);
    }

    void Update()
    {
        // Temporary keyboard controls until the planning UI exists.
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame && State == RunState.Planning) StartRun();
            if (kb.rKey.wasPressedThisFrame) ResetToPlanning();
        }

        if (State == RunState.Planning)
        {
            foreach (Guard g in guards) g.ShowAt(PreviewTime);
            foreach (Character c in characters) c.ShowAt(PreviewTime);
        }
    }

    void FixedUpdate()
    {
        if (State != RunState.Running) return;

        RunTime += Time.fixedDeltaTime;

        foreach (Guard g in guards) g.ShowAt(RunTime);
        foreach (Character c in characters) c.Simulate(RunTime);
        foreach (ExitZone v in vans) v.Simulate(RunTime);

        foreach (Guard g in guards)
            foreach (Character c in characters)
                if (!c.Boarded && g.CanSee(c.DocPos))
                {
                    ReportDetected(c, "the guard");
                    return;
                }

        CheckFinished();
    }

    public void StartRun()
    {
        foreach (Character c in characters)
            if (!c.PreparePlan()) return;

        RestoreActors();
        RunTime = 0f;
        State = RunState.Running;
        Debug.Log("Run started.");
        OnRunStarted?.Invoke();
    }

    /// <summary>Called by guards and cameras when a character is seen.</summary>
    public void ReportDetected(Character c, string seenBy)
    {
        if (State != RunState.Running) return;
        c.MarkDetected();
        End(false, $"{c.DisplayName} was seen by {seenBy} at second {RunTime:0.0}.");
    }

    void CheckFinished()
    {
        bool haveDiamond = characters.Exists(c => c.HasDiamond);
        bool allBoarded = characters.TrueForAll(c => c.Boarded);

        // Everyone is in the van with the diamond: drive away, and win once it's off the map.
        if (allBoarded && haveDiamond && vans.Count > 0)
        {
            ExitZone van = vans[0];
            van.StartDriving(RunTime);
            if (van.HasLeftMap) End(true, $"Heist complete in {RunTime:0.0} seconds.");
            return;
        }

        // Otherwise wait until every plan is over (plus time to climb in and for the trigger to register).
        const float grace = 0.25f;
        foreach (Character c in characters)
            if (RunTime < c.PlanEndTime + Character.BoardTime + grace) return;

        if (!haveDiamond) End(false, "The plan ended without the diamond.");
        else End(false, "The plan ended before everyone got into the getaway van.");
    }

    void End(bool success, string reason)
    {
        State = RunState.Ended;
        Debug.Log((success ? "SUCCESS: " : "FAILED: ") + reason);
        OnRunEnded?.Invoke(success, reason);

        if (!success && tutorialMode) StartCoroutine(ReturnToPlanningSoon());
    }

    IEnumerator ReturnToPlanningSoon()
    {
        yield return new WaitForSecondsRealtime(failPause);
        if (State == RunState.Ended) ResetToPlanning();
    }

    /// <summary>Back to planning at second 0. The plan itself is kept.</summary>
    public void ResetToPlanning()
    {
        StopAllCoroutines();
        State = RunState.Planning;
        RunTime = 0f;
        PreviewTime = 0f;
        RestoreActors();
    }

    void RestoreActors()
    {
        foreach (Diamond d in diamonds) d.ResetState();
        foreach (ExitZone v in vans) v.ResetState();
        foreach (SecurityCamera cam in cameras) cam.ResetState();
        foreach (Guard g in guards) g.ShowAt(0f);
        foreach (Character c in characters) c.ResetState();
    }
}
