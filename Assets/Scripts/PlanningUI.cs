using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>What the next tap on the map means.</summary>
public enum PlanMode { None, ChooseCell, ChooseCamera }

/// <summary>
/// The planning screen. At first only the map is shown. Tapping a character on the
/// map opens their timeline window at the bottom, with buttons for the actions that
/// character can do. "Walk" and "Camera off" then wait for a tap on a map cell or a
/// camera. Built in code; works with mouse and touch.
/// </summary>
public class PlanningUI : MonoBehaviour
{
    public LevelSpawner spawner;
    public RunClock clock;
    public MapBuilder map;
    public CameraFit cameraFit;

    [Header("Layout (pixels at 1920x1080)")]
    public float topBarHeight = 120f;
    public float panelHeight = 400f;
    public float pixelsPerSecond = 40f;
    public float rowHeight = 62f;

    [Tooltip("The tutorial keeps RUN locked until the plan is ready.")]
    public bool allowRun = true;

    public Character Selected { get; private set; }
    public PlanMode Mode { get; private set; }
    public bool Ready { get; private set; }
    public bool PanelOpen => panel != null && panel.activeSelf;

    // Things the tutorial points at.
    public RectTransform RunButtonRect => (RectTransform)runButton.transform;
    public RectTransform DoneButtonRect => (RectTransform)doneButton.transform;
    public RectTransform TimelineRect => timelineArea;
    public RectTransform ActionBarRect => actionBar;
    public RectTransform ActionButton(string id) =>
        actionButtons.TryGetValue(id, out Button b) ? (RectTransform)b.transform : null;

    Text statusText, panelName, failReason;
    Image panelPortrait;
    GameObject panel, failWindow;
    RectTransform actionBar, timelineArea, playhead;
    Button runButton, doneButton;
    readonly Dictionary<string, Button> actionButtons = new Dictionary<string, Button>();
    readonly Dictionary<Character, LineRenderer> paths = new Dictionary<Character, LineRenderer>();
    readonly List<GameObject> timelineItems = new List<GameObject>();
    readonly List<SpriteRenderer> cellMarkers = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> cameraMarkers = new List<SpriteRenderer>();
    string defaultStatus = "Tap a character on the map to plan their moves.";

    IEnumerator Start()
    {
        yield return null; // let LevelSpawner create everything first

        // Fill any reference that wasn't dragged in, so a missing field can't break the screen.
        if (spawner == null) spawner = FindAnyObjectByType<LevelSpawner>();
        if (map == null) map = spawner != null && spawner.map != null ? spawner.map : FindAnyObjectByType<MapBuilder>();
        if (clock == null) clock = spawner != null && spawner.clock != null ? spawner.clock : FindAnyObjectByType<RunClock>();
        if (cameraFit == null && Camera.main != null) cameraFit = Camera.main.GetComponent<CameraFit>();

        clock.tutorialMode = false; // failures show the "Attempt failed" window instead
        clock.PreviewTime = 0f;     // during planning everyone stands at their start position
        BuildUI();
        foreach (Character c in spawner.Characters) CreatePath(c);
        CreateGuardRoutes();
        CreateCellMarkers();

        clock.OnRunEnded += HandleRunEnded;
        if (cameraFit != null) cameraFit.SetSpace(1.4f, 4.6f);

        ClosePanel();
        PlanChanged();
        Ready = true;
    }

    // ------------------------------------------------------------------ selecting and modes

    public void Select(Character c)
    {
        Selected = c;
        Mode = PlanMode.None;
        panel.SetActive(true);
        panelName.text = c.DisplayName;
        panelPortrait.sprite = c.body != null ? c.body.sprite : null;
        BuildActionButtons(c);
        RefreshAll();
        SetStatus(c.DisplayName + ": add actions with the buttons below.");
    }

    public void ClosePanel()
    {
        Selected = null;
        SetMode(PlanMode.None);
        if (panel != null) panel.SetActive(false);
        RefreshAll();
        SetStatus(defaultStatus);
    }

    void SetMode(PlanMode mode)
    {
        Mode = mode;
        foreach (SpriteRenderer m in cellMarkers) m.enabled = mode == PlanMode.ChooseCell;
        foreach (SpriteRenderer m in cameraMarkers) m.enabled = mode == PlanMode.ChooseCamera;
        if (mode == PlanMode.ChooseCell) SetStatus("Tap a cell on the map to walk there.");
        if (mode == PlanMode.ChooseCamera) SetStatus("Tap the camera you want to switch off.");
    }

    /// <summary>A tap on the map (from MapTapCatcher).</summary>
    public void MapTap(Vector2 screenPos)
    {
        if (!Ready || clock.State != RunState.Planning) return;
        Vector2Int cell = map.WorldToDoc(Camera.main.ScreenToWorldPoint(screenPos));

        if (Mode == PlanMode.ChooseCell)
        {
            if (!map.IsWalkable(cell)) { SetStatus("You can't walk there. Tap a lit cell."); return; }
            SetMode(PlanMode.None);
            AddCommand(new MoveCommand(cell));
            return;
        }

        if (Mode == PlanMode.ChooseCamera)
        {
            SecurityCamera cam = spawner.Cameras.Find(c => Vector2Int.Distance(c.Cell, cell) < 1.1f);
            if (cam == null) { SetStatus("Tap a security camera."); return; }
            SetMode(PlanMode.None);
            AddCommand(new DisableCameraCommand(cam.Cell, spawner.cameraOffSeconds));
            return;
        }

        Character tapped = spawner.Characters.Find(c => Vector2.Distance(c.DocPos, cell) < 0.6f);
        if (tapped != null) Select(tapped);
    }

    // ------------------------------------------------------------------ actions

    void BuildActionButtons(Character c)
    {
        foreach (Button b in actionButtons.Values) Destroy(b.gameObject);
        actionButtons.Clear();

        AddAction("walk", "Walk", () => SetMode(PlanMode.ChooseCell), 170);
        AddAction("wait1", "Wait +1s", () => AddWait(1f), 180);
        AddAction("wait5", "Wait +5s", () => AddWait(5f), 180);
        AddAction("pickup", "Pick up", () => AddCommand(new PickupCommand()), 170);

        // Special skills: only the characters who have them get the button.
        if (c.Role == CharacterRole.Hacker)
            AddAction("camera", "Camera off", () => SetMode(PlanMode.ChooseCamera), 220, new Color(0.65f, 0.2f, 0.2f));

        AddAction("undo", "Undo", Undo, 140);
    }

    void AddAction(string id, string label, UnityEngine.Events.UnityAction onClick, float width, Color? color = null)
    {
        Button b = UIKit.NewButton(actionBar, label, onClick, color, 30);
        var le = b.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = width;
        actionButtons[id] = b;
    }

    void AddWait(float seconds)
    {
        List<Command> plan = Selected.Plan;
        if (plan.Count > 0 && plan[plan.Count - 1] is WaitCommand w)
            plan[plan.Count - 1] = new WaitCommand(w.Seconds + seconds); // one longer wait instead of many short ones
        else
            plan.Add(new WaitCommand(seconds));
        PlanChanged();
    }

    void AddCommand(Command cmd)
    {
        Selected.Plan.Add(cmd);
        if (!Selected.PreparePlan())
        {
            Selected.Plan.RemoveAt(Selected.Plan.Count - 1);
            SetStatus("That isn't possible from there.");
        }
        PlanChanged();
    }

    void Undo()
    {
        if (Selected.Plan.Count > 0) Selected.Plan.RemoveAt(Selected.Plan.Count - 1);
        SetMode(PlanMode.None);
        PlanChanged();
    }

    void Run()
    {
        ClosePanel();
        GameManager.Ensure().RecordAttempt();
        clock.StartRun();
        if (clock.State == RunState.Running) SetStatus("Running... watch your crew!");
    }

    public void PlanChanged()
    {
        foreach (Character c in spawner.Characters) c.PreparePlan();
        RefreshAll();
    }

    void RefreshAll()
    {
        if (timelineArea == null) return;
        RefreshTimeline();
        RefreshPaths();
    }

    /// <summary>Kept for TimelineScrubber; planning no longer previews other seconds, so characters stay put.</summary>
    public void ScrubTo(Vector2 screenPos) { }

    public void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }

    // ------------------------------------------------------------------ run results

    void HandleRunEnded(bool success, string reason)
    {
        if (success)
        {
            SetStatus("Heist complete!");
            StartCoroutine(GoToWin(clock.RunTime));
        }
        else
        {
            failReason.text = reason + "\n\nYour plan is kept. Change it and try again.";
            failWindow.SetActive(true);
        }
    }

    IEnumerator GoToWin(float runTime)
    {
        yield return new WaitForSeconds(1.2f);
        GameManager.Ensure().CompleteLevel(runTime);
    }

    void BackToPlanning()
    {
        failWindow.SetActive(false);
        clock.ResetToPlanning();
        SetStatus(defaultStatus);
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        if (!Ready) return;
        bool planning = clock.State == RunState.Planning;

        runButton.gameObject.SetActive(allowRun);
        runButton.interactable = planning;
        foreach (Button b in actionButtons.Values) b.interactable = planning;
        if (actionButtons.TryGetValue("undo", out Button undo))
            undo.interactable = planning && Selected != null && Selected.Plan.Count > 0;

        if (PanelOpen)
            playhead.anchoredPosition = new Vector2(clock.CurrentTime * pixelsPerSecond, 0f);

        float pulse = 0.18f + 0.12f * Mathf.Sin(Time.time * 5f);
        foreach (SpriteRenderer m in cellMarkers) m.color = new Color(1f, 1f, 1f, pulse);
        foreach (SpriteRenderer m in cameraMarkers) m.color = new Color(1f, 0.2f, 0.2f, pulse + 0.2f);
    }

    // ------------------------------------------------------------------ drawing on the map

    void CreatePath(Character c)
    {
        var lr = new GameObject("Path " + c.DisplayName).AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.sortingOrder = 4;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        if (c.body != null) lr.sharedMaterial = c.body.sharedMaterial; // a sprite material that is always in the build
        paths[c] = lr;
    }

    void RefreshPaths()
    {
        foreach (var pair in paths)
        {
            Character c = pair.Key;
            LineRenderer lr = pair.Value;
            if (c == null || lr == null) continue;

            Color col = RoleColor(c.Role);
            col.a = c == Selected ? 0.95f : 0.45f;
            lr.startColor = lr.endColor = col;
            lr.widthMultiplier = c == Selected ? 0.1f : 0.06f;

            var points = new List<Vector3>();
            float end = c.PlanEndTime;
            for (float t = 0f; t <= end + 0.001f; t += 0.1f) points.Add(map.DocToWorld(c.PositionAt(t)));
            if (points.Count < 2) points.Clear();
            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
        }
    }

    /// <summary>Dotted line along every guard's patrol, so the player sees where the guard walks.</summary>
    void CreateGuardRoutes()
    {
        foreach (Guard g in spawner.Guards)
        {
            Vector2 a = g.PatrolStart, b = g.PatrolEnd;
            int dots = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.3f));
            for (int i = 0; i <= dots; i++)
            {
                SpriteRenderer dot = NewMarker("Patrol dot", map.DocToWorld(Vector2.Lerp(a, b, i / (float)dots)), 0.12f);
                dot.color = new Color(0.3f, 0.55f, 1f, 0.8f);
                dot.enabled = true;
            }
        }
    }

    /// <summary>Highlights shown while choosing where to walk or which camera to switch off.</summary>
    void CreateCellMarkers()
    {
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.IsWalkable(new Vector2Int(x, y)))
                    cellMarkers.Add(NewMarker("Cell marker", map.DocToWorld(new Vector2(x, y)), 0.86f));

        foreach (SecurityCamera cam in spawner.Cameras)
            cameraMarkers.Add(NewMarker("Camera marker", cam.transform.position, 1f));
    }

    SpriteRenderer NewMarker(string name, Vector3 position, float size)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.transform.position = position;
        sr.sprite = map.wallLineSprite; // any plain white square
        sr.sortingOrder = 1;
        Vector2 s = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        sr.transform.localScale = new Vector3(size / s.x, size / s.y, 1f);
        sr.enabled = false;
        return sr;
    }

    // ------------------------------------------------------------------ timeline

    void RefreshTimeline()
    {
        foreach (GameObject g in timelineItems) Destroy(g);
        timelineItems.Clear();

        List<Character> chars = spawner.Characters;
        for (int i = 0; i < chars.Count; i++)
        {
            Character c = chars[i];
            bool selected = c == Selected;

            Image stripe = UIKit.NewImage("Row", timelineArea, new Color(1, 1, 1, selected ? 0.10f : 0.03f));
            stripe.raycastTarget = false;
            var srt = stripe.rectTransform;
            srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0, 1);
            srt.sizeDelta = new Vector2(0, rowHeight - 4); srt.anchoredPosition = new Vector2(0, -i * rowHeight);
            timelineItems.Add(stripe.gameObject);

            Text name = UIKit.NewText(timelineArea, c.DisplayName, 26, TextAnchor.MiddleRight,
                                      selected ? RoleColor(c.Role) : UIKit.Muted, FontStyle.Bold);
            UIKit.Place(name.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(-12, -i * rowHeight), new Vector2(160, rowHeight));
            timelineItems.Add(name.gameObject);

            foreach (Command cmd in c.Plan)
            {
                float width = Mathf.Max(cmd.Duration * pixelsPerSecond, 64f);
                Color col = BlockColor(cmd);
                if (!selected) col *= 0.6f;
                Image block = UIKit.NewImage(cmd.Label, timelineArea, col);
                block.raycastTarget = false; // presses go to the scrubber underneath
                UIKit.Place(block.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                            new Vector2(cmd.StartTime * pixelsPerSecond, -i * rowHeight - 4), new Vector2(width - 2, rowHeight - 12));
                string label = cmd is WaitCommand w ? $"Wait {w.Seconds:0}s" : cmd.Label;
                UIKit.NewText(block.rectTransform, label, 22, TextAnchor.MiddleCenter, Color.white);
                timelineItems.Add(block.gameObject);
            }
        }

        // Second marks every 5 seconds.
        for (int s = 0; s <= 40; s += 5)
        {
            Text mark = UIKit.NewText(timelineArea, s + "s", 20, TextAnchor.UpperCenter, UIKit.Muted);
            UIKit.Place(mark.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(s * pixelsPerSecond, -2), new Vector2(60, 26));
            timelineItems.Add(mark.gameObject);
        }
        playhead.SetAsLastSibling();
    }

    /// <summary>Screen rectangle of one command block in the selected character's row (for the tutorial).</summary>
    public Rect LastBlockScreenRect(Character c)
    {
        int row = spawner.Characters.IndexOf(c);
        if (row < 0 || c.Plan.Count == 0) return UIKit.ScreenRect(timelineArea);
        Command cmd = c.Plan[c.Plan.Count - 1];
        var corners = new Vector3[4];
        timelineArea.GetWorldCorners(corners);
        float scale = timelineArea.lossyScale.x;
        float x0 = corners[1].x + cmd.StartTime * pixelsPerSecond * scale;
        float w = Mathf.Max(cmd.Duration * pixelsPerSecond, 64f) * scale;
        float yTop = corners[1].y - row * rowHeight * scale;
        return Rect.MinMaxRect(x0, yTop - rowHeight * scale, x0 + w, yTop);
    }

    static Color RoleColor(CharacterRole role)
    {
        switch (role)
        {
            case CharacterRole.Hacker: return new Color(0.2f, 0.75f, 1f);
            case CharacterRole.Sneak: return new Color(0.75f, 0.5f, 1f);
            case CharacterRole.Burglar: return new Color(1f, 0.6f, 0.2f);
            case CharacterRole.Impostor: return new Color(0.35f, 0.85f, 0.4f);
            default: return new Color(1f, 0.85f, 0.25f);
        }
    }

    static Color BlockColor(Command cmd)
    {
        if (cmd is MoveCommand) return new Color(0.25f, 0.45f, 0.78f);
        if (cmd is WaitCommand) return new Color(0.38f, 0.38f, 0.44f);
        if (cmd is DisableCameraCommand) return new Color(0.8f, 0.25f, 0.25f);
        return new Color(0.85f, 0.65f, 0.15f);
    }

    // ------------------------------------------------------------------ building the UI

    void BuildUI()
    {
        Transform root = UIKit.CreateCanvas("Planning UI").transform;

        // Taps that miss every panel land here and go to the map.
        Image catcher = UIKit.NewImage("Map Input", root, new Color(0, 0, 0, 0));
        UIKit.Stretch(catcher.rectTransform);
        catcher.gameObject.AddComponent<MapTapCatcher>().ui = this;

        // Top bar: status text, Home, RUN.
        Image top = UIKit.NewImage("Top Bar", root, UIKit.Panel);
        var trt = top.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
        trt.sizeDelta = new Vector2(0, topBarHeight); trt.anchoredPosition = Vector2.zero;

        Button home = UIKit.NewButton(trt, "< Home", () => GameManager.Ensure().GoHome());
        UIKit.Place((RectTransform)home.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(200, 80));

        statusText = UIKit.NewText(trt, "", 34, TextAnchor.MiddleLeft, Color.white);
        UIKit.Stretch(statusText.rectTransform, 250, 0, 330, 0);

        runButton = UIKit.NewButton(trt, "RUN  >", Run, UIKit.Green, 40);
        UIKit.Place((RectTransform)runButton.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(280, 90));

        // Bottom panel: the selected character's timeline window.
        Image p = UIKit.NewImage("Timeline Window", root, UIKit.Panel);
        panel = p.gameObject;
        var prt = p.rectTransform;
        prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(0.5f, 0);
        prt.sizeDelta = new Vector2(0, panelHeight); prt.anchoredPosition = Vector2.zero;

        panelPortrait = UIKit.NewSprite("Portrait", prt, null, Color.white);
        UIKit.Place(panelPortrait.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -12), new Vector2(110, 110));
        panelName = UIKit.NewText(prt, "", 30, TextAnchor.MiddleLeft, UIKit.Gold, FontStyle.Bold);
        UIKit.Place(panelName.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, -12), new Vector2(200, 50));

        actionBar = UIKit.NewRect("Actions", prt);
        actionBar.anchorMin = new Vector2(0, 1); actionBar.anchorMax = new Vector2(1, 1); actionBar.pivot = new Vector2(0, 1);
        actionBar.offsetMin = new Vector2(350, -112); actionBar.offsetMax = new Vector2(-300, -16);
        var layout = actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12; layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;

        doneButton = UIKit.NewButton(prt, "Done", ClosePanel, UIKit.Green, 34);
        UIKit.Place((RectTransform)doneButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -16), new Vector2(240, 96));

        Image area = UIKit.NewImage("Timeline", prt, new Color(1, 1, 1, 0.03f));
        timelineArea = area.rectTransform;
        timelineArea.anchorMin = new Vector2(0, 0); timelineArea.anchorMax = new Vector2(1, 1);
        timelineArea.pivot = new Vector2(0, 1);
        timelineArea.offsetMin = new Vector2(200, 46);
        timelineArea.offsetMax = new Vector2(-24, -140);

        Image ph = UIKit.NewImage("Playhead", timelineArea, UIKit.Gold);
        ph.raycastTarget = false;
        playhead = ph.rectTransform;
        playhead.anchorMin = new Vector2(0, 0); playhead.anchorMax = new Vector2(0, 1);
        playhead.pivot = new Vector2(0.5f, 0.5f);
        playhead.sizeDelta = new Vector2(4, 0);

        BuildFailWindow(root);
    }

    void BuildFailWindow(Transform root)
    {
        Image shade = UIKit.NewImage("Attempt Failed", root, new Color(0, 0, 0, 0.65f));
        UIKit.Stretch(shade.rectTransform);
        failWindow = shade.gameObject;

        Image box = UIKit.NewImage("Box", shade.rectTransform, UIKit.Panel);
        UIKit.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 520));

        Text title = UIKit.NewText(box.rectTransform, "Attempt failed", 70, TextAnchor.UpperCenter, new Color(1f, 0.45f, 0.4f), FontStyle.Bold);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1000, 90));

        failReason = UIKit.NewText(box.rectTransform, "", 36, TextAnchor.UpperCenter, Color.white);
        UIKit.Place(failReason.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(980, 220));

        Button back = UIKit.NewButton(box.rectTransform, "Back to planning", BackToPlanning, UIKit.Green, 36);
        UIKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(460, 100));

        failWindow.SetActive(false);
    }
}
