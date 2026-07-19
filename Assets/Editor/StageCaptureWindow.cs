using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 열린 2D 스테이지의 전체 Renderer 영역을 하나 이상의 세로형 PNG로 저장합니다.
/// </summary>
public sealed class StageCaptureWindow : EditorWindow
{
    private const int DefaultOutputWidth = 2160;
    private const int DefaultTileHeight = 2048;
    private const float DefaultPadding = 0.5f;
    private const int MaximumPreviewWidth = 512;
    private const int MaximumPreviewHeight = 4096;
    private const int DividerControlHint = 0x53CA71;
    private const string LastSaveDirectoryKey = "JumpTiming.StageCapture.LastSaveDirectory";

    [SerializeField] private Camera sourceCamera;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private int outputWidth = DefaultOutputWidth;
    [SerializeField, HideInInspector] private int verticalSectionCount = 1;
    [SerializeField] private List<float> dividerPositions = new List<float>();
    [SerializeField] private Vector2 windowScrollPosition;
    [SerializeField] private int selectedDividerIndex = -1;
    [SerializeField] private int tileHeight = DefaultTileHeight;
    [SerializeField] private int antiAliasing = 4;
    [SerializeField] private float padding = DefaultPadding;
    [SerializeField] private bool revealAfterCapture = true;

    [NonSerialized] private Texture2D previewTexture;
    [NonSerialized] private bool previewDirty = true;
    [NonSerialized] private int previewPlanSignature = int.MinValue;
    [NonSerialized] private int previewAttemptedSignature = int.MinValue;
    [NonSerialized] private string previewError = string.Empty;
    [NonSerialized] private bool suppressPreviewInvalidation;

    [MenuItem("Tools/Jump Timing/Stage Capture", false, 70)]
    public static void OpenWindow()
    {
        StageCaptureWindow window = GetWindow<StageCaptureWindow>("Stage Capture");
        window.minSize = new Vector2(520f, 600f);
        window.Show();
    }

    private void OnEnable()
    {
        if (sourceCamera == null)
        {
            sourceCamera = FindDefaultCamera();
        }

        if (contentRoot == null)
        {
            contentRoot = FindDefaultContentRoot();
        }

        if (dividerPositions == null)
        {
            dividerPositions = new List<float>();
        }

        MigrateLegacySectionCount();
        EditorApplication.hierarchyChanged += MarkPreviewDirty;
        EditorApplication.projectChanged += MarkPreviewDirty;
        EditorSceneManager.sceneDirtied += HandleSceneDirtied;
        Undo.undoRedoPerformed += MarkPreviewDirty;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= MarkPreviewDirty;
        EditorApplication.projectChanged -= MarkPreviewDirty;
        EditorSceneManager.sceneDirtied -= HandleSceneDirtied;
        Undo.undoRedoPerformed -= MarkPreviewDirty;
        DestroyPreviewTexture();
    }

    private void OnGUI()
    {
        windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Stage Portrait Capture", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "현재 열린 2D 스테이지를 미리보고, 분할선을 직접 배치해 하나 이상의 고해상도 PNG로 저장합니다.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        EditorGUI.BeginChangeCheck();
        sourceCamera = EditorGUILayout.ObjectField(
            "Source Camera",
            sourceCamera,
            typeof(Camera),
            true) as Camera;
        contentRoot = EditorGUILayout.ObjectField(
            "Content Root (Optional)",
            contentRoot,
            typeof(Transform),
            true) as Transform;

        outputWidth = Mathf.Max(256, EditorGUILayout.IntField("PNG Width", outputWidth));
        padding = Mathf.Max(0f, EditorGUILayout.FloatField("World Padding", padding));
        tileHeight = Mathf.Max(256, EditorGUILayout.IntField("Render Tile Height", tileHeight));
        antiAliasing = DrawAntiAliasingPopup(antiAliasing);
        if (EditorGUI.EndChangeCheck())
        {
            MarkPreviewDirty();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Use Active Scene Camera"))
        {
            sourceCamera = FindDefaultCamera();
            MarkPreviewDirty();
        }

        if (GUILayout.Button("Auto Detect Content Root"))
        {
            contentRoot = FindDefaultContentRoot();
            MarkPreviewDirty();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Content Root는 이미지 영역만 결정합니다. 자동 선택된 배경 루트를 사용하면 멀리 떨어진 안전용 바닥은 제외하면서, 영역 안의 Platform과 Player는 함께 렌더링됩니다.",
            MessageType.None);

        revealAfterCapture = EditorGUILayout.Toggle("Reveal After Capture", revealAfterCapture);

        EditorGUILayout.Space(10f);
        bool hasPlan = StageCaptureUtility.TryCreatePlan(
            sourceCamera,
            contentRoot,
            outputWidth,
            tileHeight,
            antiAliasing,
            padding,
            out StageCapturePlan plan,
            out string error);

        StageCaptureSection[] sections = Array.Empty<StageCaptureSection>();
        bool canCapture = false;
        if (hasPlan)
        {
            SanitizeDividerPositions(plan.Height);
            int currentPlanSignature = GetPlanSignature(plan);
            if (previewPlanSignature != currentPlanSignature)
            {
                previewDirty = true;
            }

            if (previewAttemptedSignature != currentPlanSignature &&
                Event.current.type == EventType.Layout)
            {
                RefreshPreview(plan, currentPlanSignature);
            }

            sections = StageCaptureUtility.CreateSections(plan.Height, dividerPositions);
            DrawPlanSummary(plan, sections);
            DrawDividerToolbar(plan, currentPlanSignature);
            sections = StageCaptureUtility.CreateSections(plan.Height, dividerPositions);
            DrawCapturePreview(plan, sections);
            canCapture = previewTexture != null && !previewDirty;
        }
        else
        {
            DestroyPreviewTexture();
            previewAttemptedSignature = int.MinValue;
            EditorGUILayout.HelpBox(error, MessageType.Warning);
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!canCapture))
        {
            if (GUILayout.Button("Capture Stage PNG(s)...", GUILayout.Height(38f)))
            {
                Capture(plan, sections);
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Screen Space - Overlay Canvas와 Scene View의 편집용 선/아이콘은 카메라 이미지에 포함되지 않습니다.",
            MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private static int DrawAntiAliasingPopup(int currentValue)
    {
        int[] values = { 1, 2, 4, 8 };
        string[] labels = { "Off", "2x", "4x", "8x" };
        int currentIndex = Array.IndexOf(values, currentValue);
        currentIndex = Mathf.Max(0, currentIndex);
        return values[EditorGUILayout.Popup("Anti Aliasing", currentIndex, labels)];
    }

    private static void DrawPlanSummary(
        StageCapturePlan plan,
        IReadOnlyList<StageCaptureSection> sections)
    {
        double imageMemoryMegabytes = plan.Width * (double)plan.Height * 3d / (1024d * 1024d);
        EditorGUILayout.LabelField("Capture Plan", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Active Scene", plan.Scene.name);
        EditorGUILayout.LabelField("Included Renderers", plan.RendererCount.ToString("N0"));
        EditorGUILayout.LabelField("World Size", $"{plan.Area.width:F2} × {plan.Area.height:F2}");
        EditorGUILayout.LabelField("Full Resolution", $"{plan.Width:N0} × {plan.Height:N0}");
        EditorGUILayout.LabelField("Output Files", sections.Count.ToString("N0"));

        int minimumSectionHeight = int.MaxValue;
        int maximumSectionHeight = 0;
        for (int i = 0; i < sections.Count; i++)
        {
            minimumSectionHeight = Mathf.Min(minimumSectionHeight, sections[i].Height);
            maximumSectionHeight = Mathf.Max(maximumSectionHeight, sections[i].Height);
        }

        EditorGUILayout.LabelField(
            "Section Resolution",
            minimumSectionHeight == maximumSectionHeight
                ? $"{plan.Width:N0} × {minimumSectionHeight:N0}"
                : $"{plan.Width:N0} × {minimumSectionHeight:N0}~{maximumSectionHeight:N0}");
        EditorGUILayout.LabelField("Image Buffer", $"약 {imageMemoryMegabytes:F1} MB");

        if (plan.Height > plan.Width)
        {
            EditorGUILayout.HelpBox("세로형 이미지로 저장될 준비가 됐습니다.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "현재 Renderer 영역은 세로형보다 가로형에 가깝습니다. Content Root와 스테이지 배치를 확인하세요.",
                MessageType.Warning);
        }
    }

    private void DrawDividerToolbar(StageCapturePlan plan, int currentPlanSignature)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Stage Preview", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        using (new EditorGUI.DisabledScope(dividerPositions.Count >= plan.Height - 1))
        {
            if (GUILayout.Button(
                    new GUIContent("Add Divider", "가장 큰 구간의 가운데에 분할선을 추가합니다."),
                    EditorStyles.toolbarButton))
            {
                AddDivider(plan.Height);
            }
        }

        using (new EditorGUI.DisabledScope(
                   selectedDividerIndex < 0 || selectedDividerIndex >= dividerPositions.Count))
        {
            if (GUILayout.Button(
                    new GUIContent("Delete", "선택한 분할선을 삭제합니다."),
                    EditorStyles.toolbarButton))
            {
                dividerPositions.RemoveAt(selectedDividerIndex);
                selectedDividerIndex = Mathf.Min(
                    selectedDividerIndex,
                    dividerPositions.Count - 1);
            }
        }

        using (new EditorGUI.DisabledScope(dividerPositions.Count == 0))
        {
            if (GUILayout.Button(
                    new GUIContent("Even Spacing", "현재 구간들을 같은 높이로 배치합니다."),
                    EditorStyles.toolbarButton))
            {
                EvenlySpaceDividers(plan.Height);
            }

            if (GUILayout.Button(
                    new GUIContent("Reset", "모든 분할선을 제거합니다."),
                    EditorStyles.toolbarButton))
            {
                dividerPositions.Clear();
                selectedDividerIndex = -1;
            }
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button(
                new GUIContent("Refresh", "현재 씬으로 미리보기를 다시 렌더링합니다."),
                EditorStyles.toolbarButton))
        {
            RefreshPreview(plan, currentPlanSignature);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawCapturePreview(
        StageCapturePlan plan,
        IReadOnlyList<StageCaptureSection> sections)
    {
        if (!string.IsNullOrEmpty(previewError))
        {
            EditorGUILayout.HelpBox(previewError, MessageType.Error);
        }

        if (previewTexture == null)
        {
            EditorGUILayout.HelpBox("스테이지 미리보기를 준비하고 있습니다.", MessageType.Info);
            return;
        }

        if (previewDirty)
        {
            EditorGUILayout.HelpBox(
                "씬 또는 캡처 설정이 변경되었습니다. 미리보기를 갱신하세요.",
                MessageType.Warning);
        }

        float displayWidth = Mathf.Clamp(
            position.width - 42f,
            240f,
            MaximumPreviewWidth);
        float displayHeight = displayWidth * previewTexture.height / previewTexture.width;

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        Rect previewRect = GUILayoutUtility.GetRect(
            displayWidth,
            displayHeight,
            GUILayout.Width(displayWidth),
            GUILayout.Height(displayHeight));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUI.DrawTexture(previewRect, previewTexture, ScaleMode.StretchToFill, false);
        DrawSectionOverlays(previewRect, plan, sections);
        HandleDividerInput(previewRect, plan.Height);
        DrawPreviewBorder(previewRect);
    }

    private void DrawSectionOverlays(
        Rect previewRect,
        StageCapturePlan plan,
        IReadOnlyList<StageCaptureSection> sections)
    {
        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleLeft
        };
        labelStyle.normal.textColor = Color.white;

        for (int i = 0; i < sections.Count; i++)
        {
            StageCaptureSection section = sections[i];
            float sectionTop = previewRect.y +
                               previewRect.height * section.TopPixelOffset / plan.Height;
            float sectionHeight = previewRect.height * section.Height / plan.Height;
            Rect sectionRect = new Rect(
                previewRect.x,
                sectionTop,
                previewRect.width,
                sectionHeight);
            Color tint = i % 2 == 0
                ? new Color(0.05f, 0.65f, 1f, 0.06f)
                : new Color(1f, 0.65f, 0.05f, 0.05f);
            EditorGUI.DrawRect(sectionRect, tint);

            if (sectionRect.height < 18f)
            {
                continue;
            }

            string label = $"Part {i + 1:D2}  {plan.Width:N0} × {section.Height:N0}";
            float labelWidth = Mathf.Min(230f, previewRect.width - 12f);
            Rect labelRect = new Rect(
                previewRect.x + 6f,
                sectionRect.y + 4f,
                labelWidth,
                18f);
            EditorGUI.DrawRect(labelRect, new Color(0f, 0f, 0f, 0.58f));
            GUI.Label(labelRect, label, labelStyle);
        }

        for (int i = 0; i < dividerPositions.Count; i++)
        {
            float lineY = previewRect.y + previewRect.height * dividerPositions[i];
            bool isSelected = i == selectedDividerIndex;
            Color lineColor = isSelected
                ? new Color(1f, 0.78f, 0.1f, 1f)
                : new Color(0.1f, 0.85f, 1f, 1f);
            EditorGUI.DrawRect(
                new Rect(previewRect.x, lineY - 1f, previewRect.width, 2f),
                lineColor);

            Rect handleRect = new Rect(
                previewRect.xMax - 18f,
                lineY - 8f,
                18f,
                16f);
            EditorGUI.DrawRect(handleRect, lineColor);
            GUI.Label(handleRect, "=", EditorStyles.centeredGreyMiniLabel);
            EditorGUIUtility.AddCursorRect(
                new Rect(previewRect.x, lineY - 6f, previewRect.width, 12f),
                MouseCursor.ResizeVertical);
        }
    }

    private void HandleDividerInput(Rect previewRect, int totalPixelHeight)
    {
        int controlId = GUIUtility.GetControlID(
            DividerControlHint,
            FocusType.Passive,
            previewRect);
        Event currentEvent = Event.current;

        if (currentEvent.type == EventType.MouseDown &&
            currentEvent.button == 0 &&
            previewRect.Contains(currentEvent.mousePosition))
        {
            int hitDivider = FindDividerAt(previewRect, currentEvent.mousePosition.y);
            selectedDividerIndex = hitDivider;
            if (hitDivider >= 0)
            {
                GUIUtility.hotControl = controlId;
                currentEvent.Use();
            }

            Repaint();
        }
        else if (currentEvent.type == EventType.MouseDrag &&
                 GUIUtility.hotControl == controlId &&
                 selectedDividerIndex >= 0 &&
                 selectedDividerIndex < dividerPositions.Count)
        {
            float onePixel = 1f / totalPixelHeight;
            float minimum = selectedDividerIndex == 0
                ? onePixel
                : dividerPositions[selectedDividerIndex - 1] + onePixel;
            float maximum = selectedDividerIndex == dividerPositions.Count - 1
                ? 1f - onePixel
                : dividerPositions[selectedDividerIndex + 1] - onePixel;
            float normalizedPosition = Mathf.InverseLerp(
                previewRect.y,
                previewRect.yMax,
                currentEvent.mousePosition.y);
            dividerPositions[selectedDividerIndex] = Mathf.Clamp(
                normalizedPosition,
                minimum,
                maximum);
            SanitizeDividerPositions(totalPixelHeight);
            currentEvent.Use();
            Repaint();
        }
        else if (currentEvent.type == EventType.MouseUp &&
                 GUIUtility.hotControl == controlId)
        {
            GUIUtility.hotControl = 0;
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.KeyDown &&
                 selectedDividerIndex >= 0 &&
                 selectedDividerIndex < dividerPositions.Count &&
                 (currentEvent.keyCode == KeyCode.Delete ||
                  currentEvent.keyCode == KeyCode.Backspace))
        {
            dividerPositions.RemoveAt(selectedDividerIndex);
            selectedDividerIndex = Mathf.Min(
                selectedDividerIndex,
                dividerPositions.Count - 1);
            currentEvent.Use();
            Repaint();
        }
    }

    private int FindDividerAt(Rect previewRect, float mouseY)
    {
        int nearestIndex = -1;
        float nearestDistance = 7f;
        for (int i = 0; i < dividerPositions.Count; i++)
        {
            float dividerY = previewRect.y + previewRect.height * dividerPositions[i];
            float distance = Mathf.Abs(mouseY - dividerY);
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private void AddDivider(int totalPixelHeight)
    {
        StageCaptureSection[] sections = StageCaptureUtility.CreateSections(
            totalPixelHeight,
            dividerPositions);
        int largestSectionIndex = 0;
        for (int i = 1; i < sections.Length; i++)
        {
            if (sections[i].Height > sections[largestSectionIndex].Height)
            {
                largestSectionIndex = i;
            }
        }

        StageCaptureSection largestSection = sections[largestSectionIndex];
        if (largestSection.Height < 2)
        {
            return;
        }

        float dividerPosition =
            (largestSection.TopPixelOffset + largestSection.Height * 0.5f) /
            totalPixelHeight;
        dividerPositions.Add(dividerPosition);
        dividerPositions.Sort();
        selectedDividerIndex = dividerPositions.IndexOf(dividerPosition);
        SanitizeDividerPositions(totalPixelHeight);
    }

    private void EvenlySpaceDividers(int totalPixelHeight)
    {
        int sectionCount = dividerPositions.Count + 1;
        for (int i = 0; i < dividerPositions.Count; i++)
        {
            dividerPositions[i] = (i + 1f) / sectionCount;
        }

        SanitizeDividerPositions(totalPixelHeight);
    }

    private void SanitizeDividerPositions(int totalPixelHeight)
    {
        dividerPositions.Sort();
        int maximumDividerCount = Mathf.Max(0, totalPixelHeight - 1);
        if (dividerPositions.Count > maximumDividerCount)
        {
            dividerPositions.RemoveRange(
                maximumDividerCount,
                dividerPositions.Count - maximumDividerCount);
        }

        StageCaptureSection[] sections = StageCaptureUtility.CreateSections(
            totalPixelHeight,
            dividerPositions);
        dividerPositions.Clear();
        int cumulativeHeight = 0;
        for (int i = 0; i < sections.Length - 1; i++)
        {
            cumulativeHeight += sections[i].Height;
            dividerPositions.Add(cumulativeHeight / (float)totalPixelHeight);
        }

        selectedDividerIndex = Mathf.Clamp(
            selectedDividerIndex,
            -1,
            dividerPositions.Count - 1);
    }

    private void MigrateLegacySectionCount()
    {
        if (dividerPositions.Count == 0 && verticalSectionCount > 1)
        {
            int migratedSectionCount = Mathf.Clamp(verticalSectionCount, 1, 256);
            for (int i = 1; i < migratedSectionCount; i++)
            {
                dividerPositions.Add(i / (float)migratedSectionCount);
            }
        }

        verticalSectionCount = 1;
    }

    private void HandleSceneDirtied(Scene scene)
    {
        if (scene == SceneManager.GetActiveScene())
        {
            MarkPreviewDirty();
        }
    }

    private void MarkPreviewDirty()
    {
        if (suppressPreviewInvalidation)
        {
            return;
        }

        previewDirty = true;
        previewAttemptedSignature = int.MinValue;
        Repaint();
    }

    private void RefreshPreview(StageCapturePlan plan, int planSignature)
    {
        previewAttemptedSignature = planSignature;
        previewError = string.Empty;
        DestroyPreviewTexture();

        suppressPreviewInvalidation = true;
        try
        {
            previewTexture = StageCaptureUtility.RenderPreview(
                plan,
                MaximumPreviewWidth,
                MaximumPreviewHeight);
            previewPlanSignature = planSignature;
            previewDirty = false;
        }
        catch (Exception exception)
        {
            previewError = $"미리보기 렌더링에 실패했습니다. Console을 확인하세요.\n{exception.Message}";
            Debug.LogException(exception);
        }
        finally
        {
            suppressPreviewInvalidation = false;
            previewAttemptedSignature = planSignature;
        }

        Repaint();
    }

    private void DestroyPreviewTexture()
    {
        if (previewTexture != null)
        {
            DestroyImmediate(previewTexture);
            previewTexture = null;
        }

        previewPlanSignature = int.MinValue;
    }

    private static int GetPlanSignature(StageCapturePlan plan)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (int)plan.Scene.handle.GetRawData();
            hash = hash * 31 + plan.SourceCamera.GetEntityId().GetHashCode();
            hash = hash * 31 + plan.SourceCamera.cullingMask;
            hash = hash * 31 + (int)plan.SourceCamera.clearFlags;
            hash = hash * 31 + plan.SourceCamera.backgroundColor.GetHashCode();
            hash = hash * 31 + plan.SourceCamera.transform.localToWorldMatrix.GetHashCode();
            hash = hash * 31 + plan.Area.GetHashCode();
            hash = hash * 31 + plan.Width;
            hash = hash * 31 + plan.Height;
            hash = hash * 31 + plan.TileHeight;
            hash = hash * 31 + plan.AntiAliasing;
            hash = hash * 31 + plan.RendererCount;
            return hash;
        }
    }

    private static void DrawPreviewBorder(Rect rect)
    {
        Color borderColor = new Color(0f, 0f, 0f, 0.8f);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), borderColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), borderColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), borderColor);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), borderColor);
    }

    private void Capture(
        StageCapturePlan plan,
        IReadOnlyList<StageCaptureSection> sections)
    {
        string sceneName = string.IsNullOrEmpty(plan.Scene.name) ? "Stage" : plan.Scene.name;
        string baseFileName = $"{sceneName}-portrait-{DateTime.Now:yyyyMMdd-HHmmss}";
        string directory = EditorPrefs.GetString(LastSaveDirectoryKey, GetProjectRoot());
        if (!Directory.Exists(directory))
        {
            directory = GetProjectRoot();
        }

        string selectedDirectory = EditorUtility.OpenFolderPanel(
            "Select Stage Capture Folder",
            directory,
            string.Empty);

        if (string.IsNullOrEmpty(selectedDirectory))
        {
            return;
        }

        try
        {
            if (!StageCaptureUtility.CaptureSections(
                    plan,
                    selectedDirectory,
                    baseFileName,
                    sections,
                    out string[] outputPaths))
            {
                Debug.Log("Stage capture cancelled.");
                return;
            }

            EditorPrefs.SetString(LastSaveDirectoryKey, selectedDirectory);
            for (int i = 0; i < outputPaths.Length; i++)
            {
                ImportIfInsideAssets(outputPaths[i]);
            }

            Debug.Log(
                $"Stage portrait saved: {outputPaths.Length} file(s) in {selectedDirectory} " +
                $"(full size {plan.Width}x{plan.Height})");

            if (revealAfterCapture)
            {
                EditorUtility.RevealInFinder(outputPaths[0]);
            }

            int minimumSectionHeight = int.MaxValue;
            int maximumSectionHeight = 0;
            for (int i = 0; i < sections.Count; i++)
            {
                minimumSectionHeight = Mathf.Min(minimumSectionHeight, sections[i].Height);
                maximumSectionHeight = Mathf.Max(maximumSectionHeight, sections[i].Height);
            }
            string sectionHeightLabel = minimumSectionHeight == maximumSectionHeight
                ? minimumSectionHeight.ToString("N0")
                : $"{minimumSectionHeight:N0}~{maximumSectionHeight:N0}";
            string resultMessage = outputPaths.Length == 1
                ? $"{plan.Width:N0} × {plan.Height:N0} PNG 저장 완료\n\n{outputPaths[0]}"
                : $"{outputPaths.Length:N0}개 PNG 저장 완료\n" +
                  $"전체 해상도: {plan.Width:N0} × {plan.Height:N0}\n" +
                  $"분할 해상도: {plan.Width:N0} × {sectionHeightLabel}\n\n{selectedDirectory}";
            EditorUtility.DisplayDialog(
                "Stage Capture Complete",
                resultMessage,
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Stage Capture Failed",
                $"PNG 저장 중 오류가 발생했습니다. Console을 확인하세요.\n\n{exception.Message}",
                "OK");
        }
    }

    private static Camera FindDefaultCamera()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.gameObject.scene == scene)
        {
            return mainCamera;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Camera camera = roots[i].GetComponentInChildren<Camera>(false);
            if (camera != null)
            {
                return camera;
            }
        }

        return null;
    }

    private static string GetProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    private static Transform FindDefaultContentRoot()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] descendants = roots[i].GetComponentsInChildren<Transform>(true);
            for (int descendantIndex = 0; descendantIndex < descendants.Length; descendantIndex++)
            {
                if (string.Equals(
                        descendants[descendantIndex].name,
                        "Demon Castle Interior",
                        StringComparison.Ordinal))
                {
                    return descendants[descendantIndex];
                }
            }
        }

        Transform bestRoot = null;
        int bestRendererCount = 1;
        for (int i = 0; i < roots.Length; i++)
        {
            Renderer[] renderers = roots[i].GetComponentsInChildren<Renderer>(false);
            if (renderers.Length > bestRendererCount)
            {
                bestRoot = roots[i].transform;
                bestRendererCount = renderers.Length;
            }
        }

        return bestRoot;
    }

    private static void ImportIfInsideAssets(string absolutePath)
    {
        string normalizedPath = Path.GetFullPath(absolutePath).Replace('\\', '/');
        string normalizedAssets = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        if (!normalizedPath.StartsWith(normalizedAssets + "/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string assetPath = "Assets" + normalizedPath.Substring(normalizedAssets.Length);
        AssetDatabase.ImportAsset(assetPath);
    }
}

internal readonly struct StageCapturePlan
{
    public StageCapturePlan(
        Scene scene,
        Camera sourceCamera,
        Rect area,
        int width,
        int height,
        int tileHeight,
        int antiAliasing,
        int rendererCount)
    {
        Scene = scene;
        SourceCamera = sourceCamera;
        Area = area;
        Width = width;
        Height = height;
        TileHeight = tileHeight;
        AntiAliasing = antiAliasing;
        RendererCount = rendererCount;
    }

    public Scene Scene { get; }
    public Camera SourceCamera { get; }
    public Rect Area { get; }
    public int Width { get; }
    public int Height { get; }
    public int TileHeight { get; }
    public int AntiAliasing { get; }
    public int RendererCount { get; }
}

internal readonly struct StageCaptureSection
{
    public StageCaptureSection(int topPixelOffset, int height)
    {
        TopPixelOffset = topPixelOffset;
        Height = height;
    }

    public int TopPixelOffset { get; }
    public int Height { get; }
    public int EndPixelOffset => TopPixelOffset + Height;
}

internal static class StageCaptureUtility
{
    private const float MinimumWorldSize = 0.001f;
    private const float MinimumBoundarySearchDistance = 0.25f;
    private const float MaximumBoundarySearchDistance = 2f;
    private const float BoundarySearchWidthRatio = 0.05f;

    public static bool TryCreatePlan(
        Camera sourceCamera,
        Transform contentRoot,
        int outputWidth,
        int requestedTileHeight,
        int antiAliasing,
        float padding,
        out StageCapturePlan plan,
        out string error)
    {
        plan = default;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            error = "캡처할 활성 씬이 없습니다.";
            return false;
        }

        if (sourceCamera == null)
        {
            error = "Source Camera를 지정하세요.";
            return false;
        }

        if (sourceCamera.gameObject.scene != scene)
        {
            error = "Source Camera는 현재 활성 씬에 있어야 합니다.";
            return false;
        }

        if (!sourceCamera.orthographic)
        {
            error = "2D 전체 스테이지 캡처에는 Orthographic Camera가 필요합니다.";
            return false;
        }

        if (contentRoot != null && contentRoot.gameObject.scene != scene)
        {
            error = "Content Root는 현재 활성 씬에 있어야 합니다.";
            return false;
        }

        int maximumTextureSize = SystemInfo.maxTextureSize;
        if (outputWidth < 256)
        {
            error = "PNG Width는 256 이상이어야 합니다.";
            return false;
        }

        if (outputWidth > maximumTextureSize)
        {
            error = $"PNG Width가 이 장치의 최대 텍스처 크기({maximumTextureSize:N0})를 넘습니다.";
            return false;
        }

        List<Renderer> renderers = GetCandidateRenderers(scene, contentRoot);
        if (contentRoot != null)
        {
            if (!TryCalculateCameraSpaceBounds(
                    sourceCamera,
                    renderers,
                    0f,
                    out Rect contentArea,
                    out _))
            {
                error = "Source Camera에 표시되는 활성 Renderer를 찾지 못했습니다.";
                return false;
            }

            AddAdjacentStageBoundaryRenderers(
                sourceCamera,
                contentRoot,
                contentArea,
                renderers);
        }

        if (!TryCalculateCameraSpaceBounds(
                sourceCamera,
                renderers,
                Mathf.Max(0f, padding),
                out Rect rendererArea,
                out int rendererCount))
        {
            error = "Source Camera에 표시되는 활성 Renderer를 찾지 못했습니다.";
            return false;
        }

        double pixelsPerWorldUnit = outputWidth / (double)rendererArea.width;
        int outputHeight = Mathf.CeilToInt((float)(rendererArea.height * pixelsPerWorldUnit));
        outputHeight = Mathf.Max(1, outputHeight);

        if (outputHeight > maximumTextureSize)
        {
            int recommendedWidth = Mathf.Max(
                256,
                Mathf.FloorToInt(maximumTextureSize * rendererArea.width / rendererArea.height));
            error =
                $"자동 계산된 높이({outputHeight:N0})가 이 장치의 최대 텍스처 크기({maximumTextureSize:N0})를 넘습니다. " +
                $"PNG Width를 {recommendedWidth:N0} 이하로 낮추세요.";
            return false;
        }

        float exactWorldHeight = (float)(outputHeight / pixelsPerWorldUnit);
        Rect captureArea = new Rect(
            rendererArea.center.x - rendererArea.width * 0.5f,
            rendererArea.center.y - exactWorldHeight * 0.5f,
            rendererArea.width,
            exactWorldHeight);

        int safeTileHeight = Mathf.Clamp(requestedTileHeight, 256, maximumTextureSize);
        safeTileHeight = Mathf.Min(safeTileHeight, outputHeight);
        int safeAntiAliasing = NormalizeAntiAliasing(antiAliasing);

        plan = new StageCapturePlan(
            scene,
            sourceCamera,
            captureArea,
            outputWidth,
            outputHeight,
            safeTileHeight,
            safeAntiAliasing,
            rendererCount);
        error = string.Empty;
        return true;
    }

    public static StageCaptureSection[] CreateSections(
        int totalPixelHeight,
        IReadOnlyList<float> normalizedDividerPositions)
    {
        if (totalPixelHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPixelHeight));
        }

        int dividerCount = normalizedDividerPositions?.Count ?? 0;
        if (dividerCount >= totalPixelHeight)
        {
            throw new ArgumentException(
                "Divider count must leave at least one pixel in every section.",
                nameof(normalizedDividerPositions));
        }

        float[] sortedDividers = new float[dividerCount];
        for (int i = 0; i < dividerCount; i++)
        {
            float divider = normalizedDividerPositions[i];
            if (float.IsNaN(divider) || float.IsInfinity(divider))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(normalizedDividerPositions),
                    "Divider positions must be finite values.");
            }

            sortedDividers[i] = Mathf.Clamp01(divider);
        }

        Array.Sort(sortedDividers);
        StageCaptureSection[] sections = new StageCaptureSection[dividerCount + 1];
        int previousOffset = 0;
        for (int i = 0; i < dividerCount; i++)
        {
            int requestedOffset = Mathf.RoundToInt(sortedDividers[i] * totalPixelHeight);
            int minimumOffset = previousOffset + 1;
            int maximumOffset = totalPixelHeight - (dividerCount - i);
            int dividerOffset = Mathf.Clamp(
                requestedOffset,
                minimumOffset,
                maximumOffset);
            sections[i] = new StageCaptureSection(
                previousOffset,
                dividerOffset - previousOffset);
            previousOffset = dividerOffset;
        }

        sections[sections.Length - 1] = new StageCaptureSection(
            previousOffset,
            totalPixelHeight - previousOffset);
        return sections;
    }

    public static Texture2D RenderPreview(
        StageCapturePlan plan,
        int maximumWidth,
        int maximumHeight)
    {
        if (maximumWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumWidth));
        }

        if (maximumHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHeight));
        }

        double scale = Math.Min(
            1d,
            Math.Min(
                maximumWidth / (double)plan.Width,
                maximumHeight / (double)plan.Height));
        int previewWidth = Mathf.Max(1, Mathf.RoundToInt((float)(plan.Width * scale)));
        int previewHeight = Mathf.Max(1, Mathf.RoundToInt((float)(plan.Height * scale)));
        StageCapturePlan previewPlan = new StageCapturePlan(
            plan.Scene,
            plan.SourceCamera,
            plan.Area,
            previewWidth,
            previewHeight,
            Mathf.Min(1024, previewHeight),
            1,
            plan.RendererCount);

        Texture2D preview = RenderImage(previewPlan, false, out bool cancelled);
        if (cancelled || preview == null)
        {
            throw new InvalidOperationException("스테이지 미리보기 렌더링이 취소되었습니다.");
        }

        preview.name = "Stage Capture Preview";
        preview.hideFlags = HideFlags.HideAndDontSave;
        return preview;
    }

    public static bool Capture(StageCapturePlan plan, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is empty.", nameof(outputPath));
        }

        StageCaptureSection[] sections =
        {
            new StageCaptureSection(0, plan.Height)
        };
        return Capture(plan, new[] { outputPath }, sections, false);
    }

    public static bool CaptureSections(
        StageCapturePlan plan,
        string outputDirectory,
        string baseFileName,
        IReadOnlyList<StageCaptureSection> sections,
        out string[] outputPaths)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is empty.", nameof(outputDirectory));
        }

        if (string.IsNullOrWhiteSpace(baseFileName))
        {
            throw new ArgumentException("Base file name is empty.", nameof(baseFileName));
        }

        ValidateSections(plan.Height, sections);

        outputPaths = CreateUniqueOutputPaths(
            outputDirectory,
            SanitizeFileName(baseFileName),
            sections.Count);
        return Capture(plan, outputPaths, sections, true);
    }

    public static bool CaptureSections(
        StageCapturePlan plan,
        string outputDirectory,
        string baseFileName,
        int sectionCount,
        out string[] outputPaths)
    {
        if (sectionCount < 1 || sectionCount > plan.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sectionCount),
                $"Section count must be between 1 and {plan.Height:N0}.");
        }

        StageCaptureSection[] sections = new StageCaptureSection[sectionCount];
        int topPixelOffset = 0;
        for (int i = 0; i < sectionCount; i++)
        {
            int sectionHeight = GetSectionHeight(plan.Height, sectionCount, i);
            sections[i] = new StageCaptureSection(topPixelOffset, sectionHeight);
            topPixelOffset += sectionHeight;
        }

        return CaptureSections(
            plan,
            outputDirectory,
            baseFileName,
            sections,
            out outputPaths);
    }

    public static int GetSectionHeight(int totalPixelHeight, int sectionCount, int sectionIndex)
    {
        if (totalPixelHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPixelHeight));
        }

        if (sectionCount < 1 || sectionCount > totalPixelHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(sectionCount));
        }

        if (sectionIndex < 0 || sectionIndex >= sectionCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sectionIndex));
        }

        int baseHeight = totalPixelHeight / sectionCount;
        int remainder = totalPixelHeight % sectionCount;
        return baseHeight + (sectionIndex < remainder ? 1 : 0);
    }

    private static void ValidateSections(
        int totalPixelHeight,
        IReadOnlyList<StageCaptureSection> sections)
    {
        if (sections == null || sections.Count < 1 || sections.Count > totalPixelHeight)
        {
            throw new ArgumentException("Section list is invalid.", nameof(sections));
        }

        int expectedTopOffset = 0;
        for (int i = 0; i < sections.Count; i++)
        {
            StageCaptureSection section = sections[i];
            if (section.TopPixelOffset != expectedTopOffset || section.Height < 1)
            {
                throw new ArgumentException(
                    "Sections must be contiguous, ordered, and at least one pixel high.",
                    nameof(sections));
            }

            expectedTopOffset = section.EndPixelOffset;
        }

        if (expectedTopOffset != totalPixelHeight)
        {
            throw new ArgumentException(
                "Sections must cover the full image height.",
                nameof(sections));
        }
    }

    private static bool Capture(
        StageCapturePlan plan,
        IReadOnlyList<string> outputPaths,
        IReadOnlyList<StageCaptureSection> sections,
        bool deleteOutputsOnFailure)
    {
        ValidateSections(plan.Height, sections);
        if (outputPaths == null || outputPaths.Count != sections.Count)
        {
            throw new ArgumentException("Output path count is invalid.", nameof(outputPaths));
        }

        Texture2D image = null;
        List<string> createdOutputPaths = new List<string>();

        try
        {
            image = RenderImage(plan, true, out bool cancelled);
            if (cancelled)
            {
                return false;
            }

            SaveOutputImages(image, outputPaths, sections, createdOutputPaths);
            return true;
        }
        catch
        {
            if (deleteOutputsOnFailure)
            {
                DeleteGeneratedFiles(createdOutputPaths);
            }

            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (image != null)
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }

    private static Texture2D RenderImage(
        StageCapturePlan plan,
        bool allowCancellation,
        out bool cancelled)
    {
        Texture2D image = null;
        GameObject captureCameraObject = null;
        RenderTexture previousActive = RenderTexture.active;
        bool completed = false;
        cancelled = false;

        try
        {
            image = new Texture2D(plan.Width, plan.Height, TextureFormat.RGB24, false)
            {
                name = "Stage Capture Buffer",
                hideFlags = HideFlags.HideAndDontSave
            };

            captureCameraObject = new GameObject("Stage Capture Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Camera captureCamera = captureCameraObject.AddComponent<Camera>();
            captureCamera.CopyFrom(plan.SourceCamera);
            captureCamera.enabled = false;
            captureCamera.rect = new Rect(0f, 0f, 1f, 1f);
            captureCamera.useOcclusionCulling = false;
            captureCamera.transform.rotation = plan.SourceCamera.transform.rotation;

            float pixelsPerWorldUnit = plan.Width / plan.Area.width;
            int tileIndex = 0;
            int tileCount = Mathf.CeilToInt(plan.Height / (float)plan.TileHeight);

            for (int destinationY = 0; destinationY < plan.Height; destinationY += plan.TileHeight)
            {
                if (allowCancellation &&
                    EditorUtility.DisplayCancelableProgressBar(
                        "Stage Portrait Capture",
                        $"타일 렌더링 중 ({tileIndex + 1}/{tileCount})",
                        tileIndex / (float)Mathf.Max(1, tileCount)))
                {
                    cancelled = true;
                    return null;
                }

                int currentTileHeight = Mathf.Min(plan.TileHeight, plan.Height - destinationY);
                RenderTile(
                    plan,
                    captureCamera,
                    image,
                    destinationY,
                    currentTileHeight,
                    pixelsPerWorldUnit);
                tileIndex++;
            }

            image.Apply(false, false);
            completed = true;
            return image;
        }
        finally
        {
            RenderTexture.active = previousActive;
            if (captureCameraObject != null)
            {
                UnityEngine.Object.DestroyImmediate(captureCameraObject);
            }

            if (!completed && image != null)
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }

    private static void SaveOutputImages(
        Texture2D fullImage,
        IReadOnlyList<string> outputPaths,
        IReadOnlyList<StageCaptureSection> sections,
        List<string> createdOutputPaths)
    {
        if (outputPaths.Count == 1)
        {
            EditorUtility.DisplayProgressBar("Stage Portrait Capture", "PNG 인코딩 중 (1/1)", 0.95f);
            WritePng(fullImage, outputPaths[0], createdOutputPaths);
            return;
        }

        NativeArray<byte> fullImageData = fullImage.GetRawTextureData<byte>();
        int bytesPerRow = fullImage.width * 3;

        for (int sectionIndex = 0; sectionIndex < outputPaths.Count; sectionIndex++)
        {
            EditorUtility.DisplayProgressBar(
                "Stage Portrait Capture",
                $"분할 PNG 인코딩 중 ({sectionIndex + 1}/{outputPaths.Count})",
                0.9f + 0.1f * sectionIndex / outputPaths.Count);

            StageCaptureSection section = sections[sectionIndex];
            int sectionHeight = section.Height;
            int sourceY = fullImage.height - section.EndPixelOffset;
            Texture2D sectionImage = new Texture2D(
                fullImage.width,
                sectionHeight,
                TextureFormat.RGB24,
                false);
            sectionImage.name = $"Stage Capture Section {sectionIndex + 1}";

            try
            {
                NativeArray<byte> sectionData = sectionImage.GetRawTextureData<byte>();
                NativeArray<byte>.Copy(
                    fullImageData,
                    sourceY * bytesPerRow,
                    sectionData,
                    0,
                    sectionHeight * bytesPerRow);
                sectionImage.Apply(false, false);
                WritePng(sectionImage, outputPaths[sectionIndex], createdOutputPaths);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sectionImage);
            }
        }
    }

    private static void WritePng(
        Texture2D image,
        string outputPath,
        List<string> createdOutputPaths)
    {
        byte[] pngBytes = image.EncodeToPNG();
        if (pngBytes == null || pngBytes.Length == 0)
        {
            throw new InvalidOperationException("Unity가 PNG 데이터를 생성하지 못했습니다.");
        }

        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        createdOutputPaths.Add(outputPath);
        File.WriteAllBytes(outputPath, pngBytes);
    }

    private static string[] CreateUniqueOutputPaths(
        string outputDirectory,
        string baseFileName,
        int sectionCount)
    {
        int suffix = 1;
        while (true)
        {
            string candidateBaseName = suffix == 1
                ? baseFileName
                : $"{baseFileName}-{suffix}";
            string[] paths = CreateOutputPaths(
                outputDirectory,
                candidateBaseName,
                sectionCount);
            bool hasCollision = false;
            for (int i = 0; i < paths.Length; i++)
            {
                if (File.Exists(paths[i]))
                {
                    hasCollision = true;
                    break;
                }
            }

            if (!hasCollision)
            {
                return paths;
            }

            suffix++;
        }
    }

    private static string[] CreateOutputPaths(
        string outputDirectory,
        string baseFileName,
        int sectionCount)
    {
        if (sectionCount == 1)
        {
            return new[] { Path.Combine(outputDirectory, $"{baseFileName}.png") };
        }

        int numberWidth = Mathf.Max(2, sectionCount.ToString().Length);
        string countLabel = sectionCount.ToString($"D{numberWidth}");
        string[] paths = new string[sectionCount];
        for (int sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
        {
            string partLabel = (sectionIndex + 1).ToString($"D{numberWidth}");
            paths[sectionIndex] = Path.Combine(
                outputDirectory,
                $"{baseFileName}-part-{partLabel}-of-{countLabel}.png");
        }

        return paths;
    }

    private static string SanitizeFileName(string fileName)
    {
        string sanitized = Path.GetFileNameWithoutExtension(fileName);
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidCharacters.Length; i++)
        {
            sanitized = sanitized.Replace(invalidCharacters[i], '_');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "Stage-portrait" : sanitized;
    }

    private static void DeleteGeneratedFiles(IReadOnlyList<string> paths)
    {
        for (int i = 0; i < paths.Count; i++)
        {
            try
            {
                if (File.Exists(paths[i]))
                {
                    File.Delete(paths[i]);
                }
            }
            catch (Exception cleanupException)
            {
                Debug.LogWarning(
                    $"Failed to clean up incomplete stage capture: {paths[i]}\n{cleanupException.Message}");
            }
        }
    }

    private static void RenderTile(
        StageCapturePlan plan,
        Camera captureCamera,
        Texture2D image,
        int destinationY,
        int currentTileHeight,
        float pixelsPerWorldUnit)
    {
        float tileWorldHeight = currentTileHeight / pixelsPerWorldUnit;
        float tileCenterY = plan.Area.yMin + (destinationY + currentTileHeight * 0.5f) / pixelsPerWorldUnit;
        Vector3 localCameraPosition = new Vector3(plan.Area.center.x, tileCenterY, 0f);

        captureCamera.transform.position = plan.SourceCamera.transform.TransformPoint(localCameraPosition);
        captureCamera.orthographicSize = tileWorldHeight * 0.5f;
        captureCamera.aspect = plan.Width / (float)currentTileHeight;

        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(
            plan.Width,
            currentTileHeight,
            RenderTextureFormat.ARGB32,
            24)
        {
            msaaSamples = plan.AntiAliasing,
            sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
            useMipMap = false,
            autoGenerateMips = false
        };

        descriptor.msaaSamples = Mathf.Max(
            1,
            SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));

        RenderTexture tileTexture = RenderTexture.GetTemporary(descriptor);
        RenderTexture resolvedTexture = null;
        try
        {
            captureCamera.targetTexture = tileTexture;
            RenderTexture.active = tileTexture;
            captureCamera.Render();

            RenderTexture readTexture = tileTexture;
            if (descriptor.msaaSamples > 1)
            {
                RenderTextureDescriptor resolvedDescriptor = descriptor;
                resolvedDescriptor.depthBufferBits = 0;
                resolvedDescriptor.msaaSamples = 1;
                resolvedTexture = RenderTexture.GetTemporary(resolvedDescriptor);
                Graphics.Blit(tileTexture, resolvedTexture);
                readTexture = resolvedTexture;
            }

            RenderTexture.active = readTexture;
            image.ReadPixels(
                new Rect(0, 0, plan.Width, currentTileHeight),
                0,
                destinationY,
                false);
        }
        finally
        {
            captureCamera.targetTexture = null;
            RenderTexture.active = null;
            if (resolvedTexture != null)
            {
                RenderTexture.ReleaseTemporary(resolvedTexture);
            }

            RenderTexture.ReleaseTemporary(tileTexture);
        }
    }

    private static List<Renderer> GetCandidateRenderers(Scene scene, Transform contentRoot)
    {
        List<Renderer> renderers = new List<Renderer>();
        if (contentRoot != null)
        {
            contentRoot.GetComponentsInChildren(false, renderers);
            return renderers;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Renderer[] rootRenderers = roots[i].GetComponentsInChildren<Renderer>(false);
            renderers.AddRange(rootRenderers);
        }

        return renderers;
    }

    private static void AddAdjacentStageBoundaryRenderers(
        Camera camera,
        Transform contentRoot,
        Rect contentArea,
        List<Renderer> renderers)
    {
        float horizontalSearchDistance = Mathf.Clamp(
            contentArea.width * BoundarySearchWidthRatio,
            MinimumBoundarySearchDistance,
            MaximumBoundarySearchDistance);
        Rect horizontalSearchArea = Rect.MinMaxRect(
            contentArea.xMin - horizontalSearchDistance,
            contentArea.yMin,
            contentArea.xMax + horizontalSearchDistance,
            contentArea.yMax);

        HashSet<Renderer> includedRenderers = new HashSet<Renderer>(renderers);
        Renderer[] stageRenderers = contentRoot.root.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < stageRenderers.Length; i++)
        {
            Renderer renderer = stageRenderers[i];
            if (includedRenderers.Contains(renderer) || !ShouldIncludeRenderer(camera, renderer))
            {
                continue;
            }

            Rect rendererArea = GetCameraSpaceBounds(camera, renderer);
            bool overlapsVertically =
                rendererArea.yMax >= contentArea.yMin && rendererArea.yMin <= contentArea.yMax;
            bool touchesContentHorizontally =
                rendererArea.xMax >= horizontalSearchArea.xMin &&
                rendererArea.xMin <= horizontalSearchArea.xMax;
            bool extendsHorizontalBoundary =
                rendererArea.xMin < contentArea.xMin || rendererArea.xMax > contentArea.xMax;
            bool isReasonableBoundaryWidth =
                rendererArea.width <= contentArea.width + MinimumWorldSize;

            if (!overlapsVertically ||
                !touchesContentHorizontally ||
                !extendsHorizontalBoundary ||
                !isReasonableBoundaryWidth)
            {
                continue;
            }

            renderers.Add(renderer);
            includedRenderers.Add(renderer);
        }
    }

    private static bool TryCalculateCameraSpaceBounds(
        Camera camera,
        List<Renderer> renderers,
        float padding,
        out Rect area,
        out int includedCount)
    {
        bool hasBounds = false;
        float minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        float minimumY = float.PositiveInfinity;
        float maximumY = float.NegativeInfinity;
        includedCount = 0;

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (!ShouldIncludeRenderer(camera, renderer))
            {
                continue;
            }

            Rect rendererArea = GetCameraSpaceBounds(camera, renderer);
            minimumX = Mathf.Min(minimumX, rendererArea.xMin);
            maximumX = Mathf.Max(maximumX, rendererArea.xMax);
            minimumY = Mathf.Min(minimumY, rendererArea.yMin);
            maximumY = Mathf.Max(maximumY, rendererArea.yMax);

            hasBounds = true;
            includedCount++;
        }

        if (!hasBounds)
        {
            area = default;
            return false;
        }

        minimumX -= padding;
        maximumX += padding;
        minimumY -= padding;
        maximumY += padding;

        float width = Mathf.Max(MinimumWorldSize, maximumX - minimumX);
        float height = Mathf.Max(MinimumWorldSize, maximumY - minimumY);
        area = new Rect(minimumX, minimumY, width, height);
        return true;
    }

    private static Rect GetCameraSpaceBounds(Camera camera, Renderer renderer)
    {
        Bounds bounds = renderer.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float minimumX = float.PositiveInfinity;
        float maximumX = float.NegativeInfinity;
        float minimumY = float.PositiveInfinity;
        float maximumY = float.NegativeInfinity;

        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 worldCorner = center + new Vector3(
                (corner & 1) == 0 ? -extents.x : extents.x,
                (corner & 2) == 0 ? -extents.y : extents.y,
                (corner & 4) == 0 ? -extents.z : extents.z);
            Vector3 cameraSpaceCorner = camera.transform.InverseTransformPoint(worldCorner);
            minimumX = Mathf.Min(minimumX, cameraSpaceCorner.x);
            maximumX = Mathf.Max(maximumX, cameraSpaceCorner.x);
            minimumY = Mathf.Min(minimumY, cameraSpaceCorner.y);
            maximumY = Mathf.Max(maximumY, cameraSpaceCorner.y);
        }

        return Rect.MinMaxRect(minimumX, minimumY, maximumX, maximumY);
    }

    private static bool ShouldIncludeRenderer(Camera camera, Renderer renderer)
    {
        if (renderer == null ||
            !renderer.enabled ||
            renderer.forceRenderingOff ||
            !renderer.gameObject.activeInHierarchy)
        {
            return false;
        }

        int layerMask = 1 << renderer.gameObject.layer;
        return (camera.cullingMask & layerMask) != 0;
    }

    private static int NormalizeAntiAliasing(int value)
    {
        if (value >= 8)
        {
            return 8;
        }

        if (value >= 4)
        {
            return 4;
        }

        if (value >= 2)
        {
            return 2;
        }

        return 1;
    }
}
