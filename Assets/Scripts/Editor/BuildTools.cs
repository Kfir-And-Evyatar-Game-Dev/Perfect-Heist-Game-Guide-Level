using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Editor-only build commands (Tools > Perfect Heist):
/// WebGL for itch.io (built into Builds/WebGL and zipped), with settings that
/// work on itch.io and in phone browsers.
/// </summary>
static class BuildTools
{
    const string WebGLFolder = "Builds/WebGL";
    const string WebGLZip = "Builds/PerfectHeist-WebGL.zip";

    [MenuItem("Tools/Perfect Heist/Build WebGL for itch.io")]
    static void BuildWebGL()
    {
        // itch.io serves files without special headers, so use Gzip with the
        // JavaScript fallback that unpacks the game in the browser.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.productName = "Perfect Heist";

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Build WebGL", "The build scene list is empty.", "OK");
            return;
        }

        if (Directory.Exists(WebGLFolder)) Directory.Delete(WebGLFolder, true);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = WebGLFolder,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Build WebGL", "The build failed. See the Console for details.", "OK");
            return;
        }

        // itch.io wants a zip with index.html at the top level.
        if (File.Exists(WebGLZip)) File.Delete(WebGLZip);
        ZipFile.CreateFromDirectory(WebGLFolder, WebGLZip, System.IO.Compression.CompressionLevel.Optimal, false);

        string full = Path.GetFullPath(WebGLZip);
        Debug.Log("WebGL build ready: " + full);
        EditorUtility.RevealInFinder(full);
        EditorUtility.DisplayDialog("Build WebGL",
            "Done!\n\nUpload this file to itch.io:\n" + full, "OK");
    }

    const string AndroidApk = "Builds/PerfectHeist.apk";

    /// <summary>
    /// Builds an installable APK (not an App Bundle), signed with Unity's debug key,
    /// which is enough to install it on a phone and to offer it as a download on itch.io.
    /// </summary>
    [MenuItem("Tools/Perfect Heist/Build Android APK")]
    static void BuildAndroid()
    {
        PlayerSettings.productName = "Perfect Heist";
        EditorUserBuildSettings.buildAppBundle = false;               // .apk, not .aab
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory("Builds");
        if (File.Exists(AndroidApk)) File.Delete(AndroidApk);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = AndroidApk,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Build Android", "The build failed. See the Console for details.", "OK");
            return;
        }

        string full = Path.GetFullPath(AndroidApk);
        Debug.Log("Android build ready: " + full);
        EditorUtility.RevealInFinder(full);
        EditorUtility.DisplayDialog("Build Android",
            "Done!\n\nInstall on the phone and upload to itch.io:\n" + full, "OK");
    }
}
