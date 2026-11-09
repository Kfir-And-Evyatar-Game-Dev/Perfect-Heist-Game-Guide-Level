using System.Collections.Generic;
using UnityEngine;

public enum CharacterRole { Hacker, Burglar, Sneak, Impostor, Technician }

/// <summary>Setup data for one team member, edited in the LevelSpawner Inspector.</summary>
[System.Serializable]
public class CharacterData
{
    public string displayName = "Hacker";
    public CharacterRole role = CharacterRole.Hacker;
    [Tooltip("The character's image. Leave empty to keep the prefab's sprite.")]
    public Sprite sprite;
    [Tooltip("Tint applied to the sprite. Keep white for real images; use a colour for plain circles.")]
    public Color color = Color.white;
}

/// <summary>
/// A heist team member. Carries out its plan (a list of commands) as a pure
/// function of run time, so the same plan always gives the same result.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Character : MonoBehaviour
{
    /// <summary>Walking speed in cells per second (from the level design).</summary>
    public const float Speed = 2f;

    /// <summary>Seconds a character takes to get into the getaway van.</summary>
    public const float BoardTime = 0.5f;

    public SpriteRenderer body;

    public string DisplayName { get; private set; }
    public CharacterRole Role { get; private set; }
    public Vector2Int StartCell { get; private set; }
    public Vector2 DocPos { get; private set; }
    public bool HasDiamond { get; private set; }
    public bool InExit { get; set; }

    /// <summary>True once the character has climbed into the van and disappeared.</summary>
    public bool Boarded { get; private set; }

    public readonly List<Command> Plan = new List<Command>();

    MapBuilder map;
    Rigidbody2D rb;
    Collider2D col;
    Diamond touchingDiamond;
    ExitZone van;
    Color color;
    Vector3 baseScale;
    float boardStart = -1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        col = GetComponent<Collider2D>();
        baseScale = transform.localScale;
        if (body == null) body = GetComponentInChildren<SpriteRenderer>();
    }

    public void Init(MapBuilder map, CharacterData data, Vector2Int startCell)
    {
        this.map = map;
        DisplayName = data.displayName;
        Role = data.role;
        StartCell = startCell;
        color = data.color;
        name = data.displayName;
        if (body != null)
        {
            if (data.sprite != null) body.sprite = data.sprite;
            body.color = color;
            body.sortingOrder = 5;
        }
        ResetState();
    }

    /// <summary>Lays the commands out one after another on the timeline.</summary>
    public bool PreparePlan()
    {
        float t = 0f;
        Vector2Int cell = StartCell;
        foreach (Command c in Plan)
        {
            if (!c.Prepare(this, map, t, cell))
            {
                Debug.LogWarning($"{DisplayName}: can't do \"{c.Label}\" from ({cell.x},{cell.y}).", this);
                return false;
            }
            t = c.EndTime;
            cell = c.EndCell;
        }
        return true;
    }

    public float PlanEndTime => Plan.Count == 0 ? 0f : Plan[Plan.Count - 1].EndTime;

    /// <summary>Document position at a given run time.</summary>
    public Vector2 PositionAt(float t)
    {
        foreach (Command c in Plan)
        {
            if (t < c.StartTime) return c.StartCell;
            if (t < c.EndTime) return c.PositionAt(t - c.StartTime);
        }
        return Plan.Count == 0 ? (Vector2)StartCell : Plan[Plan.Count - 1].EndCell;
    }

    /// <summary>During the run: start due commands and move with physics so triggers fire.</summary>
    public void Simulate(float t)
    {
        if (Boarded) return;

        DocPos = PositionAt(t);
        foreach (Command c in Plan)
        {
            if (!c.Begun && t >= c.StartTime)
            {
                c.Begun = true;
                c.Begin(this);
            }
        }

        // Plan finished while touching the van: start climbing in.
        if (boardStart < 0f && van != null && InExit && t >= PlanEndTime)
            boardStart = t;

        Vector3 target = map.DocToWorld(DocPos);
        if (boardStart >= 0f)
        {
            // Slide into the van and shrink over BoardTime seconds, then disappear.
            float p = Mathf.Clamp01((t - boardStart) / BoardTime);
            target = Vector3.Lerp(target, van.transform.position, p);
            transform.localScale = baseScale * Mathf.Lerp(1f, 0.2f, p);
            if (p >= 1f)
            {
                Boarded = true;
                if (body != null) body.enabled = false;
                col.enabled = false; // no more camera or exit collisions
                return;
            }
        }
        rb.MovePosition(target);
    }

    /// <summary>During planning: show where the character will be at time t.</summary>
    public void ShowAt(float t)
    {
        DocPos = PositionAt(t);
        transform.position = map.DocToWorld(DocPos);
    }

    public void ResetState()
    {
        HasDiamond = false;
        InExit = false;
        Boarded = false;
        boardStart = -1f;
        touchingDiamond = null;
        van = null;
        foreach (Command c in Plan) c.Begun = false;
        transform.localScale = baseScale;
        if (col != null) col.enabled = true;
        if (body != null)
        {
            body.enabled = true;
            body.color = color;
        }
        if (map != null) ShowAt(0f);
    }

    public void MarkDetected()
    {
        if (body != null) body.color = Color.red;
    }

    public void TryPickup()
    {
        // The trigger may not have fired yet in this physics step, so also ask
        // the physics engine directly which colliders overlap our position.
        if (touchingDiamond == null)
        {
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(map.DocToWorld(DocPos), 0.25f))
                if (hit.TryGetComponent(out Diamond d)) touchingDiamond = d;
        }

        if (touchingDiamond != null && !touchingDiamond.Collected)
        {
            touchingDiamond.Collect(this);
            HasDiamond = true;
        }
        else
        {
            Debug.Log($"{DisplayName} tried to pick up, but isn't standing on the diamond.", this);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out Diamond d)) touchingDiamond = d;
        if (other.TryGetComponent(out ExitZone e))
        {
            InExit = true;
            van = e;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out Diamond d) && d == touchingDiamond) touchingDiamond = null;
        if (other.TryGetComponent(out ExitZone _)) InExit = false;
    }
}
