using System.Collections.Generic;
using UnityEngine;

/// <summary>A guard's patrol: the guard starts on its number in the layout and walks to patrolEnd and back.</summary>
[System.Serializable]
public class GuardRoute
{
    [Tooltip("The guard's number in the layout: 1, 2 or 3.")]
    public int guardNumber = 1;
    public Vector2Int patrolEnd = new Vector2Int(6, 4);
    [Tooltip("Seconds for a full round trip.")]
    public float period = 8f;
}

/// <summary>
/// Reads the MapBuilder layout and creates an instance of the right prefab on
/// every special character: '&' camera, '1'-'3' guard, '$' diamond, 'X' exit,
/// 'P' team start. Then hands everything to the RunClock.
/// </summary>
public class LevelSpawner : MonoBehaviour
{
    public MapBuilder map;
    public RunClock clock;

    [Header("Prefabs")]
    public Character characterPrefab;
    public Guard guardPrefab;
    public SecurityCamera cameraPrefab;
    public Diamond diamondPrefab;
    public ExitZone exitPrefab;

    [Header("Level settings")]
    [Tooltip("Camera viewing radius in cells (tutorial: 2).")]
    public float cameraRadius = 2f;
    public float cameraOffSeconds = 8f;
    public List<GuardRoute> guardRoutes = new List<GuardRoute> { new GuardRoute() };

    [Header("Team")]
    public List<CharacterData> team = new List<CharacterData>
    {
        new CharacterData { displayName = "Hacker", role = CharacterRole.Hacker },
        new CharacterData { displayName = "Sneak",  role = CharacterRole.Sneak },
    };

    [Tooltip("Give the team the correct tutorial plan (the \"Show me\" solution). Press Space to run it.")]
    public bool useDemoPlan = true;

    readonly List<Character> characters = new List<Character>();
    readonly List<Guard> guards = new List<Guard>();
    readonly List<SecurityCamera> cameras = new List<SecurityCamera>();
    readonly List<Diamond> diamonds = new List<Diamond>();
    readonly List<ExitZone> vans = new List<ExitZone>();

    public List<Character> Characters => characters;
    public List<SecurityCamera> Cameras => cameras;

    void Start()
    {
        Spawn();
        if (useDemoPlan) ApplyDemoPlan();
        clock.SetActors(characters, guards, cameras, diamonds, vans);
        clock.ResetToPlanning();
    }

    void Spawn()
    {
        Vector2Int start = Vector2Int.zero;

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                Vector3 pos = map.DocToWorld(cell);
                char ch = map.CharAt(x, y);

                switch (ch)
                {
                    case '&':
                        SecurityCamera cam = Instantiate(cameraPrefab, pos, Quaternion.identity, transform);
                        cam.Init(map, cell, cameraRadius);
                        cameras.Add(cam);
                        break;

                    case '1': case '2': case '3':
                        int number = ch - '0';
                        GuardRoute route = guardRoutes.Find(r => r.guardNumber == number)
                                           ?? new GuardRoute { guardNumber = number, patrolEnd = cell };
                        Guard guard = Instantiate(guardPrefab, pos, Quaternion.identity, transform);
                        guard.name = $"Guard G{number}";
                        guard.Init(map, cell, route.patrolEnd, route.period);
                        guards.Add(guard);
                        break;

                    case '$':
                        diamonds.Add(Instantiate(diamondPrefab, pos, Quaternion.identity, transform));
                        break;

                    case 'X':
                        ExitZone van = Instantiate(exitPrefab, pos, Quaternion.identity, transform);
                        van.Init(map);
                        vans.Add(van);
                        break;

                    case 'P':
                        start = cell;
                        break;
                }
            }
        }

        foreach (CharacterData data in team)
        {
            Character c = Instantiate(characterPrefab, map.DocToWorld(start), Quaternion.identity, transform);
            c.Init(map, data, start);
            characters.Add(c);
        }
    }

    /// <summary>
    /// The tutorial solution from the level design: the hacker switches the camera
    /// off at second 3, the sneak waits for the guard to turn its back, crosses,
    /// takes the diamond and both meet at the van.
    /// </summary>
    public void ApplyDemoPlan()
    {
        Character hacker = characters.Find(c => c.Role == CharacterRole.Hacker);
        Character sneak = characters.Find(c => c.Role == CharacterRole.Sneak);
        if (hacker == null || sneak == null || cameras.Count == 0)
        {
            Debug.LogWarning("Demo plan needs a Hacker, a Sneak and one camera.");
            return;
        }

        Vector2Int exit = FindCell('X');

        hacker.Plan.Clear();
        hacker.Plan.Add(new WaitCommand(3f));
        hacker.Plan.Add(new DisableCameraCommand(cameras[0].Cell, cameraOffSeconds));
        hacker.Plan.Add(new MoveCommand(exit));

        // The guard walks x=6 and sees 3 cells ahead, so column x=9 (3 cells away) is always safe.
        // Timing: cross the door while the camera is off (3-11) and the guard walks down,
        // take the diamond while its back is turned, then leave along x=9.
        sneak.Plan.Clear();
        sneak.Plan.Add(new MoveCommand(new Vector2Int(3, 3)));
        sneak.Plan.Add(new WaitCommand(6f));
        sneak.Plan.Add(new MoveCommand(new Vector2Int(7, 2)));
        sneak.Plan.Add(new MoveCommand(FindCell('$')));
        sneak.Plan.Add(new PickupCommand());
        sneak.Plan.Add(new MoveCommand(new Vector2Int(9, 1)));
        sneak.Plan.Add(new MoveCommand(new Vector2Int(9, 4)));
        sneak.Plan.Add(new MoveCommand(new Vector2Int(8, 4)));
        sneak.Plan.Add(new MoveCommand(exit));

        hacker.PreparePlan();
        sneak.PreparePlan();
    }

    public Vector2Int FindCell(char ch)
    {
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.CharAt(x, y) == ch) return new Vector2Int(x, y);
        return Vector2Int.zero;
    }
}
