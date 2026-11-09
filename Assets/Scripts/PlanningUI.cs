using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// The planning screen, built in code so there is nothing to wire by hand:
/// a message bar on top, and a bottom panel with character buttons, action
/// buttons and a timeline. Tapping the map adds a "walk" for the selected character.
/// Works with mouse and touch (Unity UI events).
/// </summary>
public class PlanningUI : MonoBehaviour
{
    public LevelSpawner spawner;
    public RunClock clock;
    public MapBuilder map;
    public CameraFit cameraFit;

    [Header("Layout (pixels at 1920x1080)")]
    public float topBarHeight = 150f;
    public float bottomPanelHeight = 330f;
    public float pixelsPerSecond = 40f;
    public float rowHeight = 80f;

    [Header("Tutorial locks (the tutorial switches these on step by step)")]
    public bool allowCameraOff = true;
    public bool allowPickup = true;
    public bool allowRun = true;

    /// <summary>When set, a "Continue" button is shown and calls this.</summary>
    public Action OnContinue;
    public event Action OnPlanChanged;

    public Character Selected { get; private set; }
    public bool Ready { get; private set; }

    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.93f);
    static readonly Color ButtonColor = new Color(0.22f, 0.25f, 0.32f);
    static readonly Color RunColor = new Color(0.15f, 0.55f, 0.25f);

    Font font;
    Text messageText, feedbackText, timeText;
    Button continueButton, waitButton, cameraButton, pickupButton, undoButton, clearButton, showButton, runButton;
    readonly Dictionary<Character, Button> characterButtons = new Dictionary<Character, Button>();
    readonly Dictionary<Character, LineRenderer> paths = new Dictionary<Character, LineRenderer>();
    readonly List<GameObject> blocks = new List<GameObject>();
    RectTransform timelineArea, playhead;

    IEnumerator Start()
    {
        yield return null; // let LevelSpawner create the characters first

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildUI();
        foreach (Character c in spawner.Characters) CreatePath(c);
        if (spawner.Characters.Count > 0) Select(spawner.Characters[0]);

        clock.OnRunStarted += () => Feedback("Running...", Color.white);
        clock.OnRunEnded += HandleRunEnded;

        if (cameraFit != null) cameraFit.SetSpace(1.8f, 4.3f);

        PlanChanged();
        Ready = true;
    }

    // ------------------------------------------------------------------ actions

    public void Select(Character c)
    {
        Selected = c;
        RefreshPaths();
    }

    /// <summary>A tap on the map: walk the selected character to that cell.</summary>
    public void MapTap(Vector2 screenPos)
    {
        if (clock.State != RunState.Planning || Selected == null) return;

        Vector3 world = Camera.main.ScreenToWorldPoint(screenPos);
        Vector2Int cell = map.WorldToDoc(world);
        if (!map.IsWalkable(cell))
        {
            Feedback("Can't walk there.", Color.yellow);
            return;
        }
        AddCommand(new MoveCommand(cell));
    }

    void AddWait()
    {
        List<Command> plan = Selected.Plan;
        if (plan.Count > 0 && plan[plan.Count - 1] is WaitCommand w)
            plan[plan.Count - 1] = new WaitCommand(w.Seconds + 1f); // merge into one longer wait
        else
            plan.Add(new WaitCommand(1f));
        PlanChanged();
    }

    void AddCameraOff()
    {
        if (spawner.Cameras.Count == 0) return;
        AddCommand(new DisableCameraCommand(spawner.Cameras[0].Cell, spawner.cameraOffSeconds));
    }

    void AddCommand(Command cmd)
    {
        Selected.Plan.Add(cmd);
        if (!Selected.PreparePlan())
        {
            Selected.Plan.RemoveAt(Selected.Plan.Count - 1);
            Feedback("That isn't possible from there.", Color.yellow);
        }
        PlanChanged();
    }

    void Undo()
    {
        if (Selected.Plan.Count > 0) Selected.Plan.RemoveAt(Selected.Plan.Count - 1);
        PlanChanged();
    }

    void Clear()
    {
        Selected.Plan.Clear();
        PlanChanged();
    }

    void ShowMe()
    {
        spawner.ApplyDemoPlan();
        PlanChanged();
        Feedback("Here is one plan that works. Tap RUN to watch it.", Color.cyan);
    }

    /// <summary>Recompute timings and redraw everything that depends on the plan.</summary>
    public void PlanChanged()
    {
        foreach (Character c in spawner.Characters) c.PreparePlan();
        RefreshTimeline();
        RefreshPaths();
        OnPlanChanged?.Invoke();
    }

    public void ScrubTo(Vector2 screenPos)
    {
        if (clock.State != RunState.Planning) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(timelineArea, screenPos, null, out Vector2 local);
        float x = local.x - timelineArea.rect.xMin;
        clock.PreviewTime = Mathf.Max(0f, x / pixelsPerSecond);
    }

    public void SetMessage(string text) => messageText.text = text;

    public void Feedback(string text, Color color)
    {
        feedbackText.text = text;
        feedbackText.color = color;
    }

    void HandleRunEnded(bool success, string reason)
    {
        if (success) Feedback("Heist complete!", Color.green);
        else Feedback(reason + (clock.tutorialMode ? "  Back to planning - your plan is kept." : ""), new Color(1f, 0.45f, 0.45f));
    }

    // ------------------------------------------------------------------ per-frame

    void Update()
    {
        if (!Ready) return;
        bool planning = clock.State == RunState.Planning;
        bool hasSel = Selected != null;

        foreach (var pair in characterButtons)
        {
            pair.Value.interactable = planning;
            pair.Value.image.color = pair.Key == Selected ? RoleColor(pair.Key.Role) : ButtonColor;
        }
        waitButton.interactable = planning && hasSel;
        cameraButton.interactable = planning && hasSel && allowCameraOff && Selected.Role == CharacterRole.Hacker;
        pickupButton.interactable = planning && hasSel && allowPickup;
        undoButton.interactable = planning && hasSel && Selected.Plan.Count > 0;
        clearButton.interactable = planning && hasSel && Selected.Plan.Count > 0;
        showButton.interactable = planning;
        runButton.interactable = planning && allowRun;
        continueButton.gameObject.SetActive(OnContinue != null);

        float t = clock.CurrentTime;
        playhead.anchoredPosition = new Vector2(t * pixelsPerSecond, 0f);
        timeText.text = $"{t:0.0} s";
    }

    // ------------------------------------------------------------------ drawing

    void RefreshTimeline()
    {
        foreach (GameObject b in blocks) Destroy(b);
        blocks.Clear();

        List<Character> chars = spawner.Characters;
        for (int i = 0; i < chars.Count; i++)
        {
            foreach (Command cmd in chars[i].Plan)
            {
                float width = Mathf.Max(cmd.Duration * pixelsPerSecond, 70f);
                Image block = NewImage(cmd.Label, timelineArea, BlockColor(cmd));
                block.raycastTarget = false; // let presses reach the scrubber underneath
                var rt = block.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(cmd.StartTime * pixelsPerSecond, -i * rowHeight - 6f);
                rt.sizeDelta = new Vector2(width - 2f, rowHeight - 12f);

                string label = cmd is WaitCommand w ? $"Wait {w.Seconds:0}s" : cmd.Label;
                NewText(rt, label, 24, TextAnchor.MiddleCenter, Color.white);
                blocks.Add(block.gameObject);
            }
        }
        playhead.SetAsLastSibling();
    }

    void CreatePath(Character c)
    {
        var go = new GameObject("Path " + c.DisplayName);
        var lr = go.AddComponent<LineRenderer>();
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
            Color col = RoleColor(c.Role);
            bool selected = c == Selected;
            col.a = selected ? 0.9f : 0.35f;
            lr.startColor = lr.endColor = col;
            lr.widthMultiplier = selected ? 0.09f : 0.05f;

            var points = new List<Vector3>();
            float end = c.PlanEndTime;
            for (float t = 0f; t <= end + 0.001f; t += 0.1f) points.Add(map.DocToWorld(c.PositionAt(t)));
            if (points.Count == 1) points.Clear();
            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
        }
    }

    static Color RoleColor(CharacterRole role)
    {
        switch (role)
        {
            case CharacterRole.Hacker: return new Color(0.2f, 0.75f, 1f);
            case CharacterRole.Sneak: return new Color(0.7f, 0.45f, 1f);
            case CharacterRole.Burglar: return new Color(1f, 0.6f, 0.2f);
            case CharacterRole.Impostor: return new Color(0.35f, 0.85f, 0.4f);
            default: return new Color(1f, 0.85f, 0.25f);
        }
    }

    static Color BlockColor(Command cmd)
    {
        if (cmd is MoveCommand) return new Color(0.25f, 0.45f, 0.75f);
        if (cmd is WaitCommand) return new Color(0.35f, 0.35f, 0.4f);
        if (cmd is DisableCameraCommand) return new Color(0.8f, 0.25f, 0.25f);
        return new Color(0.85f, 0.65f, 0.15f);
    }

    // ------------------------------------------------------------------ building the UI

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("Planning UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGO.transform;

        // Taps that miss every panel land here and go to the map.
        Image catcher = NewImage("Map Input", root, new Color(0, 0, 0, 0));
        Stretch(catcher.rectTransform);
        catcher.gameObject.AddComponent<MapTapCatcher>().ui = this;

        // Top bar: instructions and feedback.
        Image top = NewImage("Top Bar", root, PanelColor);
        var trt = top.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
        trt.sizeDelta = new Vector2(0, topBarHeight); trt.anchoredPosition = Vector2.zero;
        messageText = NewText(trt, "", 36, TextAnchor.UpperLeft, Color.white);
        Stretch(messageText.rectTransform, 30, 50, 340, 18);
        feedbackText = NewText(trt, "", 28, TextAnchor.LowerLeft, Color.white);
        Stretch(feedbackText.rectTransform, 30, 12, 340, 90);
        continueButton = NewButton(trt, "Continue", 280, () => OnContinue?.Invoke(), RunColor);
        var crt = (RectTransform)continueButton.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(1, 0.5f); crt.pivot = new Vector2(1, 0.5f);
        crt.sizeDelta = new Vector2(280, 90); crt.anchoredPosition = new Vector2(-30, 0);

        // Bottom panel.
        Image bottom = NewImage("Bottom Panel", root, PanelColor);
        var brt = bottom.rectTransform;
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(0.5f, 0);
        brt.sizeDelta = new Vector2(0, bottomPanelHeight); brt.anchoredPosition = Vector2.zero;

        // Row of buttons.
        var bar = NewRect("Buttons", brt);
        bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(0.5f, 1);
        bar.sizeDelta = new Vector2(-40, 86); bar.anchoredPosition = new Vector2(0, -12);
        var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12; layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;

        foreach (Character c in spawner.Characters)
        {
            Character captured = c;
            characterButtons[c] = NewButton(bar, c.DisplayName, 200, () => Select(captured));
        }
        Spacer(bar);
        waitButton = NewButton(bar, "Wait +1s", 190, AddWait);
        cameraButton = NewButton(bar, "Camera off", 210, AddCameraOff);
        pickupButton = NewButton(bar, "Pick up", 170, () => AddCommand(new PickupCommand()));
        undoButton = NewButton(bar, "Undo", 140, Undo);
        clearButton = NewButton(bar, "Clear", 140, Clear);
        Spacer(bar);
        showButton = NewButton(bar, "Show me", 190, ShowMe);
        runButton = NewButton(bar, "RUN", 200, () => clock.StartRun(), RunColor);

        // Timeline: one row per character, scrub by pressing/dragging.
        float timelineTop = 12 + 86 + 14;
        Image area = NewImage("Timeline", brt, new Color(1, 1, 1, 0.04f));
        timelineArea = area.rectTransform;
        timelineArea.anchorMin = new Vector2(0, 1); timelineArea.anchorMax = new Vector2(1, 1);
        timelineArea.pivot = new Vector2(0, 1);
        timelineArea.offsetMin = new Vector2(220, -timelineTop - rowHeight * spawner.Characters.Count);
        timelineArea.offsetMax = new Vector2(-20, -timelineTop);
        area.gameObject.AddComponent<TimelineScrubber>().ui = this;

        for (int i = 0; i < spawner.Characters.Count; i++)
        {
            Character c = spawner.Characters[i];
            Text name = NewText(brt, c.DisplayName, 30, TextAnchor.MiddleLeft, RoleColor(c.Role));
            var nrt = name.rectTransform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0, 1); nrt.pivot = new Vector2(0, 1);
            nrt.sizeDelta = new Vector2(190, rowHeight);
            nrt.anchoredPosition = new Vector2(24, -timelineTop - i * rowHeight);

            Image stripe = NewImage("Row " + c.DisplayName, timelineArea, new Color(1, 1, 1, i % 2 == 0 ? 0.05f : 0.02f));
            stripe.raycastTarget = false;
            var srt = stripe.rectTransform;
            srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0, 1);
            srt.sizeDelta = new Vector2(0, rowHeight); srt.anchoredPosition = new Vector2(0, -i * rowHeight);
        }

        timeText = NewText(brt, "0.0 s", 28, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.3f));
        var trt2 = timeText.rectTransform;
        trt2.anchorMin = trt2.anchorMax = new Vector2(0, 1); trt2.pivot = new Vector2(0, 1);
        trt2.sizeDelta = new Vector2(190, 40);
        trt2.anchoredPosition = new Vector2(24, -timelineTop - rowHeight * spawner.Characters.Count - 4);

        Image ph = NewImage("Playhead", timelineArea, new Color(1f, 0.85f, 0.3f));
        ph.raycastTarget = false;
        playhead = ph.rectTransform;
        playhead.anchorMin = new Vector2(0, 0); playhead.anchorMax = new Vector2(0, 1);
        playhead.pivot = new Vector2(0.5f, 0.5f);
        playhead.sizeDelta = new Vector2(4, 0);
    }

    // ------------------------------------------------------------------ small UI helpers

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var img = NewRect(name, parent).gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    Text NewText(Transform parent, string text, int size, TextAnchor anchor, Color color)
    {
        RectTransform rt = NewRect("Text", parent);
        Stretch(rt);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    Button NewButton(Transform parent, string label, float width, UnityEngine.Events.UnityAction onClick, Color? color = null)
    {
        Image img = NewImage(label, parent, color ?? ButtonColor);
        var b = img.gameObject.AddComponent<Button>();
        b.onClick.AddListener(onClick);
        var le = img.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = width;
        NewText(img.transform, label, 30, TextAnchor.MiddleCenter, Color.white);
        return b;
    }

    static void Spacer(Transform parent)
    {
        var le = NewRect("Spacer", parent).gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
    }

    static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }
}
