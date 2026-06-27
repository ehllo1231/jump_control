using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PlaytestLogWindow : EditorWindow
{
    private const string SelectedLogPathKey = "JumpTiming.PlaytestLogs.SelectedPath";
    private const float MinimumFallThreshold = -1000f;
    private const float MaximumFallThreshold = 1000f;

    private string[] logPaths = Array.Empty<string>();
    private string[] logNames = Array.Empty<string>();
    private string selectedLogPath;
    private PlaytestLogData selectedLog;
    private string loadError;

    [MenuItem("Tools/Jump Timing/Playtest Logs", false, 32)]
    public static void OpenWindow()
    {
        PlaytestLogWindow window = GetWindow<PlaytestLogWindow>("Playtest Logs");
        window.minSize = new Vector2(390f, 360f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshLogList();
        string persistedPath = EditorPrefs.GetString(SelectedLogPathKey, string.Empty);
        if (string.IsNullOrEmpty(persistedPath) || !File.Exists(persistedPath))
        {
            persistedPath = logPaths.Length > 0 ? logPaths[0] : string.Empty;
        }

        SelectLog(persistedPath);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Playtest Logs", EditorStyles.boldLabel);

        DrawRecordingSettings();
        EditorGUILayout.Space(8f);
        DrawLogSelection();
        EditorGUILayout.Space(8f);
        DrawSelectedLogSummary();
    }

    private void DrawRecordingSettings()
    {
        EditorGUILayout.LabelField("Recording", EditorStyles.boldLabel);

        bool autoLogging = PlaytestLogger.AutoLoggingEnabled;
        bool nextAutoLogging = EditorGUILayout.Toggle("Record In Play Mode", autoLogging);
        if (nextAutoLogging != autoLogging)
        {
            PlaytestLogger.AutoLoggingEnabled = nextAutoLogging;
        }

        float fallY = PlaytestLogger.ConfiguredFallYThreshold;
        float nextFallY = EditorGUILayout.FloatField("Fall Y Threshold", fallY);
        nextFallY = Mathf.Clamp(nextFallY, MinimumFallThreshold, MaximumFallThreshold);
        if (!Mathf.Approximately(nextFallY, fallY))
        {
            PlaytestLogger.ConfiguredFallYThreshold = nextFallY;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Log Directory", PlaytestLogPaths.GetLogDirectory());
            }

            if (GUILayout.Button("Open", GUILayout.Width(56f)))
            {
                OpenLogDirectory();
            }
        }
    }

    private void DrawLogSelection()
    {
        EditorGUILayout.LabelField("Review", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
            {
                RefreshLogList();
                SelectLog(File.Exists(selectedLogPath) ? selectedLogPath : (logPaths.Length > 0 ? logPaths[0] : string.Empty));
            }

            bool showOverlay = PlaytestLogSceneOverlay.IsVisible;
            bool nextShowOverlay = EditorGUILayout.Toggle("Show In Scene View", showOverlay);
            if (nextShowOverlay != showOverlay)
            {
                PlaytestLogSceneOverlay.SetVisible(nextShowOverlay);
            }
        }

        if (logPaths.Length == 0)
        {
            EditorGUILayout.HelpBox("아직 기록된 플레이테스트 로그가 없습니다. Play Mode에서 테스트하면 로그가 생성됩니다.", MessageType.Info);
            return;
        }

        int selectedIndex = Array.IndexOf(logPaths, selectedLogPath);
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        int nextIndex = EditorGUILayout.Popup("Log File", selectedIndex, logNames);
        if (nextIndex >= 0 && nextIndex < logPaths.Length && nextIndex != selectedIndex)
        {
            SelectLog(logPaths[nextIndex]);
        }

        if (!string.IsNullOrEmpty(loadError))
        {
            EditorGUILayout.HelpBox(loadError, MessageType.Warning);
        }
    }

    private void DrawSelectedLogSummary()
    {
        if (selectedLog == null)
        {
            return;
        }

        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Scene", selectedLog.SceneName);
            EditorGUILayout.IntField("Movement Samples", selectedLog.Samples.Count);
            EditorGUILayout.IntField("Jump Events", selectedLog.Jumps.Count);
            EditorGUILayout.IntField("Fall Events", selectedLog.Falls.Count);
            EditorGUILayout.IntField("Max Trail Density", selectedLog.MaxSegmentDensity);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!selectedLog.HasBounds))
            {
                if (GUILayout.Button("Frame In Scene View"))
                {
                    PlaytestLogSceneOverlay.FrameSelectedLog();
                }
            }

            if (GUILayout.Button("Reload"))
            {
                SelectLog(selectedLogPath);
            }
        }

        EditorGUILayout.HelpBox(
            "Scene View에서 이동 궤적은 파란 선으로, 많이 겹친 구간은 더 굵고 밝게 표시됩니다. 점프는 청록 원, 낙하는 빨간 X로 표시됩니다.",
            MessageType.None);
    }

    private void RefreshLogList()
    {
        string logDirectory = PlaytestLogPaths.GetLogDirectory();
        if (!Directory.Exists(logDirectory))
        {
            logPaths = Array.Empty<string>();
            logNames = Array.Empty<string>();
            return;
        }

        logPaths = Directory.GetFiles(logDirectory, "*" + PlaytestLogPaths.LogFileExtension);
        Array.Sort(logPaths, CompareLogPathByRecentWriteTime);

        logNames = new string[logPaths.Length];
        for (int i = 0; i < logPaths.Length; i++)
        {
            FileInfo fileInfo = new FileInfo(logPaths[i]);
            logNames[i] = $"{Path.GetFileName(logPaths[i])} ({fileInfo.LastWriteTime:MM-dd HH:mm})";
        }
    }

    private void SelectLog(string path)
    {
        selectedLogPath = path;
        loadError = string.Empty;
        selectedLog = null;

        if (string.IsNullOrEmpty(path))
        {
            EditorPrefs.DeleteKey(SelectedLogPathKey);
            PlaytestLogSceneOverlay.SetData(null);
            return;
        }

        EditorPrefs.SetString(SelectedLogPathKey, path);
        if (!PlaytestLogFileReader.TryRead(path, out selectedLog, out loadError))
        {
            PlaytestLogSceneOverlay.SetData(null);
            return;
        }

        PlaytestLogSceneOverlay.SetData(selectedLog);
    }

    private static int CompareLogPathByRecentWriteTime(string left, string right)
    {
        DateTime leftTime = File.Exists(left) ? File.GetLastWriteTimeUtc(left) : DateTime.MinValue;
        DateTime rightTime = File.Exists(right) ? File.GetLastWriteTimeUtc(right) : DateTime.MinValue;
        return rightTime.CompareTo(leftTime);
    }

    private static void OpenLogDirectory()
    {
        string logDirectory = PlaytestLogPaths.GetLogDirectory();
        Directory.CreateDirectory(logDirectory);
        EditorUtility.RevealInFinder(logDirectory);
    }
}

[InitializeOnLoad]
internal static class PlaytestLogSceneOverlay
{
    private const string ShowOverlayKey = "JumpTiming.PlaytestLogs.ShowOverlay";
    private const float DensityCellSize = 0.75f;

    private static readonly Color trailColor = new Color(0.12f, 0.72f, 1f, 0.32f);
    private static readonly Color denseTrailColor = new Color(1f, 0.76f, 0.12f, 0.88f);
    private static readonly Color jumpColor = new Color(0.1f, 1f, 0.78f, 0.95f);
    private static readonly Color fallColor = new Color(1f, 0.18f, 0.16f, 0.98f);
    private static readonly Vector3[] lineBuffer = new Vector3[2];

    private static PlaytestLogData data;

    public static bool IsVisible => EditorPrefs.GetBool(ShowOverlayKey, true);

    static PlaytestLogSceneOverlay()
    {
        SceneView.duringSceneGui += DrawOverlay;
        EditorApplication.playModeStateChanged += _ => SceneView.RepaintAll();
    }

    public static void SetVisible(bool visible)
    {
        EditorPrefs.SetBool(ShowOverlayKey, visible);
        SceneView.RepaintAll();
    }

    public static void SetData(PlaytestLogData nextData)
    {
        data = nextData;
        if (data != null)
        {
            data.RebuildSegments(DensityCellSize);
        }

        SceneView.RepaintAll();
    }

    public static void FrameSelectedLog()
    {
        if (data == null || !data.HasBounds)
        {
            return;
        }

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            return;
        }

        sceneView.Frame(data.Bounds, false);
    }

    private static void DrawOverlay(SceneView sceneView)
    {
        if (!IsVisible || data == null)
        {
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent != null && currentEvent.type != EventType.Repaint)
        {
            return;
        }

        CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = CompareFunction.Always;
        DrawSegments();
        DrawJumpMarkers();
        DrawFallMarkers();
        Handles.zTest = previousZTest;
    }

    private static void DrawSegments()
    {
        int maxDensity = Mathf.Max(1, data.MaxSegmentDensity);
        for (int i = 0; i < data.Segments.Count; i++)
        {
            PlaytestLogSegment segment = data.Segments[i];
            float normalizedDensity = maxDensity <= 1
                ? 0f
                : Mathf.InverseLerp(1f, maxDensity, segment.Density);
            Handles.color = Color.Lerp(trailColor, denseTrailColor, normalizedDensity);

            lineBuffer[0] = new Vector3(segment.Start.x, segment.Start.y, 0f);
            lineBuffer[1] = new Vector3(segment.End.x, segment.End.y, 0f);
            Handles.DrawAAPolyLine(Mathf.Lerp(2.5f, 8f, normalizedDensity), lineBuffer);
        }
    }

    private static void DrawJumpMarkers()
    {
        Handles.color = jumpColor;
        for (int i = 0; i < data.Jumps.Count; i++)
        {
            Vector3 position = ToWorld(data.Jumps[i].Position);
            float radius = GetMarkerRadius(position);
            Handles.DrawSolidDisc(position, Vector3.forward, radius);
            lineBuffer[0] = position;
            lineBuffer[1] = position + Vector3.up * radius * 1.8f;
            Handles.DrawAAPolyLine(3f, lineBuffer);
        }
    }

    private static void DrawFallMarkers()
    {
        Handles.color = fallColor;
        for (int i = 0; i < data.Falls.Count; i++)
        {
            Vector3 position = ToWorld(data.Falls[i].Position);
            float radius = GetMarkerRadius(position) * 1.25f;
            Handles.DrawWireDisc(position, Vector3.forward, radius);

            lineBuffer[0] = position + new Vector3(-radius, -radius, 0f);
            lineBuffer[1] = position + new Vector3(radius, radius, 0f);
            Handles.DrawAAPolyLine(4f, lineBuffer);

            lineBuffer[0] = position + new Vector3(-radius, radius, 0f);
            lineBuffer[1] = position + new Vector3(radius, -radius, 0f);
            Handles.DrawAAPolyLine(4f, lineBuffer);
        }
    }

    private static Vector3 ToWorld(Vector2 position)
    {
        return new Vector3(position.x, position.y, 0f);
    }

    private static float GetMarkerRadius(Vector3 position)
    {
        return Mathf.Clamp(HandleUtility.GetHandleSize(position) * 0.06f, 0.06f, 0.45f);
    }
}

internal static class PlaytestLogFileReader
{
    public static bool TryRead(string path, out PlaytestLogData data, out string error)
    {
        data = null;
        error = string.Empty;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            error = "선택한 로그 파일을 찾지 못했습니다.";
            return false;
        }

        try
        {
            PlaytestLogData loaded = new PlaytestLogData(path);
            int skippedLines = 0;
            foreach (string line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                PlaytestLogRecord record;
                try
                {
                    record = JsonUtility.FromJson<PlaytestLogRecord>(line);
                }
                catch (ArgumentException)
                {
                    skippedLines++;
                    continue;
                }

                if (record == null || string.IsNullOrEmpty(record.type))
                {
                    skippedLines++;
                    continue;
                }

                AddRecord(loaded, record);
            }

            loaded.RebuildSegments(0.75f);
            data = loaded;
            if (skippedLines > 0)
            {
                error = $"{skippedLines}개 로그 라인을 읽지 못하고 건너뛰었습니다.";
            }

            return true;
        }
        catch (Exception exception)
        {
            error = $"로그 파일을 읽지 못했습니다. {exception.Message}";
            return false;
        }
    }

    private static void AddRecord(PlaytestLogData data, PlaytestLogRecord record)
    {
        if (!string.IsNullOrEmpty(record.scene) && string.IsNullOrEmpty(data.SceneName))
        {
            data.SceneName = record.scene;
        }

        if (string.Equals(record.type, PlaytestLogRecordTypes.Sample, StringComparison.Ordinal))
        {
            Vector2 position = new Vector2(record.x, record.y);
            if (IsValidPosition(position))
            {
                data.Samples.Add(position);
                data.Encapsulate(position);
            }

            return;
        }

        if (string.Equals(record.type, PlaytestLogRecordTypes.Jump, StringComparison.Ordinal))
        {
            AddMarker(data.Jumps, data, record);
            return;
        }

        if (string.Equals(record.type, PlaytestLogRecordTypes.Fall, StringComparison.Ordinal))
        {
            AddMarker(data.Falls, data, record);
        }
    }

    private static void AddMarker(List<PlaytestLogMarker> markers, PlaytestLogData data, PlaytestLogRecord record)
    {
        Vector2 position = new Vector2(record.x, record.y);
        if (!IsValidPosition(position))
        {
            return;
        }

        markers.Add(new PlaytestLogMarker(position, record.time, record.angle, record.power));
        data.Encapsulate(position);
    }

    private static bool IsValidPosition(Vector2 position)
    {
        return IsFinite(position.x) && IsFinite(position.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

internal sealed class PlaytestLogData
{
    public readonly string Path;
    public readonly List<Vector2> Samples = new List<Vector2>();
    public readonly List<PlaytestLogMarker> Jumps = new List<PlaytestLogMarker>();
    public readonly List<PlaytestLogMarker> Falls = new List<PlaytestLogMarker>();
    public readonly List<PlaytestLogSegment> Segments = new List<PlaytestLogSegment>();

    private Bounds bounds;

    public string SceneName;
    public int MaxSegmentDensity { get; private set; }
    public bool HasBounds { get; private set; }
    public Bounds Bounds => bounds;

    public PlaytestLogData(string path)
    {
        Path = path;
        SceneName = string.Empty;
    }

    public void Encapsulate(Vector2 position)
    {
        Vector3 worldPosition = new Vector3(position.x, position.y, 0f);
        if (!HasBounds)
        {
            bounds = new Bounds(worldPosition, Vector3.one);
            HasBounds = true;
            return;
        }

        bounds.Encapsulate(worldPosition);
    }

    public void RebuildSegments(float cellSize)
    {
        Segments.Clear();
        MaxSegmentDensity = 0;
        if (Samples.Count < 2)
        {
            return;
        }

        Dictionary<Vector2Int, int> densityByCell = new Dictionary<Vector2Int, int>();
        for (int i = 1; i < Samples.Count; i++)
        {
            Vector2 start = Samples[i - 1];
            Vector2 end = Samples[i];
            if ((end - start).sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            Vector2Int cell = GetSegmentCell(start, end, cellSize);
            densityByCell.TryGetValue(cell, out int density);
            densityByCell[cell] = density + 1;
        }

        for (int i = 1; i < Samples.Count; i++)
        {
            Vector2 start = Samples[i - 1];
            Vector2 end = Samples[i];
            if ((end - start).sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            Vector2Int cell = GetSegmentCell(start, end, cellSize);
            int density = densityByCell.TryGetValue(cell, out int foundDensity) ? foundDensity : 1;
            MaxSegmentDensity = Mathf.Max(MaxSegmentDensity, density);
            Segments.Add(new PlaytestLogSegment(start, end, density));
        }
    }

    private static Vector2Int GetSegmentCell(Vector2 start, Vector2 end, float cellSize)
    {
        float safeCellSize = Mathf.Max(0.01f, cellSize);
        Vector2 midpoint = (start + end) * 0.5f;
        return new Vector2Int(
            Mathf.FloorToInt(midpoint.x / safeCellSize),
            Mathf.FloorToInt(midpoint.y / safeCellSize));
    }
}

internal readonly struct PlaytestLogMarker
{
    public readonly Vector2 Position;
    public readonly float Time;
    public readonly float Angle;
    public readonly float Power;

    public PlaytestLogMarker(Vector2 position, float time, float angle, float power)
    {
        Position = position;
        Time = time;
        Angle = angle;
        Power = power;
    }
}

internal readonly struct PlaytestLogSegment
{
    public readonly Vector2 Start;
    public readonly Vector2 End;
    public readonly int Density;

    public PlaytestLogSegment(Vector2 start, Vector2 end, int density)
    {
        Start = start;
        End = end;
        Density = density;
    }
}
