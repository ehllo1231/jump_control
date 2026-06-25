using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Platform 생성과 테스트 시작을 한 곳에서 제공하는 초보자용 맵 제작 창입니다.
/// </summary>
public sealed class MapBuilderWindow : EditorWindow
{
    private const string DefaultPlatformPrefabPath = "Assets/Prefabs/Platform.prefab";
    private const float QuickRotationStep = 15f;

    [SerializeField] private GameObject platformPrefab;
    [SerializeField] private PlatformShape2D defaultShape = PlatformShape2D.Rectangle;
    [SerializeField] private float defaultWidth = 2.5f;
    [SerializeField] private float defaultHeight = 0.3f;
    [SerializeField] private float defaultRotationDegrees;

    [MenuItem("Tools/Jump Timing/Map Builder")]
    public static void OpenWindow()
    {
        MapBuilderWindow window = GetWindow<MapBuilderWindow>("Jump Map Builder");
        window.minSize = new Vector2(390f, 430f);
        window.Show();
    }

    private void OnEnable()
    {
        if (platformPrefab == null)
        {
            platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlatformPrefabPath);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("2D Jump Map Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1) Platform 생성  2) Scene View에서 이동/회전  3) Inspector에서 크기 조절  4) 도달 후보 확인  5) 선택한 발판에서 테스트",
            MessageType.Info);
        bool isPlaying = Application.isPlaying;
        if (isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play Mode에서 생성한 Platform은 게임을 정지하면 Edit Mode 씬에 다시 적용됩니다.",
                MessageType.None);
        }

        EditorGUILayout.Space(4f);
        platformPrefab = EditorGUILayout.ObjectField(
            "Platform Prefab",
            platformPrefab,
            typeof(GameObject),
            false) as GameObject;

        defaultShape = (PlatformShape2D)EditorGUILayout.EnumPopup("New Platform Shape", defaultShape);
        defaultWidth = Mathf.Max(0.1f, EditorGUILayout.FloatField("New Platform Width", defaultWidth));
        defaultHeight = Mathf.Max(0.1f, EditorGUILayout.FloatField("New Platform Height", defaultHeight));
        defaultRotationDegrees = EditorGUILayout.FloatField("New Platform Rotation", defaultRotationDegrees);

        EditorGUILayout.Space(8f);
        if (GUILayout.Button(GetCreateButtonLabel(), GUILayout.Height(34f)))
        {
            CreatePlatform();
        }

        Platform2D selectedPlatform = GetSelectedPlatform();
        GameObject selectedObject = Selection.activeGameObject;

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Selected Platform", EditorStyles.boldLabel);

        if (selectedPlatform != null)
        {
            EditorGUILayout.ObjectField("Platform", selectedPlatform, typeof(Platform2D), true);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Shape", selectedPlatform.Shape);
                EditorGUILayout.Vector2Field("Size", selectedPlatform.Size);
                EditorGUILayout.Vector2Field("Top Center", selectedPlatform.TopCenter);
            }

            DrawSelectedRotationControls(selectedPlatform);
        }
        else
        {
            EditorGUILayout.HelpBox("Platform을 선택하면 크기 정보와 테스트 버튼을 사용할 수 있습니다.", MessageType.None);
        }

        bool canConvert = !isPlaying && selectedPlatform == null && CanConvertToPlatform(selectedObject);
        using (new EditorGUI.DisabledScope(!canConvert))
        {
            if (GUILayout.Button("Convert Selected To Editable Platform"))
            {
                ConvertSelectedToPlatform();
            }
        }

        if (isPlaying && selectedPlatform == null && CanConvertToPlatform(selectedObject))
        {
            EditorGUILayout.HelpBox(
                "Play Mode에서는 변환 대신 Edit Mode에서 Convert를 실행하세요. Play Mode 변환은 Unity가 정식 씬에 저장하지 않습니다.",
                MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(selectedPlatform == null))
        {
            if (GUILayout.Button("Start Test From Selected Platform", GUILayout.Height(30f)))
            {
                MapPlaytestLauncher.StartFromPlatform(selectedPlatform);
            }
        }

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Jump Reachability", EditorStyles.boldLabel);
        bool showReachability = JumpReachabilityOverlay.IsEnabled;
        bool nextShowReachability = EditorGUILayout.Toggle("Show In Scene View", showReachability);
        if (nextShowReachability != showReachability)
        {
            JumpReachabilityOverlay.SetEnabled(nextShowReachability);
        }

        EditorGUILayout.HelpBox(
            "Player 현재 위치와 Jump Tuning 값을 기준으로 Scene View에 점프 궤적과 도달 가능한 Platform 후보를 표시합니다.",
            MessageType.None);

        EditorGUILayout.Space(12f);
        EditorGUILayout.HelpBox(
            "Platform 두 개를 Ctrl/Cmd로 함께 선택하면 Scene View에 ΔX, ΔY, Distance가 표시됩니다.",
            MessageType.Info);
    }

    private void CreatePlatform()
    {
        if (platformPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Platform Prefab Missing",
                $"{DefaultPlatformPrefabPath}를 찾지 못했습니다. Platform Prefab 필드에 프리팹을 지정하세요.",
                "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("No Active Scene", "Platform을 생성할 열린 씬이 없습니다.", "OK");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(platformPrefab, scene) as GameObject;
        if (instance == null)
        {
            EditorUtility.DisplayDialog("Create Failed", "Platform 프리팹 인스턴스를 생성하지 못했습니다.", "OK");
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Create Platform");
        instance.name = GameObjectUtility.GetUniqueNameForSibling(null, "Platform");
        instance.transform.position = GetCreatePosition();
        instance.transform.localScale = Vector3.one;

        Platform2D platform = instance.GetComponent<Platform2D>();
        if (platform == null)
        {
            platform = Undo.AddComponent<Platform2D>(instance);
        }

        platform.SetShape(defaultShape);
        platform.SetSize(defaultWidth, defaultHeight);
        platform.SetRotationDegrees(defaultRotationDegrees);
        EditorUtility.SetDirty(platform);

        MapBuilderPlayModePersistence.TrackCreatedPlatform(platform);
        MarkSceneDirtyIfEditable(scene);

        Selection.activeGameObject = instance;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private Vector3 GetCreatePosition()
    {
        Platform2D selectedPlatform = GetSelectedPlatform();
        if (selectedPlatform != null)
        {
            Vector2 topCenter = selectedPlatform.TopCenter;
            return new Vector3(topCenter.x, topCenter.y + 1.5f, 0f);
        }

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            Vector3 pivot = sceneView.pivot;
            return new Vector3(pivot.x, pivot.y, 0f);
        }

        return Vector3.zero;
    }

    private string GetCreateButtonLabel()
    {
        if (defaultShape == PlatformShape2D.Rectangle)
        {
            return "Create Platform";
        }

        return IsRightTriangleShape(defaultShape)
            ? "Create Right Triangle Brick"
            : "Create Triangle Brick";
    }

    private static bool IsRightTriangleShape(PlatformShape2D shape)
    {
        switch (shape)
        {
            case PlatformShape2D.RightTriangleBottomLeft:
            case PlatformShape2D.RightTriangleBottomRight:
            case PlatformShape2D.RightTriangleTopRight:
            case PlatformShape2D.RightTriangleTopLeft:
                return true;
            default:
                return false;
        }
    }

    private void DrawSelectedRotationControls(Platform2D selectedPlatform)
    {
        EditorGUI.BeginChangeCheck();
        float nextRotation = EditorGUILayout.FloatField("Rotation Z", selectedPlatform.RotationDegrees);
        if (EditorGUI.EndChangeCheck())
        {
            ApplyRotation(selectedPlatform, nextRotation, "Rotate Platform");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("-15"))
            {
                ApplyRotation(
                    selectedPlatform,
                    selectedPlatform.RotationDegrees - QuickRotationStep,
                    "Rotate Platform");
            }

            if (GUILayout.Button("Reset Rotation"))
            {
                ApplyRotation(selectedPlatform, 0f, "Reset Platform Rotation");
            }

            if (GUILayout.Button("+15"))
            {
                ApplyRotation(
                    selectedPlatform,
                    selectedPlatform.RotationDegrees + QuickRotationStep,
                    "Rotate Platform");
            }
        }
    }

    private static void ApplyRotation(Platform2D platform, float rotationDegrees, string undoName)
    {
        if (platform == null)
        {
            return;
        }

        Undo.RecordObject(platform.transform, undoName);
        platform.SetRotationDegrees(rotationDegrees);
        EditorUtility.SetDirty(platform.transform);
        EditorUtility.SetDirty(platform);
        MarkSceneDirtyIfEditable(platform.gameObject.scene);
        SceneView.RepaintAll();
    }

    private void ConvertSelectedToPlatform()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Convert Disabled In Play Mode",
                "Play Mode에서 변환한 오브젝트는 게임을 정지하면 사라질 수 있습니다. Edit Mode로 돌아간 뒤 Convert를 실행하세요.",
                "OK");
            return;
        }

        GameObject selectedObject = Selection.activeGameObject;
        if (!CanConvertToPlatform(selectedObject))
        {
            return;
        }

        SpriteRenderer renderer = selectedObject.GetComponent<SpriteRenderer>();
        Vector2 worldSize = renderer.bounds.size;
        Transform parent = selectedObject.transform.parent;
        float parentScaleX = parent != null ? Mathf.Max(0.0001f, Mathf.Abs(parent.lossyScale.x)) : 1f;
        float parentScaleY = parent != null ? Mathf.Max(0.0001f, Mathf.Abs(parent.lossyScale.y)) : 1f;
        Vector2 localSize = new Vector2(worldSize.x / parentScaleX, worldSize.y / parentScaleY);

        Undo.RecordObject(selectedObject.transform, "Convert Platform Scale");
        selectedObject.transform.localScale = Vector3.one;

        Platform2D platform = Undo.AddComponent<Platform2D>(selectedObject);
        platform.SetSize(localSize.x, localSize.y);
        EditorUtility.SetDirty(platform);
        MarkSceneDirtyIfEditable(selectedObject.scene);
        Selection.activeGameObject = selectedObject;
    }

    private static void MarkSceneDirtyIfEditable(Scene scene)
    {
        if (Application.isPlaying || !scene.IsValid())
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool CanConvertToPlatform(GameObject candidate)
    {
        return candidate != null
            && candidate.GetComponent<Platform2D>() == null
            && candidate.GetComponent<SpriteRenderer>() != null
            && candidate.GetComponent<BoxCollider2D>() != null;
    }

    private static Platform2D GetSelectedPlatform()
    {
        GameObject selectedObject = Selection.activeGameObject;
        return selectedObject != null ? selectedObject.GetComponentInParent<Platform2D>() : null;
    }
}

[InitializeOnLoad]
internal static class MapBuilderPlayModePersistence
{
    private const string DefaultPlatformPrefabPath = "Assets/Prefabs/Platform.prefab";
    private const string PendingPlatformsJsonKey = "JumpTiming.MapBuilder.PendingPlatforms";
    private static readonly List<Platform2D> trackedPlatforms = new List<Platform2D>();

    static MapBuilderPlayModePersistence()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    public static void TrackCreatedPlatform(Platform2D platform)
    {
        if (!Application.isPlaying || platform == null)
        {
            return;
        }

        if (trackedPlatforms.Contains(platform))
        {
            return;
        }

        trackedPlatforms.Add(platform);
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            CaptureTrackedPlatforms();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += ApplyPendingPlatforms;
        }
    }

    private static void CaptureTrackedPlatforms()
    {
        List<PlatformSnapshot> snapshots = new List<PlatformSnapshot>();

        foreach (Platform2D platform in trackedPlatforms)
        {
            if (platform == null)
            {
                continue;
            }

            snapshots.Add(PlatformSnapshot.FromPlatform(platform));
        }

        if (snapshots.Count > 0)
        {
            PlatformSnapshotList snapshotList = new PlatformSnapshotList
            {
                platforms = snapshots.ToArray()
            };
            SessionState.SetString(PendingPlatformsJsonKey, JsonUtility.ToJson(snapshotList));
        }

        trackedPlatforms.Clear();
    }

    private static void ApplyPendingPlatforms()
    {
        if (Application.isPlaying)
        {
            return;
        }

        string json = SessionState.GetString(PendingPlatformsJsonKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        PlatformSnapshotList snapshotList = JsonUtility.FromJson<PlatformSnapshotList>(json);
        if (snapshotList == null || snapshotList.platforms == null || snapshotList.platforms.Length == 0)
        {
            SessionState.EraseString(PendingPlatformsJsonKey);
            return;
        }

        GameObject platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlatformPrefabPath);
        if (platformPrefab == null)
        {
            Debug.LogWarning($"Map Builder: {DefaultPlatformPrefabPath}를 찾지 못해 Play Mode에서 생성한 Platform을 씬에 적용하지 못했습니다.");
            return;
        }

        GameObject lastCreatedPlatform = null;
        foreach (PlatformSnapshot snapshot in snapshotList.platforms)
        {
            GameObject createdPlatform = CreatePlatformFromSnapshot(platformPrefab, snapshot);
            if (createdPlatform != null)
            {
                lastCreatedPlatform = createdPlatform;
            }
        }

        SessionState.EraseString(PendingPlatformsJsonKey);
        if (lastCreatedPlatform != null)
        {
            Selection.activeGameObject = lastCreatedPlatform;
        }
    }

    private static GameObject CreatePlatformFromSnapshot(GameObject platformPrefab, PlatformSnapshot snapshot)
    {
        Scene scene = FindScene(snapshot.scenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = SceneManager.GetActiveScene();
        }

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("Map Builder: Play Mode에서 생성한 Platform을 적용할 열린 씬이 없습니다.");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(platformPrefab, scene) as GameObject;
        if (instance == null)
        {
            return null;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Persist Play Mode Platform");
        instance.name = GameObjectUtility.GetUniqueNameForSibling(null, string.IsNullOrEmpty(snapshot.name) ? "Platform" : snapshot.name);
        instance.transform.position = snapshot.position;
        instance.transform.rotation = snapshot.rotation;
        instance.transform.localScale = snapshot.localScale;

        Platform2D platform = instance.GetComponent<Platform2D>();
        if (platform == null)
        {
            platform = Undo.AddComponent<Platform2D>(instance);
        }

        platform.SetShape(snapshot.shape);
        platform.SetSize(snapshot.size.x, snapshot.size.y);
        if (platform.UsesTriangleShape)
        {
            platform.SetTriangleVertices(
                snapshot.triangleVertexA,
                snapshot.triangleVertexB,
                snapshot.triangleVertexC);
        }
        EditorUtility.SetDirty(platform);

        SpriteRenderer spriteRenderer = instance.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = snapshot.color;
            EditorUtility.SetDirty(spriteRenderer);
        }

        platform.ApplySize();
        MarkSceneDirtyIfEditable(scene);
        return instance;
    }

    private static void MarkSceneDirtyIfEditable(Scene scene)
    {
        if (Application.isPlaying || !scene.IsValid())
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static Scene FindScene(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            return default;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path == scenePath)
            {
                return scene;
            }
        }

        return default;
    }

    [Serializable]
    private sealed class PlatformSnapshotList
    {
        public PlatformSnapshot[] platforms = Array.Empty<PlatformSnapshot>();
    }

    [Serializable]
    private sealed class PlatformSnapshot
    {
        public string name;
        public string scenePath;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
        public PlatformShape2D shape;
        public Vector2 size;
        public Vector2 triangleVertexA;
        public Vector2 triangleVertexB;
        public Vector2 triangleVertexC;
        public Color color;

        public static PlatformSnapshot FromPlatform(Platform2D platform)
        {
            SpriteRenderer spriteRenderer = platform.GetComponent<SpriteRenderer>();
            Vector2[] triangleVertices = platform.GetTriangleVertices();
            return new PlatformSnapshot
            {
                name = platform.gameObject.name,
                scenePath = platform.gameObject.scene.path,
                position = platform.transform.position,
                rotation = platform.transform.rotation,
                localScale = platform.transform.localScale,
                shape = platform.Shape,
                size = platform.Size,
                triangleVertexA = triangleVertices[0],
                triangleVertexB = triangleVertices[1],
                triangleVertexC = triangleVertices[2],
                color = spriteRenderer != null ? spriteRenderer.color : Color.white
            };
        }
    }
}
