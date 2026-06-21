using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택된 두 Platform의 거리 정보를 Scene View에 표시합니다.
/// 표시 책임만 가지며 측정 계산은 PlatformMeasurementUtility에 위임합니다.
/// </summary>
[InitializeOnLoad]
public static class PlatformSceneOverlay
{
    private static GUIStyle labelStyle;

    static PlatformSceneOverlay()
    {
        SceneView.duringSceneGui += DrawMeasurement;
        Selection.selectionChanged += SceneView.RepaintAll;
    }

    private static void DrawMeasurement(SceneView sceneView)
    {
        List<Platform2D> platforms = GetSelectedPlatforms();
        if (platforms.Count != 2)
        {
            return;
        }

        Platform2D activePlatform = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<Platform2D>()
            : null;

        Platform2D from = platforms[0];
        Platform2D to = platforms[1];
        if (activePlatform == from)
        {
            from = platforms[1];
            to = platforms[0];
        }

        PlatformMeasurement measurement = PlatformMeasurementUtility.Measure(from, to);
        Vector3 start = measurement.Start;
        Vector3 end = measurement.End;
        Vector3 midpoint = (start + end) * 0.5f;

        Color previousColor = Handles.color;
        Handles.color = new Color(0.2f, 1f, 0.85f, 1f);
        Handles.DrawDottedLine(start, end, 4f);
        Handles.DrawWireDisc(start, Vector3.forward, HandleUtility.GetHandleSize(start) * 0.05f);
        Handles.DrawWireDisc(end, Vector3.forward, HandleUtility.GetHandleSize(end) * 0.05f);

        string label =
            $"A → B\nΔX: {measurement.Delta.x:0.00}\nΔY: {measurement.Delta.y:0.00}\nDistance: {measurement.Distance:0.00}";
        Handles.Label(midpoint, label, GetLabelStyle());
        Handles.Label(start, "A", GetLabelStyle());
        Handles.Label(end, "B", GetLabelStyle());
        Handles.color = previousColor;
    }

    private static List<Platform2D> GetSelectedPlatforms()
    {
        List<Platform2D> platforms = new List<Platform2D>(2);
        HashSet<Platform2D> uniquePlatforms = new HashSet<Platform2D>();

        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            Platform2D platform = selectedObject != null
                ? selectedObject.GetComponentInParent<Platform2D>()
                : null;

            if (platform != null && uniquePlatforms.Add(platform))
            {
                platforms.Add(platform);
            }
        }

        return platforms;
    }

    private static GUIStyle GetLabelStyle()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        return labelStyle;
    }
}
