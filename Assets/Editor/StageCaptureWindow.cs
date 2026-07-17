using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 열린 2D 스테이지의 전체 Renderer 영역을 한 장의 세로형 PNG로 저장합니다.
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
    [SerializeField] private int tileHeight = DefaultTileHeight;
    [SerializeField] private int antiAliasing = 4;
    [SerializeField] private float padding = DefaultPadding;
    [SerializeField] private bool revealAfterCapture = true;

    [MenuItem("Tools/Jump Timing/Stage Capture", false, 70)]
    public static void OpenWindow()
    {
        StageCaptureWindow window = GetWindow<StageCaptureWindow>("Stage Capture");
        window.minSize = new Vector2(430f, 410f);
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
            "현재 열린 2D 스테이지의 활성 Renderer 범위를 자동으로 계산하고, 월드 비율을 유지한 한 장의 고해상도 PNG로 저장합니다.",
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
        padding = Mathf.Max(0f, EditorGUILayout.FloatField("World Padding", padding));
        tileHeight = Mathf.Max(256, EditorGUILayout.IntField("Render Tile Height", tileHeight));
        antiAliasing = DrawAntiAliasingPopup(antiAliasing);
        revealAfterCapture = EditorGUILayout.Toggle("Reveal After Capture", revealAfterCapture);

        EditorGUILayout.Space(10f);
        bool canCapture = StageCaptureUtility.TryCreatePlan(
            sourceCamera,
            contentRoot,
            outputWidth,
            tileHeight,
            antiAliasing,
            padding,
            out StageCapturePlan plan,
            out string error);

        if (canCapture)
        {
            DrawPlanSummary(plan);
        }
        else
        {
            EditorGUILayout.HelpBox(error, MessageType.Warning);
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!canCapture))
        {
            if (GUILayout.Button("Capture Stage PNG...", GUILayout.Height(38f)))
            {
                Capture(plan);
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

    private static void DrawPlanSummary(StageCapturePlan plan)
    {
        double imageMemoryMegabytes = plan.Width * (double)plan.Height * 3d / (1024d * 1024d);
        EditorGUILayout.LabelField("Capture Preview", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Active Scene", plan.Scene.name);
        EditorGUILayout.LabelField("Included Renderers", plan.RendererCount.ToString("N0"));
        EditorGUILayout.LabelField("World Size", $"{plan.Area.width:F2} × {plan.Area.height:F2}");
        EditorGUILayout.LabelField("PNG Resolution", $"{plan.Width:N0} × {plan.Height:N0}");
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

    private void Capture(StageCapturePlan plan)
    {
        string sceneName = string.IsNullOrEmpty(plan.Scene.name) ? "Stage" : plan.Scene.name;
        string fileName = $"{sceneName}-portrait-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        string directory = EditorPrefs.GetString(LastSaveDirectoryKey, GetProjectRoot());
        if (!Directory.Exists(directory))
        {
            directory = GetProjectRoot();
        }

        string path = EditorUtility.SaveFilePanel(
            "Save Stage Portrait PNG",
            directory,
            fileName,
            "png");

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (!StageCaptureUtility.Capture(plan, path))
            {
                Debug.Log("Stage capture cancelled.");
                return;
            }

            string saveDirectory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(saveDirectory))
            {
                EditorPrefs.SetString(LastSaveDirectoryKey, saveDirectory);
            }

            ImportIfInsideAssets(path);
            Debug.Log($"Stage portrait saved: {path} ({plan.Width}x{plan.Height})");

            if (revealAfterCapture)
            {
                EditorUtility.RevealInFinder(path);
            }

            EditorUtility.DisplayDialog(
                "Stage Capture Complete",
                $"{plan.Width:N0} × {plan.Height:N0} PNG 저장 완료\n\n{path}",
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

        Texture2D image = null;
        GameObject captureCameraObject = null;
        RenderTexture previousActive = RenderTexture.active;

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

            EditorUtility.DisplayProgressBar("Stage Portrait Capture", "PNG 인코딩 중...", 0.95f);
            image.Apply(false, false);
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

            File.WriteAllBytes(outputPath, pngBytes);
            return true;
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
