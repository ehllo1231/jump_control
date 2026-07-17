using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
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
    private const string LastSaveDirectoryKey = "JumpTiming.StageCapture.LastSaveDirectory";

    [SerializeField] private Camera sourceCamera;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private int outputWidth = DefaultOutputWidth;
    [SerializeField] private int verticalSectionCount = 1;
    [SerializeField] private int tileHeight = DefaultTileHeight;
    [SerializeField] private int antiAliasing = 4;
    [SerializeField] private float padding = DefaultPadding;
    [SerializeField] private bool revealAfterCapture = true;

    [MenuItem("Tools/Jump Timing/Stage Capture", false, 70)]
    public static void OpenWindow()
    {
        StageCaptureWindow window = GetWindow<StageCaptureWindow>("Stage Capture");
        window.minSize = new Vector2(430f, 500f);
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

        EditorApplication.hierarchyChanged += Repaint;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= Repaint;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Stage Portrait Capture", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "현재 열린 2D 스테이지의 활성 Renderer 범위를 자동으로 계산하고, 전체 세로 길이를 원하는 개수의 고해상도 PNG로 저장합니다.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        sourceCamera = EditorGUILayout.ObjectField(
            "Source Camera",
            sourceCamera,
            typeof(Camera),
            true) as Camera;

        if (GUILayout.Button("Use Active Scene Camera"))
        {
            sourceCamera = FindDefaultCamera();
        }

        contentRoot = EditorGUILayout.ObjectField(
            "Content Root (Optional)",
            contentRoot,
            typeof(Transform),
            true) as Transform;

        if (GUILayout.Button("Auto Detect Content Root"))
        {
            contentRoot = FindDefaultContentRoot();
        }

        EditorGUILayout.HelpBox(
            "Content Root는 이미지 영역만 결정합니다. 자동 선택된 배경 루트를 사용하면 멀리 떨어진 안전용 바닥은 제외하면서, 영역 안의 Platform과 Player는 함께 렌더링됩니다.",
            MessageType.None);

        EditorGUILayout.Space(6f);
        outputWidth = Mathf.Max(256, EditorGUILayout.IntField("PNG Width", outputWidth));
        verticalSectionCount = Mathf.Max(
            1,
            EditorGUILayout.IntField("Vertical Sections", verticalSectionCount));
        EditorGUILayout.HelpBox(
            "1이면 전체를 한 장으로 저장합니다. 2 이상이면 전체 세로 길이를 위에서 아래 순서로 나눠 저장합니다.",
            MessageType.None);
        padding = Mathf.Max(0f, EditorGUILayout.FloatField("World Padding", padding));
        tileHeight = Mathf.Max(256, EditorGUILayout.IntField("Render Tile Height", tileHeight));
        antiAliasing = DrawAntiAliasingPopup(antiAliasing);
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

        bool hasValidSectionCount = hasPlan && verticalSectionCount <= plan.Height;
        if (hasPlan)
        {
            DrawPlanSummary(plan, verticalSectionCount);
            if (!hasValidSectionCount)
            {
                EditorGUILayout.HelpBox(
                    $"Vertical Sections는 전체 이미지 높이({plan.Height:N0}px) 이하여야 합니다.",
                    MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.HelpBox(error, MessageType.Warning);
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!hasValidSectionCount))
        {
            if (GUILayout.Button("Capture Stage PNG(s)...", GUILayout.Height(38f)))
            {
                Capture(plan, verticalSectionCount);
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Screen Space - Overlay Canvas와 Scene View의 편집용 선/아이콘은 카메라 이미지에 포함되지 않습니다.",
            MessageType.None);
    }

    private static int DrawAntiAliasingPopup(int currentValue)
    {
        int[] values = { 1, 2, 4, 8 };
        string[] labels = { "Off", "2x", "4x", "8x" };
        int currentIndex = Array.IndexOf(values, currentValue);
        currentIndex = Mathf.Max(0, currentIndex);
        return values[EditorGUILayout.Popup("Anti Aliasing", currentIndex, labels)];
    }

    private static void DrawPlanSummary(StageCapturePlan plan, int sectionCount)
    {
        double imageMemoryMegabytes = plan.Width * (double)plan.Height * 3d / (1024d * 1024d);
        EditorGUILayout.LabelField("Capture Preview", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Active Scene", plan.Scene.name);
        EditorGUILayout.LabelField("Included Renderers", plan.RendererCount.ToString("N0"));
        EditorGUILayout.LabelField("World Size", $"{plan.Area.width:F2} × {plan.Area.height:F2}");
        EditorGUILayout.LabelField("Full Resolution", $"{plan.Width:N0} × {plan.Height:N0}");
        EditorGUILayout.LabelField("Output Files", sectionCount.ToString("N0"));
        if (sectionCount <= plan.Height)
        {
            int maximumSectionHeight = StageCaptureUtility.GetSectionHeight(
                plan.Height,
                sectionCount,
                0);
            int minimumSectionHeight = plan.Height / sectionCount;
            EditorGUILayout.LabelField(
                "Section Resolution",
                minimumSectionHeight == maximumSectionHeight
                    ? $"{plan.Width:N0} × {minimumSectionHeight:N0}"
                    : $"{plan.Width:N0} × {minimumSectionHeight:N0}~{maximumSectionHeight:N0}");
        }
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

    private void Capture(StageCapturePlan plan, int sectionCount)
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
                    sectionCount,
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

            int minimumSectionHeight = plan.Height / outputPaths.Length;
            int maximumSectionHeight = StageCaptureUtility.GetSectionHeight(
                plan.Height,
                outputPaths.Length,
                0);
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

    public static bool Capture(StageCapturePlan plan, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is empty.", nameof(outputPath));
        }

        return Capture(plan, new[] { outputPath }, false);
    }

    public static bool CaptureSections(
        StageCapturePlan plan,
        string outputDirectory,
        string baseFileName,
        int sectionCount,
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

        if (sectionCount < 1 || sectionCount > plan.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sectionCount),
                $"Section count must be between 1 and {plan.Height:N0}.");
        }

        outputPaths = CreateUniqueOutputPaths(
            outputDirectory,
            SanitizeFileName(baseFileName),
            sectionCount);
        return Capture(plan, outputPaths, true);
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

    private static bool Capture(
        StageCapturePlan plan,
        IReadOnlyList<string> outputPaths,
        bool deleteOutputsOnFailure)
    {
        if (outputPaths == null || outputPaths.Count < 1 || outputPaths.Count > plan.Height)
        {
            throw new ArgumentException("Output path count is invalid.", nameof(outputPaths));
        }

        Texture2D image = null;
        GameObject captureCameraObject = null;
        RenderTexture previousActive = RenderTexture.active;
        List<string> createdOutputPaths = new List<string>();

        try
        {
            image = new Texture2D(plan.Width, plan.Height, TextureFormat.RGB24, false);
            image.name = "Stage Capture Buffer";

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
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Stage Portrait Capture",
                        $"타일 렌더링 중 ({tileIndex + 1}/{tileCount})",
                        tileIndex / (float)Mathf.Max(1, tileCount)))
                {
                    return false;
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
            SaveOutputImages(image, outputPaths, createdOutputPaths);
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
            RenderTexture.active = previousActive;
            EditorUtility.ClearProgressBar();

            if (captureCameraObject != null)
            {
                UnityEngine.Object.DestroyImmediate(captureCameraObject);
            }

            if (image != null)
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }

    private static void SaveOutputImages(
        Texture2D fullImage,
        IReadOnlyList<string> outputPaths,
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
        int topPixelOffset = 0;

        for (int sectionIndex = 0; sectionIndex < outputPaths.Count; sectionIndex++)
        {
            EditorUtility.DisplayProgressBar(
                "Stage Portrait Capture",
                $"분할 PNG 인코딩 중 ({sectionIndex + 1}/{outputPaths.Count})",
                0.9f + 0.1f * sectionIndex / outputPaths.Count);

            int sectionHeight = GetSectionHeight(
                fullImage.height,
                outputPaths.Count,
                sectionIndex);
            int sourceY = fullImage.height - topPixelOffset - sectionHeight;
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

            topPixelOffset += sectionHeight;
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
