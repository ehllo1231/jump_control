using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlaytestLogViewerWindow : EditorWindow
{
    private const float SidebarWidth = 320f;
    private const float ToolbarHeight = 34f;
    private const float MinimumMapWidth = 240f;
    private const float MinimumZoom = 8f;
    private const float MaximumZoom = 220f;
    private const float DefaultZoom = 36f;
    private const float FitPadding = 64f;
    private const float TrailLineWidth = 2.5f;
    private const int CircleSegmentCount = 36;

    private static readonly Color mapBackgroundColor = new Color(0.105f, 0.115f, 0.125f, 1f);
    private static readonly Color mapGridColor = new Color(1f, 1f, 1f, 0.055f);
    private static readonly Color mapAxisColor = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color defaultMapFillColor = new Color(0.46f, 0.49f, 0.52f, 0.76f);
    private static readonly Color defaultMapLineColor = new Color(0.76f, 0.8f, 0.84f, 0.9f);
    private static readonly Color playerFillColor = new Color(1f, 1f, 1f, 0.9f);
    private static readonly Color playerLineColor = new Color(0.08f, 0.09f, 0.1f, 1f);
    private static readonly Color jumpMarkerColor = new Color(0.1f, 1f, 0.78f, 0.95f);
    private static readonly Color fallMarkerColor = new Color(1f, 0.18f, 0.16f, 0.98f);

    private static readonly Color[] logPalette =
    {
        new Color(0.14f, 0.72f, 1f, 0.92f),
        new Color(1f, 0.64f, 0.18f, 0.92f),
        new Color(0.45f, 0.95f, 0.38f, 0.92f),
        new Color(1f, 0.32f, 0.56f, 0.92f),
        new Color(0.72f, 0.56f, 1f, 0.92f),
        new Color(1f, 0.92f, 0.28f, 0.92f),
        new Color(0.28f, 0.95f, 0.88f, 0.92f),
        new Color(0.95f, 0.95f, 0.95f, 0.92f)
    };

    private readonly List<MapContour> mapContours = new List<MapContour>(256);
    private readonly List<LogEntry> logEntries = new List<LogEntry>(64);
    private readonly Vector3[] lineBuffer = new Vector3[2];

    private Vector2 viewCenter;
    private float zoom = DefaultZoom;
    private bool isPanning;
    private Vector2 lastMousePosition;
    private bool hasMapBounds;
    private Bounds mapBounds;
    private bool hasScenePlayer;
    private Vector2 scenePlayerPosition;
    private Vector2 logListScroll;
    private string mapStatus = string.Empty;

    [MenuItem("Tools/Jump Timing/Log Viewer", false, 33)]
    public static void OpenWindow()
    {
        PlaytestLogViewerWindow window = GetWindow<PlaytestLogViewerWindow>("Log Viewer");
        window.minSize = new Vector2(760f, 420f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshMapGeometry();
        RefreshLogList();
        EditorApplication.hierarchyChanged += HandleSceneChanged;
        Undo.undoRedoPerformed += HandleSceneChanged;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= HandleSceneChanged;
        Undo.undoRedoPerformed -= HandleSceneChanged;
    }

    private void OnGUI()
    {
        Rect mapRect = new Rect(
            0f,
            0f,
            Mathf.Max(MinimumMapWidth, position.width - SidebarWidth),
            position.height);
        Rect sidebarRect = new Rect(mapRect.xMax, 0f, SidebarWidth, position.height);

        if (Event.current.type == EventType.Repaint)
        {
            RefreshMapGeometry();
        }

        HandleMapInput(mapRect);
        DrawMap(mapRect);
        DrawSidebar(sidebarRect);
    }

    private void DrawMap(Rect mapRect)
    {
        EditorGUI.DrawRect(mapRect, mapBackgroundColor);
        GUI.BeginGroup(mapRect);
        Rect localRect = new Rect(0f, 0f, mapRect.width, mapRect.height);

        Handles.BeginGUI();
        DrawGrid(localRect);
        DrawMapContours(localRect);
        DrawSelectedLogs(localRect);
        DrawScenePlayer(localRect);
        Handles.EndGUI();

        GUI.EndGroup();
        DrawMapToolbar(mapRect);
    }

    private void DrawMapToolbar(Rect mapRect)
    {
        GUILayout.BeginArea(new Rect(mapRect.x + 8f, mapRect.y + 8f, mapRect.width - 16f, ToolbarHeight));
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Fit Map", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                FrameBounds(GetMapOnlyBounds(), mapRect);
            }

            if (GUILayout.Button("Fit Logs", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                FrameBounds(GetVisibleBounds(includeSelectedLogs: true), mapRect);
            }

            if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(28f)))
            {
                SetZoomAt(mapRect, mapRect.size * 0.5f, zoom * 0.85f);
            }

            GUILayout.Label($"{Mathf.RoundToInt(zoom)} px/u", EditorStyles.miniLabel, GUILayout.Width(64f));

            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(28f)))
            {
                SetZoomAt(mapRect, mapRect.size * 0.5f, zoom * 1.18f);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(SceneManager.GetActiveScene().name, EditorStyles.miniLabel, GUILayout.Width(150f));

            if (GUILayout.Button("Refresh Map", EditorStyles.toolbarButton, GUILayout.Width(92f)))
            {
                RefreshMapGeometry();
                Repaint();
            }
        }
        GUILayout.EndArea();

        if (!string.IsNullOrEmpty(mapStatus))
        {
            GUI.Label(
                new Rect(mapRect.x + 10f, mapRect.yMax - 26f, mapRect.width - 20f, 20f),
                mapStatus,
                EditorStyles.miniLabel);
        }
    }

    private void DrawSidebar(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.16f, 0.165f, 0.175f, 1f));
        GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f));
        EditorGUILayout.LabelField("Playtest Logs", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Logs"))
            {
                RefreshLogList();
            }

            if (GUILayout.Button("Clear"))
            {
                ClearSelectedLogs();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Latest"))
            {
                SelectLatestLog();
            }

            if (GUILayout.Button("All"))
            {
                SelectAllLogs();
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"Selected: {CountSelectedLogs()} / {logEntries.Count}", EditorStyles.miniLabel);

        logListScroll = EditorGUILayout.BeginScrollView(logListScroll);
        if (logEntries.Count == 0)
        {
            EditorGUILayout.HelpBox("기록된 로그 파일이 없습니다.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < logEntries.Count; i++)
            {
                DrawLogEntry(logEntries[i]);
            }
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawLogEntry(LogEntry entry)
    {
        Rect rowRect = EditorGUILayout.GetControlRect(false, 22f);
        Rect swatchRect = new Rect(rowRect.x, rowRect.y + 4f, 14f, 14f);
        Rect toggleRect = new Rect(rowRect.x + 20f, rowRect.y, rowRect.width - 20f, rowRect.height);

        EditorGUI.DrawRect(swatchRect, entry.Color);
        bool nextSelected = EditorGUI.ToggleLeft(toggleRect, entry.DisplayName, entry.IsSelected);
        if (nextSelected != entry.IsSelected)
        {
            entry.IsSelected = nextSelected;
            if (entry.IsSelected)
            {
                LoadLogEntry(entry);
            }

            Repaint();
        }

        if (!entry.IsSelected)
        {
            return;
        }

        EditorGUI.indentLevel++;
        if (!string.IsNullOrEmpty(entry.Error))
        {
            EditorGUILayout.HelpBox(entry.Error, MessageType.Warning);
        }
        else if (entry.Data != null)
        {
            EditorGUILayout.LabelField(
                $"Samples {entry.Data.Samples.Count}  Jumps {entry.Data.Jumps.Count}  Falls {entry.Data.Falls.Count}",
                EditorStyles.miniLabel);
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(2f);
    }

    private void DrawGrid(Rect rect)
    {
        float worldStep = GetGridStep(48f / Mathf.Max(1f, zoom));
        Vector2 minWorld = GuiToWorld(new Vector2(0f, rect.height), rect);
        Vector2 maxWorld = GuiToWorld(new Vector2(rect.width, 0f), rect);

        Handles.color = mapGridColor;
        float startX = Mathf.Floor(minWorld.x / worldStep) * worldStep;
        for (float x = startX; x <= maxWorld.x; x += worldStep)
        {
            Vector2 a = WorldToGui(new Vector2(x, minWorld.y), rect);
            Vector2 b = WorldToGui(new Vector2(x, maxWorld.y), rect);
            DrawGuiLine(a, b, 1f);
        }

        float startY = Mathf.Floor(minWorld.y / worldStep) * worldStep;
        for (float y = startY; y <= maxWorld.y; y += worldStep)
        {
            Vector2 a = WorldToGui(new Vector2(minWorld.x, y), rect);
            Vector2 b = WorldToGui(new Vector2(maxWorld.x, y), rect);
            DrawGuiLine(a, b, 1f);
        }

        Handles.color = mapAxisColor;
        DrawGuiLine(WorldToGui(new Vector2(0f, minWorld.y), rect), WorldToGui(new Vector2(0f, maxWorld.y), rect), 1.5f);
        DrawGuiLine(WorldToGui(new Vector2(minWorld.x, 0f), rect), WorldToGui(new Vector2(maxWorld.x, 0f), rect), 1.5f);
    }

    private void DrawMapContours(Rect rect)
    {
        for (int i = 0; i < mapContours.Count; i++)
        {
            MapContour contour = mapContours[i];
            if (contour.Points.Length < 2)
            {
                continue;
            }

            Vector3[] guiPoints = ToGuiPoints(contour.Points, rect, contour.Closed);
            if (contour.Closed && contour.Points.Length >= 3)
            {
                Handles.color = contour.FillColor;
                Handles.DrawAAConvexPolygon(ToGuiPoints(contour.Points, rect, close: false));
            }

            Handles.color = contour.LineColor;
            Handles.DrawAAPolyLine(1.8f, guiPoints);
        }
    }

    private void DrawSelectedLogs(Rect rect)
    {
        for (int i = 0; i < logEntries.Count; i++)
        {
            LogEntry entry = logEntries[i];
            if (!entry.IsSelected)
            {
                continue;
            }

            LoadLogEntry(entry);
            if (entry.Data == null)
            {
                continue;
            }

            DrawLogData(entry, rect);
        }
    }

    private void DrawLogData(LogEntry entry, Rect rect)
    {
        Handles.color = entry.Color;
        for (int i = 0; i < entry.Data.Segments.Count; i++)
        {
            PlaytestLogSegment segment = entry.Data.Segments[i];
            DrawGuiLine(WorldToGui(segment.Start, rect), WorldToGui(segment.End, rect), TrailLineWidth);
        }

        Handles.color = jumpMarkerColor;
        for (int i = 0; i < entry.Data.Jumps.Count; i++)
        {
            DrawCircleMarker(WorldToGui(entry.Data.Jumps[i].Position, rect), 5f);
        }

        Handles.color = fallMarkerColor;
        for (int i = 0; i < entry.Data.Falls.Count; i++)
        {
            DrawFallMarker(WorldToGui(entry.Data.Falls[i].Position, rect), 6f);
        }
    }

    private void DrawScenePlayer(Rect rect)
    {
        if (!hasScenePlayer)
        {
            return;
        }

        Vector2 center = WorldToGui(scenePlayerPosition, rect);
        float size = 8f;
        Vector3[] points =
        {
            new Vector3(center.x, center.y - size, 0f),
            new Vector3(center.x + size, center.y, 0f),
            new Vector3(center.x, center.y + size, 0f),
            new Vector3(center.x - size, center.y, 0f)
        };

        Handles.color = playerFillColor;
        Handles.DrawAAConvexPolygon(points);
        Handles.color = playerLineColor;
        Handles.DrawAAPolyLine(2f, new[]
        {
            points[0],
            points[1],
            points[2],
            points[3],
            points[0]
        });
    }

    private void DrawCircleMarker(Vector2 center, float radius)
    {
        Handles.DrawSolidDisc(new Vector3(center.x, center.y, 0f), Vector3.forward, radius);
    }

    private void DrawFallMarker(Vector2 center, float radius)
    {
        DrawGuiLine(center + new Vector2(-radius, -radius), center + new Vector2(radius, radius), 3f);
        DrawGuiLine(center + new Vector2(-radius, radius), center + new Vector2(radius, -radius), 3f);
    }

    private void DrawGuiLine(Vector2 start, Vector2 end, float width)
    {
        lineBuffer[0] = new Vector3(start.x, start.y, 0f);
        lineBuffer[1] = new Vector3(end.x, end.y, 0f);
        Handles.DrawAAPolyLine(width, lineBuffer);
    }

    private void RefreshMapGeometry()
    {
        mapContours.Clear();
        hasMapBounds = false;
        hasScenePlayer = false;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            mapStatus = "No active scene";
            return;
        }

        Collider2D[] colliders = UnityEngine.Object.FindObjectsByType<Collider2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (!ShouldDrawCollider(collider, activeScene))
            {
                continue;
            }

            AddColliderContours(collider);
        }

        PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (player != null && player.gameObject.scene == activeScene)
        {
            hasScenePlayer = true;
            scenePlayerPosition = player.transform.position;
            EncapsulateMapPoint(scenePlayerPosition);
        }

        mapStatus = hasMapBounds
            ? $"{mapContours.Count} map shapes"
            : "No 2D colliders in active scene";
    }

    private static bool ShouldDrawCollider(Collider2D collider, Scene activeScene)
    {
        if (collider == null
            || !collider.enabled
            || collider.isTrigger
            || !collider.gameObject.activeInHierarchy
            || collider.gameObject.scene != activeScene)
        {
            return false;
        }

        return collider.GetComponentInParent<PlayerController>() == null;
    }

    private void AddColliderContours(Collider2D collider)
    {
        Color fillColor;
        Color lineColor;
        GetColliderColors(collider, out fillColor, out lineColor);

        BoxCollider2D box = collider as BoxCollider2D;
        if (box != null)
        {
            AddClosedContour(GetBoxPoints(box), fillColor, lineColor);
            return;
        }

        PolygonCollider2D polygon = collider as PolygonCollider2D;
        if (polygon != null)
        {
            for (int i = 0; i < polygon.pathCount; i++)
            {
                AddClosedContour(TransformPoints(polygon.transform, polygon.GetPath(i)), fillColor, lineColor);
            }

            return;
        }

        CompositeCollider2D composite = collider as CompositeCollider2D;
        if (composite != null)
        {
            Vector2[] points = Array.Empty<Vector2>();
            for (int i = 0; i < composite.pathCount; i++)
            {
                int pointCount = composite.GetPathPointCount(i);
                if (points.Length < pointCount)
                {
                    points = new Vector2[pointCount];
                }

                composite.GetPath(i, points);
                AddClosedContour(TransformPoints(composite.transform, points, pointCount), fillColor, lineColor);
            }

            return;
        }

        CircleCollider2D circle = collider as CircleCollider2D;
        if (circle != null)
        {
            AddClosedContour(GetCirclePoints(circle), fillColor, lineColor);
            return;
        }

        EdgeCollider2D edge = collider as EdgeCollider2D;
        if (edge != null)
        {
            AddOpenContour(TransformPoints(edge.transform, edge.points, edge.pointCount, edge.offset), lineColor);
            return;
        }

        AddClosedContour(GetBoundsPoints(collider.bounds), fillColor, lineColor);
    }

    private void AddClosedContour(Vector2[] points, Color fillColor, Color lineColor)
    {
        if (points == null || points.Length < 3)
        {
            return;
        }

        mapContours.Add(new MapContour(points, closed: true, fillColor, lineColor));
        EncapsulateMapPoints(points);
    }

    private void AddOpenContour(Vector2[] points, Color lineColor)
    {
        if (points == null || points.Length < 2)
        {
            return;
        }

        mapContours.Add(new MapContour(points, closed: false, Color.clear, lineColor));
        EncapsulateMapPoints(points);
    }

    private void RefreshLogList()
    {
        HashSet<string> selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < logEntries.Count; i++)
        {
            if (logEntries[i].IsSelected)
            {
                selectedPaths.Add(logEntries[i].Path);
            }
        }

        logEntries.Clear();
        string logDirectory = PlaytestLogPaths.GetLogDirectory();
        if (!Directory.Exists(logDirectory))
        {
            Repaint();
            return;
        }

        string[] paths = Directory.GetFiles(logDirectory, "*" + PlaytestLogPaths.LogFileExtension);
        Array.Sort(paths, CompareLogPathByRecentWriteTime);
        for (int i = 0; i < paths.Length; i++)
        {
            LogEntry entry = new LogEntry(paths[i], FormatLogDisplayName(paths[i]), logPalette[i % logPalette.Length])
            {
                IsSelected = selectedPaths.Contains(paths[i])
            };

            if (entry.IsSelected)
            {
                LoadLogEntry(entry);
            }

            logEntries.Add(entry);
        }

        Repaint();
    }

    private static int CompareLogPathByRecentWriteTime(string left, string right)
    {
        DateTime leftTime = File.Exists(left) ? File.GetLastWriteTimeUtc(left) : DateTime.MinValue;
        DateTime rightTime = File.Exists(right) ? File.GetLastWriteTimeUtc(right) : DateTime.MinValue;
        return rightTime.CompareTo(leftTime);
    }

    private static string FormatLogDisplayName(string path)
    {
        DateTime writeTime = File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.MinValue;
        return $"{Path.GetFileNameWithoutExtension(path)}  {writeTime:MM-dd HH:mm}";
    }

    private void LoadLogEntry(LogEntry entry)
    {
        if (entry == null || entry.Data != null || !string.IsNullOrEmpty(entry.Error))
        {
            return;
        }

        if (!PlaytestLogFileReader.TryRead(entry.Path, out PlaytestLogData data, out string error))
        {
            entry.Error = error;
            return;
        }

        entry.Data = data;
    }

    private void ClearSelectedLogs()
    {
        for (int i = 0; i < logEntries.Count; i++)
        {
            logEntries[i].IsSelected = false;
        }

        Repaint();
    }

    private void SelectLatestLog()
    {
        ClearSelectedLogs();
        if (logEntries.Count == 0)
        {
            return;
        }

        logEntries[0].IsSelected = true;
        LoadLogEntry(logEntries[0]);
        Repaint();
    }

    private void SelectAllLogs()
    {
        for (int i = 0; i < logEntries.Count; i++)
        {
            logEntries[i].IsSelected = true;
            LoadLogEntry(logEntries[i]);
        }

        Repaint();
    }

    private int CountSelectedLogs()
    {
        int count = 0;
        for (int i = 0; i < logEntries.Count; i++)
        {
            if (logEntries[i].IsSelected)
            {
                count++;
            }
        }

        return count;
    }

    private void HandleMapInput(Rect mapRect)
    {
        Event current = Event.current;
        if (current == null)
        {
            return;
        }

        if (isPanning && current.type == EventType.MouseUp)
        {
            isPanning = false;
            current.Use();
            return;
        }

        if (!mapRect.Contains(current.mousePosition))
        {
            return;
        }

        Vector2 localMouse = current.mousePosition - mapRect.position;
        if (current.type == EventType.ScrollWheel)
        {
            float zoomMultiplier = Mathf.Pow(1.12f, -current.delta.y);
            SetZoomAt(mapRect, localMouse, zoom * zoomMultiplier);
            current.Use();
            Repaint();
            return;
        }

        if (current.type == EventType.MouseDown
            && (current.button == 2 || (current.button == 0 && current.alt)))
        {
            isPanning = true;
            lastMousePosition = current.mousePosition;
            current.Use();
            return;
        }

        if (current.type == EventType.MouseDrag && isPanning)
        {
            Vector2 delta = current.mousePosition - lastMousePosition;
            viewCenter += new Vector2(-delta.x / zoom, delta.y / zoom);
            lastMousePosition = current.mousePosition;
            current.Use();
            Repaint();
        }
    }

    private void SetZoomAt(Rect mapRect, Vector2 localMouse, float nextZoom)
    {
        Rect localRect = new Rect(0f, 0f, mapRect.width, mapRect.height);
        Vector2 worldBefore = GuiToWorld(localMouse, localRect);
        zoom = Mathf.Clamp(nextZoom, MinimumZoom, MaximumZoom);
        Vector2 worldAfter = GuiToWorld(localMouse, localRect);
        viewCenter += worldBefore - worldAfter;
    }

    private void FrameBounds(Bounds bounds, Rect mapRect)
    {
        if (bounds.size == Vector3.zero)
        {
            return;
        }

        viewCenter = new Vector2(bounds.center.x, bounds.center.y);
        float width = Mathf.Max(0.01f, bounds.size.x);
        float height = Mathf.Max(0.01f, bounds.size.y);
        float nextZoomX = Mathf.Max(1f, mapRect.width - FitPadding) / width;
        float nextZoomY = Mathf.Max(1f, mapRect.height - FitPadding) / height;
        zoom = Mathf.Clamp(Mathf.Min(nextZoomX, nextZoomY), MinimumZoom, MaximumZoom);
        Repaint();
    }

    private Bounds GetMapOnlyBounds()
    {
        if (hasMapBounds)
        {
            return mapBounds;
        }

        return new Bounds(Vector3.zero, Vector3.one * 8f);
    }

    private Bounds GetVisibleBounds(bool includeSelectedLogs)
    {
        Bounds bounds = GetMapOnlyBounds();
        bool hasBounds = hasMapBounds;
        if (includeSelectedLogs)
        {
            for (int i = 0; i < logEntries.Count; i++)
            {
                LogEntry entry = logEntries[i];
                if (!entry.IsSelected)
                {
                    continue;
                }

                LoadLogEntry(entry);
                if (entry.Data == null || !entry.Data.HasBounds)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = entry.Data.Bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(entry.Data.Bounds);
                }
            }
        }

        return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one * 8f);
    }

    private Vector2 WorldToGui(Vector2 world, Rect rect)
    {
        Vector2 center = rect.size * 0.5f;
        return new Vector2(
            center.x + (world.x - viewCenter.x) * zoom,
            center.y - (world.y - viewCenter.y) * zoom);
    }

    private Vector2 GuiToWorld(Vector2 gui, Rect rect)
    {
        Vector2 center = rect.size * 0.5f;
        return new Vector2(
            viewCenter.x + (gui.x - center.x) / zoom,
            viewCenter.y - (gui.y - center.y) / zoom);
    }

    private static float GetGridStep(float minimumWorldSpacing)
    {
        float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(Mathf.Max(0.0001f, minimumWorldSpacing))));
        float normalized = minimumWorldSpacing / magnitude;
        if (normalized > 5f)
        {
            return 10f * magnitude;
        }

        if (normalized > 2f)
        {
            return 5f * magnitude;
        }

        if (normalized > 1f)
        {
            return 2f * magnitude;
        }

        return magnitude;
    }

    private Vector3[] ToGuiPoints(Vector2[] worldPoints, Rect rect, bool close)
    {
        int pointCount = worldPoints.Length + (close ? 1 : 0);
        Vector3[] guiPoints = new Vector3[pointCount];
        for (int i = 0; i < worldPoints.Length; i++)
        {
            Vector2 point = WorldToGui(worldPoints[i], rect);
            guiPoints[i] = new Vector3(point.x, point.y, 0f);
        }

        if (close)
        {
            guiPoints[pointCount - 1] = guiPoints[0];
        }

        return guiPoints;
    }

    private void EncapsulateMapPoints(Vector2[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            EncapsulateMapPoint(points[i]);
        }
    }

    private void EncapsulateMapPoint(Vector2 point)
    {
        Vector3 world = new Vector3(point.x, point.y, 0f);
        if (!hasMapBounds)
        {
            mapBounds = new Bounds(world, Vector3.one * 0.1f);
            hasMapBounds = true;
            return;
        }

        mapBounds.Encapsulate(world);
    }

    private void HandleSceneChanged()
    {
        RefreshMapGeometry();
        Repaint();
    }

    private static void GetColliderColors(Collider2D collider, out Color fillColor, out Color lineColor)
    {
        SpriteRenderer spriteRenderer = collider.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = collider.GetComponentInParent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            Color source = spriteRenderer.color;
            fillColor = new Color(source.r, source.g, source.b, 0.72f);
            lineColor = new Color(
                Mathf.Min(1f, source.r + 0.24f),
                Mathf.Min(1f, source.g + 0.24f),
                Mathf.Min(1f, source.b + 0.24f),
                0.95f);
            return;
        }

        fillColor = defaultMapFillColor;
        lineColor = defaultMapLineColor;
    }

    private static Vector2[] GetBoxPoints(BoxCollider2D box)
    {
        Vector2 half = box.size * 0.5f;
        Vector2 offset = box.offset;
        Vector2[] local =
        {
            offset + new Vector2(-half.x, -half.y),
            offset + new Vector2(-half.x, half.y),
            offset + new Vector2(half.x, half.y),
            offset + new Vector2(half.x, -half.y)
        };

        return TransformPoints(box.transform, local);
    }

    private static Vector2[] GetCirclePoints(CircleCollider2D circle)
    {
        Vector2[] local = new Vector2[CircleSegmentCount];
        for (int i = 0; i < CircleSegmentCount; i++)
        {
            float angle = Mathf.PI * 2f * i / CircleSegmentCount;
            local[i] = circle.offset + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * circle.radius;
        }

        return TransformPoints(circle.transform, local);
    }

    private static Vector2[] GetBoundsPoints(Bounds bounds)
    {
        return new[]
        {
            new Vector2(bounds.min.x, bounds.min.y),
            new Vector2(bounds.min.x, bounds.max.y),
            new Vector2(bounds.max.x, bounds.max.y),
            new Vector2(bounds.max.x, bounds.min.y)
        };
    }

    private static Vector2[] TransformPoints(Transform transform, Vector2[] localPoints)
    {
        return TransformPoints(transform, localPoints, localPoints.Length);
    }

    private static Vector2[] TransformPoints(Transform transform, Vector2[] localPoints, int count)
    {
        Vector2[] worldPoints = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            worldPoints[i] = transform.TransformPoint(localPoints[i]);
        }

        return worldPoints;
    }

    private static Vector2[] TransformPoints(
        Transform transform,
        Vector2[] localPoints,
        int count,
        Vector2 offset)
    {
        Vector2[] worldPoints = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            worldPoints[i] = transform.TransformPoint(localPoints[i] + offset);
        }

        return worldPoints;
    }

    private sealed class LogEntry
    {
        public readonly string Path;
        public readonly string DisplayName;
        public readonly Color Color;
        public bool IsSelected;
        public PlaytestLogData Data;
        public string Error;

        public LogEntry(string path, string displayName, Color color)
        {
            Path = path;
            DisplayName = displayName;
            Color = color;
            Error = string.Empty;
        }
    }

    private readonly struct MapContour
    {
        public readonly Vector2[] Points;
        public readonly bool Closed;
        public readonly Color FillColor;
        public readonly Color LineColor;

        public MapContour(Vector2[] points, bool closed, Color fillColor, Color lineColor)
        {
            Points = points;
            Closed = closed;
            FillColor = fillColor;
            LineColor = lineColor;
        }
    }
}
