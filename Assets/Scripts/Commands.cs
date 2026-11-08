using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One step in a character's plan, placed on the timeline.
/// Times are seconds from the moment "Run" is pressed. Positions are document
/// coordinates (x right, y down), the same as in the level design document.
/// </summary>
public abstract class Command
{
    public float StartTime { get; private set; }
    public Vector2Int StartCell { get; private set; }
    public Vector2Int EndCell { get; protected set; }
    public abstract float Duration { get; }
    public float EndTime => StartTime + Duration;

    /// <summary>Set once the run clock has passed StartTime and Begin() was called.</summary>
    public bool Begun { get; set; }

    /// <summary>Short text for the timeline UI.</summary>
    public abstract string Label { get; }

    /// <summary>
    /// Places the command on the timeline after the previous one.
    /// Returns false if the command can't be carried out (for example, no path).
    /// </summary>
    public virtual bool Prepare(Character character, MapBuilder map, float startTime, Vector2Int fromCell)
    {
        StartTime = startTime;
        StartCell = fromCell;
        EndCell = fromCell;
        return true;
    }

    /// <summary>Where the character is, <paramref name="localTime"/> seconds into this command.</summary>
    public virtual Vector2 PositionAt(float localTime) => StartCell;

    /// <summary>Called once, when the run clock reaches StartTime.</summary>
    public virtual void Begin(Character character) { }
}

/// <summary>Walk to a cell along the shortest path around walls.</summary>
public class MoveCommand : Command
{
    public readonly Vector2Int Target;
    List<Vector2Int> path;

    public MoveCommand(Vector2Int target) { Target = target; }

    public override string Label => $"Move ({Target.x},{Target.y})";

    public override float Duration => path == null ? 0f : (path.Count - 1) / Character.Speed;

    public override bool Prepare(Character character, MapBuilder map, float startTime, Vector2Int fromCell)
    {
        base.Prepare(character, map, startTime, fromCell);
        path = map.FindPath(fromCell, Target);
        if (path == null) return false;
        EndCell = Target;
        return true;
    }

    public override Vector2 PositionAt(float localTime)
    {
        if (path == null || path.Count < 2) return StartCell;
        float distance = Mathf.Clamp(localTime * Character.Speed, 0f, path.Count - 1);
        int i = Mathf.Min((int)distance, path.Count - 2);
        return Vector2.Lerp(path[i], path[i + 1], distance - i);
    }
}

/// <summary>Stand still for a number of seconds.</summary>
public class WaitCommand : Command
{
    public readonly float Seconds;
    public WaitCommand(float seconds) { Seconds = seconds; }
    public override string Label => $"Wait {Seconds:0.#}s";
    public override float Duration => Seconds;
}

/// <summary>Hacker ability: switch off a security camera for a while. Works from anywhere.</summary>
public class DisableCameraCommand : Command
{
    public readonly Vector2Int CameraCell;
    public readonly float Seconds;

    public DisableCameraCommand(Vector2Int cameraCell, float seconds)
    {
        CameraCell = cameraCell;
        Seconds = seconds;
    }

    public override string Label => $"Camera off {Seconds:0.#}s";
    public override float Duration => 0f;

    public override void Begin(Character character)
    {
        SecurityCamera cam = SecurityCamera.At(CameraCell);
        if (cam != null) cam.DisableFor(Seconds);
        else Debug.LogWarning($"No security camera at ({CameraCell.x},{CameraCell.y}).");
    }
}

/// <summary>Pick up the target item. The character must be standing on it.</summary>
public class PickupCommand : Command
{
    public override string Label => "Pick up";
    public override float Duration => 1f;
    public override void Begin(Character character) => character.TryPickup();
}
