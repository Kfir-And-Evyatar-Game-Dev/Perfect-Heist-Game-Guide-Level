using UnityEditor;
using UnityEngine;

/// <summary>Adds a "Build Map" button to the MapBuilder component in the Inspector.</summary>
[CustomEditor(typeof(MapBuilder))]
public class MapBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("Build Map", GUILayout.Height(30)))
            ((MapBuilder)target).BuildMap();
    }
}
