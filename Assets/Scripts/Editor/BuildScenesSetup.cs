using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only: keeps the Build Profiles scene list in the right order
/// (MainMenu first) whenever the project opens or scripts recompile.
/// </summary>
[InitializeOnLoad]
static class BuildScenesSetup
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/TeamSelect.unity",
        "Assets/Scenes/Tutorial.unity",
        "Assets/Scenes/Win.unity",
    };

    static BuildScenesSetup() => EditorApplication.delayCall += Apply;

    [MenuItem("Tools/Perfect Heist/Fix Build Scene List")]
    static void Apply()
    {
        // Only scenes Unity has already imported (they have an ID); a scene without one is invalid.
        var wanted = Scenes
            .Select(path => new { path, guid = AssetDatabase.AssetPathToGUID(path) })
            .Where(s => !string.IsNullOrEmpty(s.guid))
            .ToArray();

        var current = EditorBuildSettings.scenes;
        bool same = current.Length == wanted.Length &&
                    current.Select(s => s.path + "|" + s.guid).SequenceEqual(wanted.Select(w => w.path + "|" + w.guid));
        if (same) return;

        EditorBuildSettings.scenes = wanted
            .Select(w => new EditorBuildSettingsScene(new GUID(w.guid), true))
            .ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Build scene list set to: " + string.Join(", ", wanted.Select(w => w.path)));
    }
}
