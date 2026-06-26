using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 비어 있는 Unity 프로젝트에서도 메뉴 클릭 한 번으로 MVP 테스트 씬과 프리팹을 생성합니다.
/// Tools/Jump Timing/Build MVP Scene 메뉴를 실행하면 Assets/Scenes/MVPJumpScene.unity가 저장됩니다.
/// </summary>
public static class MVPSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MVPJumpScene.unity";
    private const string AllowSceneOverwriteArgument = "-jumpTimingAllowSceneOverwrite";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string PlatformPrefabPath = "Assets/Prefabs/Platform.prefab";
    private const string WhiteSpritePath = "Assets/Sprites/MVPWhiteSquare.png";
    private const string MusicClipPath = "Assets/Resources/Music/first_castle.mp3";
    private const string PlayerPhysicsPath = "Assets/Materials/MVPPlayerPhysics.physicsMaterial2D";
    private const string PlatformPhysicsPath = "Assets/Materials/MVPPlatformPhysics.physicsMaterial2D";
    private static readonly Color PlatformColor = new Color(0.22f, 0.24f, 0.27f);

    [MenuItem("Tools/Jump Timing/Build MVP Scene")]
    public static void BuildMVPScene()
    {
        if (!CanBuildMVPScene())
        {
            Debug.LogWarning("Build MVP Scene은 Play Mode에서 실행할 수 없습니다. Edit Mode로 돌아간 뒤 실행하세요.");
            return;
        }

        if (!ConfirmSceneReplacement())
        {
            return;
        }

        if (!BackupExistingSceneBeforeReplacement())
        {
            return;
        }

        EnsureFolders();

        Sprite whiteSprite = CreateWhiteSprite();
        PhysicsMaterial2D playerPhysics = CreatePhysicsMaterial(PlayerPhysicsPath, 0.35f, 0f);
        PhysicsMaterial2D platformPhysics = CreatePhysicsMaterial(PlatformPhysicsPath, 0.9f, 0f);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "MVPJumpScene";
        Physics2D.gravity = new Vector2(0f, -24f);

        GameObject player = CreatePlayer(whiteSprite, playerPhysics);
        GameObject platformPrefab = CreatePlatformPrefab(whiteSprite, platformPhysics);

        CreatePlatformInstance(platformPrefab, "Floor", new Vector2(0f, -1f), new Vector2(24f, 0.45f));
        CreatePlatformInstance(platformPrefab, "Platform_01", new Vector2(2.35f, 0.9f), new Vector2(2.6f, 0.32f));
        CreatePlatformInstance(platformPrefab, "Platform_02", new Vector2(-1.7f, 2.55f), new Vector2(2.25f, 0.32f));
        CreatePlatformInstance(platformPrefab, "Platform_03", new Vector2(3.4f, 4.15f), new Vector2(2.15f, 0.32f));
        CreatePlatformInstance(platformPrefab, "Platform_04", new Vector2(-3.0f, 5.75f), new Vector2(2.35f, 0.32f));
        CreatePlatformInstance(platformPrefab, "Platform_05", new Vector2(0.85f, 7.25f), new Vector2(2.6f, 0.32f));

        CreateCamera(player.transform);
        CreateBackgroundMusic();

        PrefabUtility.SaveAsPrefabAssetAndConnect(player, PlayerPrefabPath, InteractionMode.AutomatedAction);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("MVP Scene Built", "Assets/Scenes/MVPJumpScene.unity 생성 완료. 씬을 열고 Play를 누르면 Space 키로 테스트할 수 있습니다.", "OK");
        }
    }

    [MenuItem("Tools/Jump Timing/Build MVP Scene", true)]
    private static bool CanBuildMVPScene()
    {
        return !Application.isPlaying;
    }

    private static bool ConfirmSceneReplacement()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return false;
        }

        if (!File.Exists(ScenePath))
        {
            return true;
        }

        if (Application.isBatchMode)
        {
            if (HasCommandLineArgument(AllowSceneOverwriteArgument))
            {
                return true;
            }

            Debug.LogWarning(
                $"Build MVP Scene skipped because {ScenePath} already exists. " +
                $"Pass {AllowSceneOverwriteArgument} only when overwriting the user map is intentional.");
            return false;
        }

        return EditorUtility.DisplayDialog(
            "Replace MVP Scene?",
            $"{ScenePath}를 기본 샘플 씬으로 다시 저장합니다. 현재 맵 구성이 대체될 수 있습니다.",
            "Replace",
            "Cancel");
    }

    private static bool BackupExistingSceneBeforeReplacement()
    {
        if (!File.Exists(ScenePath))
        {
            return true;
        }

        string backupPath = MapSceneBackupUtility.CreateBackupForScenePath(ScenePath, "before-mvp-rebuild");
        if (!string.IsNullOrEmpty(backupPath))
        {
            return true;
        }

        Debug.LogError($"Build MVP Scene aborted because a backup could not be created for {ScenePath}.");
        return false;
    }

    private static bool HasCommandLineArgument(string argument)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], argument, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets", "Scripts");
        EnsureFolder("Assets/Scripts", "Player");
        EnsureFolder("Assets/Scripts", "Jump");
        EnsureFolder("Assets/Scripts", "Input");
        EnsureFolder("Assets/Scripts", "Camera");
        EnsureFolder("Assets", "Editor");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Sprites");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string fullPath = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static Sprite CreateWhiteSprite()
    {
        if (!File.Exists(WhiteSpritePath))
        {
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(WhiteSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        AssetDatabase.ImportAsset(WhiteSpritePath);
        TextureImporter importer = AssetImporter.GetAtPath(WhiteSpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            TextureImporterSettings importerSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(importerSettings);
            importerSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(importerSettings);

            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
    }

    private static PhysicsMaterial2D CreatePhysicsMaterial(string path, float friction, float bounciness)
    {
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (material == null)
        {
            material = new PhysicsMaterial2D(Path.GetFileNameWithoutExtension(path));
            AssetDatabase.CreateAsset(material, path);
        }

        material.friction = friction;
        material.bounciness = bounciness;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreatePlayer(Sprite sprite, PhysicsMaterial2D physicsMaterial)
    {
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0f, -0.415f, 0f);

        Rigidbody2D body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 1f;
        body.mass = 1f;
        body.linearDamping = 0.15f;
        body.angularDamping = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.72f, 0.72f);
        collider.sharedMaterial = physicsMaterial;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(player.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.2f, 0.65f, 1f);
        renderer.sortingOrder = 10;
        visual.transform.localScale = new Vector3(0.72f, 0.72f, 1f);

        GameObject groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(player.transform, false);
        groundCheck.transform.localPosition = new Vector3(0f, -0.39f, 0f);

        JumpInputReader inputReader = player.AddComponent<JumpInputReader>();
        GroundChecker groundChecker = player.AddComponent<GroundChecker>();
        PlayerJumpMotor jumpMotor = player.AddComponent<PlayerJumpMotor>();
        PlayerVisual playerVisual = player.AddComponent<PlayerVisual>();
        JumpPowerGauge powerGauge = player.AddComponent<JumpPowerGauge>();
        JumpAngleAim angleAim = player.AddComponent<JumpAngleAim>();
        PlayerController controller = player.AddComponent<PlayerController>();

        SetObject(groundChecker, "groundCheckPoint", groundCheck.transform);
        SetObject(jumpMotor, "body", body);
        SetObject(playerVisual, "visualRoot", visual.transform);
        SetObject(playerVisual, "bodyRenderer", renderer);
        SetObject(powerGauge, "gaugeSprite", sprite);
        SetObject(angleAim, "arrowSprite", sprite);

        SetObject(controller, "inputReader", inputReader);
        SetObject(controller, "powerGauge", powerGauge);
        SetObject(controller, "angleAim", angleAim);
        SetObject(controller, "jumpMotor", jumpMotor);
        SetObject(controller, "groundChecker", groundChecker);
        SetObject(controller, "playerVisual", playerVisual);

        return player;
    }

    private static GameObject CreatePlatformPrefab(Sprite sprite, PhysicsMaterial2D physicsMaterial)
    {
        GameObject platform = new GameObject("Platform");
        SpriteRenderer renderer = platform.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = PlatformColor;
        renderer.sortingOrder = 0;

        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.sharedMaterial = physicsMaterial;

        Platform2D platform2D = platform.AddComponent<Platform2D>();
        platform2D.SetSize(1f, 1f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(platform, PlatformPrefabPath);
        Object.DestroyImmediate(platform);
        return prefab;
    }

    private static void CreatePlatformInstance(GameObject prefab, string name, Vector2 position, Vector2 scale)
    {
        GameObject platform = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (platform == null)
        {
            return;
        }

        platform.name = name;
        platform.transform.position = position;
        platform.transform.localScale = Vector3.one;

        Platform2D platform2D = platform.GetComponent<Platform2D>();
        if (platform2D == null)
        {
            platform2D = platform.AddComponent<Platform2D>();
        }
        platform2D.SetSize(scale.x, scale.y);

        SpriteRenderer renderer = platform.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = PlatformColor;
        }
    }

    private static void CreateCamera(Transform player)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 1.5f, -10f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 4.8f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);

        cameraObject.AddComponent<AudioListener>();
        SimpleCameraFollow follow = cameraObject.AddComponent<SimpleCameraFollow>();
        SetObject(follow, "target", player);
        SetVector3(follow, "offset", new Vector3(0f, 1.6f, -10f));
        SetFloat(follow, "smoothTime", 0.16f);
        SetFloat(follow, "minY", 0.25f);
    }

    private static void CreateBackgroundMusic()
    {
        AudioClip musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicClipPath);
        if (musicClip == null)
        {
            Debug.LogWarning($"{MusicClipPath}를 찾지 못해 배경 음악 오브젝트를 생성하지 않았습니다.");
            return;
        }

        GameObject musicObject = new GameObject("Background Music");
        AudioSource audioSource = musicObject.AddComponent<AudioSource>();
        audioSource.clip = musicClip;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        BackgroundMusicLoop musicLoop = musicObject.AddComponent<BackgroundMusicLoop>();
        musicLoop.Configure(musicClip, 0.75f, 3f);
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
    }

    private static void SetObject(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetVector3(Object target, string propertyName, Vector3 value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).vector3Value = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
