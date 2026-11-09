using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>A heist the player can choose on the home screen.</summary>
public class LevelInfo
{
    public string title;
    public string subtitle;
    public string sceneName;
    public bool locked;
    public bool isTutorial;
    public int teamSize = 2;
    public CharacterRole[] allowedRoles;

    public static readonly LevelInfo Tutorial = new LevelInfo
    {
        title = "Tutorial",
        subtitle = "Learn to plan your first heist",
        sceneName = "Tutorial",
        isTutorial = true,
        teamSize = 2,
        allowedRoles = new[] { CharacterRole.Hacker, CharacterRole.Sneak },
    };

    public static readonly LevelInfo[] All =
    {
        Tutorial,
        new LevelInfo { title = "Jewelry Store", subtitle = "Coming soon", locked = true },
        new LevelInfo { title = "Museum", subtitle = "Coming soon", locked = true },
        new LevelInfo { title = "Bank", subtitle = "Coming soon", locked = true },
    };

    public bool Allows(CharacterRole role) => allowedRoles == null || System.Array.IndexOf(allowedRoles, role) >= 0;
}

/// <summary>
/// Lives for the whole game (Singleton + DontDestroyOnLoad) and carries data between
/// scenes: the chosen heist, the chosen crew, attempts and the last run time.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public const string MainMenuScene = "MainMenu";
    public const string TeamSelectScene = "TeamSelect";
    public const string WinScene = "Win";

    public LevelInfo CurrentLevel { get; private set; } = LevelInfo.Tutorial;
    public readonly List<CharacterRole> Team = new List<CharacterRole>();
    public int Attempts { get; private set; }
    public float LastRunTime { get; private set; }

    /// <summary>Saved on the device, so it survives closing the game.</summary>
    public bool TutorialCompleted
    {
        get => PlayerPrefs.GetInt("TutorialCompleted", 0) == 1;
        private set { PlayerPrefs.SetInt("TutorialCompleted", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>Returns the one GameManager, creating it if this is the first scene to need it.</summary>
    public static GameManager Ensure()
    {
        if (Instance == null) new GameObject("GameManager").AddComponent<GameManager>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // a GameManager already exists from an earlier scene
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ChooseLevel(LevelInfo level)
    {
        CurrentLevel = level;
        Team.Clear();
        Attempts = 0;
        SceneManager.LoadScene(TeamSelectScene);
    }

    public void StartPlanning(IEnumerable<CharacterRole> crew)
    {
        Team.Clear();
        Team.AddRange(crew);
        SceneManager.LoadScene(CurrentLevel.sceneName);
    }

    public void RecordAttempt() => Attempts++;

    public void CompleteLevel(float runTime)
    {
        LastRunTime = runTime;
        if (CurrentLevel.isTutorial) TutorialCompleted = true;
        SceneManager.LoadScene(WinScene);
    }

    public void GoHome() => SceneManager.LoadScene(MainMenuScene);
}
