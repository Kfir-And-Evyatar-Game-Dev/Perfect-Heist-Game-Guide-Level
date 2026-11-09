using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes sure every scene has its screen and the GameManager, without placing
/// objects by hand: when the MainMenu or TeamSelect scene loads, the matching
/// screen script is created automatically.
/// </summary>
public static class ScreenBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameManager.Ensure();

        switch (scene.name)
        {
            case GameManager.MainMenuScene:
                if (Object.FindAnyObjectByType<MainMenuScreen>() == null)
                    new GameObject("Main Menu Screen").AddComponent<MainMenuScreen>();
                break;
            case GameManager.TeamSelectScene:
                if (Object.FindAnyObjectByType<TeamSelectScreen>() == null)
                    new GameObject("Crew Select Screen").AddComponent<TeamSelectScreen>();
                break;
            case GameManager.WinScene:
                if (Object.FindAnyObjectByType<WinScreen>() == null)
                    new GameObject("Win Screen").AddComponent<WinScreen>();
                break;
        }
    }
}
