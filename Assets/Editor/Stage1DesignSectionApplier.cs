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
    private const string DesignSpritePath = "Assets/Art/Design/stage1/design1.png";
    private const string StructureReferencePath = "Assets/Art/Design/stage1/structure1.png";
    private const string ShaderPath = "Assets/Shaders/StageDesignBackgroundKey.shader";
    private const string MaterialPath = "Assets/Art/Design/stage1/StageDesignBackgroundKey.mat";
    private const string TestRootName = "DesignTest_Section_01_of_05";
    private const string DesignObjectName = "Design1";
    private const int OutputWidth = 2160;
    private const int DesignSortingOrder = 5;
    private const float PixelsPerUnit = 100f;
    private const float KeyThreshold = 0.09f;
    private const float KeyFeather = 0.08f;
    private const float SectionWorldLeft = -31.485327f;
    private const float SectionWorldRight = 29.92495f;
    private const float SectionWorldBottom = -6.97415f;
    private const float SectionWorldTop = 38.83467f;

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

        Sprite designSprite = EnsureDesignSprite();
        Material designMaterial = EnsureDesignMaterial();
        Rect sectionArea = CreateReferenceSectionArea();

        Transform stageArt = FindStageArt(scene);
        Transform testRoot = RebuildTestRoot(stageArt);
        CreateDesignObject(testRoot, designSprite, designMaterial, sectionArea);

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
            $"Applied {DesignSpritePath} to Stage1/ForegroundDecor/StageArt/{TestRootName}. " +
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

    private static Sprite EnsureDesignSprite()
    {
        if (!File.Exists(DesignSpritePath) || !File.Exists(StructureReferencePath))
        {
            throw new FileNotFoundException(
                $"Both {DesignSpritePath} and {StructureReferencePath} are required.");
        }

        AssetDatabase.ImportAsset(DesignSpritePath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(DesignSpritePath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"Texture importer was not created for {DesignSpritePath}.");
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DesignSpritePath);
        if (sprite == null)
        {
            throw new InvalidOperationException($"Sprite import failed for {DesignSpritePath}.");
        }

        return sprite;
    }

    private static Material EnsureDesignMaterial()
    {
        AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null || !shader.isSupported)
        {
            throw new InvalidOperationException($"Stage design shader is missing or unsupported: {ShaderPath}");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "StageDesignBackgroundKey"
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetFloat("_KeyThreshold", KeyThreshold);
        material.SetFloat("_KeyFeather", KeyFeather);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Rect CreateReferenceSectionArea()
    {
        Vector2Int structureSize = ReadPngSize(StructureReferencePath);
        if (structureSize.x != OutputWidth || structureSize.y != 1611)
        {
            throw new InvalidOperationException(
                $"The expected reference size is {OutputWidth}x1611, but " +
                $"{StructureReferencePath} is {structureSize.x}x{structureSize.y}.");
        }

        // structure1의 중앙 세로벽, 좌우 장벽, 바닥 픽셀 경계를 현재
        // CollisionGuides의 월드 Bounds에 최소제곱으로 대응시킨 값이다.
        return Rect.MinMaxRect(
            SectionWorldLeft,
            SectionWorldBottom,
            SectionWorldRight,
            SectionWorldTop);
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

    private static void CreateDesignObject(
        Transform testRoot,
        Sprite sprite,
        Material material,
        Rect sectionArea)
    {
        GameObject designObject = new GameObject(DesignObjectName);
        designObject.transform.SetParent(testRoot, false);
        designObject.transform.position = new Vector3(sectionArea.center.x, sectionArea.center.y, 0f);

        Vector2 spriteSize = sprite.bounds.size;
        designObject.transform.localScale = new Vector3(
            sectionArea.width / spriteSize.x,
            sectionArea.height / spriteSize.y,
            1f);

        SpriteRenderer renderer = designObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = 0;
        renderer.sortingOrder = DesignSortingOrder;
        renderer.color = Color.white;
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
            OutputWidth,
            1611,
            1611,
            4,
            0);
        if (!StageCaptureUtility.Capture(previewPlan, previewPath))
        {
            throw new InvalidOperationException("Stage1 design preview capture was cancelled.");
        }

        Debug.Log($"Stage1 design preview: {previewPath}");
    }
}
