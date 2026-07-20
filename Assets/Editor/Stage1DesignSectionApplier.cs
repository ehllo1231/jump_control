using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class Stage1DesignSectionApplier
{
    private const string ScenePath = "Assets/Scenes/MVPJumpScene.unity";
    private const string DesignDirectory = "Assets/Art/graybox_design/stage1/stage1-1_design";
    private const string StructureReferencePath = DesignDirectory + "/1-1그레이박스.png";
    private const string TestRootName = "DesignTest_Section_01_of_05";
    private const int ReferenceWidth = 2160;
    private const int ReferenceHeight = 1515;
    private const int BaseSortingOrder = 5;
    private const int DetailSortingOrder = 6;
    private const float PixelsPerUnit = 100f;
    private const float SectionWorldLeft = -31.485327f;
    private const float SectionWorldRight = 29.92495f;
    private const float SectionWorldBottom = -6.97415f;
    private const float OriginalSectionWorldTop = 38.83467f;
    private const int OriginalReferenceHeight = 1611;
    private const int CroppedTopPixels = 96;

    private static readonly PartPlacement[] PartPlacements =
    {
        new PartPlacement(
            "Parts1",
            DesignDirectory + "/parts1.png",
            new Vector2Int(1322, 1056),
            new RectInt(9, 8, 1304, 1039),
            new RectInt(368, 0, 1775, 1498),
            BaseSortingOrder,
            stretchToTargetHeight: true),
        new PartPlacement(
            "Parts2",
            DesignDirectory + "/parts2.png",
            new Vector2Int(370, 918),
            new RectInt(9, 8, 352, 901),
            new RectInt(1219, 0, 468, 1213),
            DetailSortingOrder,
            stretchToTargetHeight: true),
        new PartPlacement(
            "Parts3",
            DesignDirectory + "/parts3.png",
            new Vector2Int(347, 608),
            new RectInt(9, 9, 329, 590),
            new RectInt(1034, 51, 422, 775),
            DetailSortingOrder,
            stretchToTargetHeight: true),
        new PartPlacement(
            "Parts4",
            DesignDirectory + "/parts4.png",
            new Vector2Int(107, 46),
            new RectInt(9, 9, 89, 28),
            new RectInt(1194, 178, 112, 26),
            DetailSortingOrder,
            stretchToTargetHeight: false),
        new PartPlacement(
            "Parts5",
            DesignDirectory + "/parts5.png",
            new Vector2Int(105, 46),
            new RectInt(9, 9, 87, 28),
            new RectInt(1364, 250, 112, 27),
            DetailSortingOrder,
            stretchToTargetHeight: false)
    };

    private readonly struct PartPlacement
    {
        public PartPlacement(
            string objectName,
            string assetPath,
            Vector2Int expectedSize,
            RectInt contentPixels,
            RectInt targetPixels,
            int sortingOrder,
            bool stretchToTargetHeight)
        {
            ObjectName = objectName;
            AssetPath = assetPath;
            ExpectedSize = expectedSize;
            ContentPixels = contentPixels;
            TargetPixels = targetPixels;
            SortingOrder = sortingOrder;
            StretchToTargetHeight = stretchToTargetHeight;
        }

        public string ObjectName { get; }
        public string AssetPath { get; }
        public Vector2Int ExpectedSize { get; }
        public RectInt ContentPixels { get; }
        public RectInt TargetPixels { get; }
        public int SortingOrder { get; }
        public bool StretchToTargetHeight { get; }
    }

    [MenuItem("Tools/Jump Timing/Stage1 Design/Apply Section 1 Test", false, 80)]
    public static void ApplySectionOne()
    {
        ApplySectionOneInternal(saveScene: true, capturePreview: false);
    }

    [MenuItem("Tools/Jump Timing/Stage1 Design/Rollback Section 1 Test", false, 81)]
    public static void RollbackSectionOne()
    {
        Scene scene = OpenTargetScene();
        Transform stageArt = FindStageArt(scene);
        Transform existingRoot = stageArt.Find(TestRootName);
        if (existingRoot == null)
        {
            Debug.Log($"Stage1 design rollback skipped: {TestRootName} does not exist.");
            return;
        }

        Object.DestroyImmediate(existingRoot.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"Rolled back Stage1 design test root: {TestRootName}");
    }

    public static void ApplySectionOneBatch()
    {
        ApplySectionOneInternal(saveScene: true, capturePreview: true);
    }

    private static void ApplySectionOneInternal(bool saveScene, bool capturePreview)
    {
        Scene scene = OpenTargetScene();
        Transform collisionRoot = FindRequiredTransform(scene, "Stage1/Collision");
        string collisionStateBefore = CaptureCollisionState(collisionRoot);

        Rect sectionArea = CreateReferenceSectionArea();
        Sprite[] partSprites = EnsurePartSprites();

        Transform stageArt = FindStageArt(scene);
        Transform testRoot = RebuildTestRoot(stageArt);
        CreatePartObjects(testRoot, partSprites, sectionArea);
        ValidatePartObjects(testRoot);

        string collisionStateAfter = CaptureCollisionState(collisionRoot);
        if (!string.Equals(collisionStateBefore, collisionStateAfter, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Stage1 Collision changed while applying design. The scene was not saved.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (saveScene)
        {
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        if (capturePreview)
        {
            CaptureFirstSectionPreview(scene, sectionArea);
        }

        Debug.Log(
            $"Applied {PartPlacements.Length} design parts to " +
            $"Stage1/ForegroundDecor/StageArt/{TestRootName}. " +
            $"World area: x {sectionArea.xMin:F3}..{sectionArea.xMax:F3}, " +
            $"y {sectionArea.yMin:F3}..{sectionArea.yMax:F3}. " +
            "Stage1 Collision state remained unchanged.");
    }

    private static Scene OpenTargetScene()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.IsValid() && string.Equals(activeScene.path, ScenePath, StringComparison.Ordinal))
        {
            return activeScene;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            throw new InvalidOperationException("Scene switch was cancelled.");
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static Sprite[] EnsurePartSprites()
    {
        if (!File.Exists(StructureReferencePath))
        {
            throw new FileNotFoundException($"The graybox reference is required: {StructureReferencePath}");
        }

        Sprite[] sprites = new Sprite[PartPlacements.Length];
        for (int index = 0; index < PartPlacements.Length; index++)
        {
            sprites[index] = EnsurePartSprite(PartPlacements[index]);
        }

        return sprites;
    }

    private static Sprite EnsurePartSprite(PartPlacement placement)
    {
        if (!File.Exists(placement.AssetPath))
        {
            throw new FileNotFoundException($"Stage1 design part is missing: {placement.AssetPath}");
        }

        Vector2Int actualSize = ReadPngSize(placement.AssetPath);
        if (actualSize != placement.ExpectedSize)
        {
            throw new InvalidOperationException(
                $"Expected {placement.AssetPath} to be {placement.ExpectedSize.x}x{placement.ExpectedSize.y}, " +
                $"but it is {actualSize.x}x{actualSize.y}.");
        }

        AssetDatabase.ImportAsset(placement.AssetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(placement.AssetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException(
                $"Texture importer was not created for {placement.AssetPath}.");
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(placement.AssetPath);
        if (sprite == null)
        {
            throw new InvalidOperationException($"Sprite import failed for {placement.AssetPath}.");
        }

        return sprite;
    }

    private static Rect CreateReferenceSectionArea()
    {
        Vector2Int structureSize = ReadPngSize(StructureReferencePath);
        if (structureSize.x != ReferenceWidth || structureSize.y != ReferenceHeight)
        {
            throw new InvalidOperationException(
                $"The expected reference size is {ReferenceWidth}x{ReferenceHeight}, but " +
                $"{StructureReferencePath} is {structureSize.x}x{structureSize.y}.");
        }

        float originalWorldHeight = OriginalSectionWorldTop - SectionWorldBottom;
        float croppedWorldTop = OriginalSectionWorldTop -
                                CroppedTopPixels * originalWorldHeight / OriginalReferenceHeight;
        return Rect.MinMaxRect(
            SectionWorldLeft,
            SectionWorldBottom,
            SectionWorldRight,
            croppedWorldTop);
    }

    private static Transform RebuildTestRoot(Transform stageArt)
    {
        Transform existingRoot = stageArt.Find(TestRootName);
        if (existingRoot != null)
        {
            Object.DestroyImmediate(existingRoot.gameObject);
        }

        GameObject testRootObject = new GameObject(TestRootName);
        testRootObject.transform.SetParent(stageArt, false);
        testRootObject.transform.SetAsFirstSibling();
        return testRootObject.transform;
    }

    private static void CreatePartObjects(
        Transform testRoot,
        IReadOnlyList<Sprite> sprites,
        Rect sectionArea)
    {
        float worldPerTargetPixelX = sectionArea.width / ReferenceWidth;
        float worldPerTargetPixelY = sectionArea.height / ReferenceHeight;

        for (int index = 0; index < PartPlacements.Length; index++)
        {
            PartPlacement placement = PartPlacements[index];
            Sprite sprite = sprites[index];
            float targetPixelsPerSourcePixelX =
                (float)placement.TargetPixels.width / placement.ContentPixels.width;
            float localScaleX = targetPixelsPerSourcePixelX *
                                worldPerTargetPixelX * sprite.pixelsPerUnit;

            float localScaleY;
            float targetPixelsPerSourcePixelY;
            if (placement.StretchToTargetHeight)
            {
                targetPixelsPerSourcePixelY =
                    (float)placement.TargetPixels.height / placement.ContentPixels.height;
                localScaleY = targetPixelsPerSourcePixelY *
                              worldPerTargetPixelY * sprite.pixelsPerUnit;
            }
            else
            {
                localScaleY = localScaleX;
                targetPixelsPerSourcePixelY =
                    localScaleY / (sprite.pixelsPerUnit * worldPerTargetPixelY);
            }

            float spriteCenterFromContentLeft =
                sprite.rect.width * 0.5f - placement.ContentPixels.xMin;
            float spriteCenterFromContentTop =
                sprite.rect.height * 0.5f - placement.ContentPixels.yMin;
            float targetCenterPixelX = placement.TargetPixels.xMin +
                                       spriteCenterFromContentLeft * targetPixelsPerSourcePixelX;
            float targetCenterPixelY = placement.TargetPixels.yMin +
                                       spriteCenterFromContentTop * targetPixelsPerSourcePixelY;

            GameObject partObject = new GameObject(placement.ObjectName);
            partObject.transform.SetParent(testRoot, false);
            partObject.transform.position = new Vector3(
                sectionArea.xMin + targetCenterPixelX * worldPerTargetPixelX,
                sectionArea.yMax - targetCenterPixelY * worldPerTargetPixelY,
                0f);
            partObject.transform.localScale = new Vector3(localScaleX, localScaleY, 1f);

            SpriteRenderer renderer = partObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = placement.SortingOrder;
            renderer.color = Color.white;
        }
    }

    private static void ValidatePartObjects(Transform testRoot)
    {
        SpriteRenderer[] renderers = testRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length != PartPlacements.Length || testRoot.childCount != PartPlacements.Length)
        {
            throw new InvalidOperationException(
                $"Expected exactly {PartPlacements.Length} Stage1 design parts, but found " +
                $"{renderers.Length} renderers and {testRoot.childCount} direct children.");
        }

        if (testRoot.GetComponentInChildren<Collider2D>(true) != null ||
            testRoot.GetComponentInChildren<Rigidbody2D>(true) != null ||
            testRoot.GetComponentInChildren<Platform2D>(true) != null)
        {
            throw new InvalidOperationException(
                "Stage1 design parts must not contain Collider2D, Rigidbody2D, or Platform2D components.");
        }
    }

    private static Transform FindStageArt(Scene scene)
    {
        return FindRequiredTransform(scene, "Stage1/ForegroundDecor/StageArt");
    }

    private static Transform FindRequiredTransform(Scene scene, string path)
    {
        string[] segments = path.Split('/');
        Transform current = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, segments[0], StringComparison.Ordinal))
            {
                current = root.transform;
                break;
            }
        }

        for (int index = 1; current != null && index < segments.Length; index++)
        {
            current = current.Find(segments[index]);
        }

        if (current == null)
        {
            throw new InvalidOperationException($"Required scene hierarchy was not found: {path}");
        }

        return current;
    }

    private static Camera FindSceneCamera(Scene scene)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.gameObject.scene == scene)
        {
            return mainCamera;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera camera = root.GetComponentInChildren<Camera>(false);
            if (camera != null)
            {
                return camera;
            }
        }

        throw new InvalidOperationException("An active scene camera was not found.");
    }

    private static Vector2Int ReadPngSize(string assetPath)
    {
        byte[] bytes = File.ReadAllBytes(assetPath);
        if (bytes.Length < 24 ||
            bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
        {
            throw new InvalidDataException($"Not a valid PNG file: {assetPath}");
        }

        int width = ReadBigEndianInt32(bytes, 16);
        int height = ReadBigEndianInt32(bytes, 20);
        return new Vector2Int(width, height);
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) |
               (bytes[offset + 1] << 16) |
               (bytes[offset + 2] << 8) |
               bytes[offset + 3];
    }

    private static string CaptureCollisionState(Transform collisionRoot)
    {
        StringBuilder builder = new StringBuilder();
        Transform[] transforms = collisionRoot.GetComponentsInChildren<Transform>(true);
        Array.Sort(transforms, (left, right) =>
            string.CompareOrdinal(GetRelativePath(collisionRoot, left), GetRelativePath(collisionRoot, right)));

        foreach (Transform transform in transforms)
        {
            builder.Append(GetRelativePath(collisionRoot, transform)).Append('|')
                .Append(transform.localPosition.ToString("R")).Append('|')
                .Append(transform.localRotation.ToString("R")).Append('|')
                .Append(transform.localScale.ToString("R")).Append('\n');

            Collider2D[] colliders = transform.GetComponents<Collider2D>();
            for (int index = 0; index < colliders.Length; index++)
            {
                builder.Append(index).Append(':')
                    .Append(colliders[index].GetType().FullName).Append(':')
                    .Append(EditorJsonUtility.ToJson(colliders[index], false)).Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (target == root)
        {
            return root.name;
        }

        Stack<string> names = new Stack<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return root.name + "/" + string.Join("/", names);
    }

    private static void CaptureFirstSectionPreview(Scene scene, Rect sectionArea)
    {
        string previewDirectory = Path.Combine(
            Path.GetTempPath(),
            "JumpTimingStage1DesignPreview");
        Directory.CreateDirectory(previewDirectory);
        string previewPath = Path.Combine(previewDirectory, "Stage1-design-preview-section-01.png");
        StageCapturePlan previewPlan = new StageCapturePlan(
            scene,
            FindSceneCamera(scene),
            sectionArea,
            ReferenceWidth,
            ReferenceHeight,
            ReferenceHeight,
            4,
            0);
        if (!StageCaptureUtility.Capture(previewPlan, previewPath))
        {
            throw new InvalidOperationException("Stage1 design preview capture was cancelled.");
        }

        Debug.Log($"Stage1 design preview: {previewPath}");
    }
}
