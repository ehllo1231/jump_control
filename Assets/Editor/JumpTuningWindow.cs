using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 점프 관련 튜닝 값을 한 화면에서 편집하는 전용 Unity Editor 창입니다.
/// Play Mode에서 씬 Player를 대상으로 사용하면 변경값이 즉시 반영되고 Edit Mode 복귀 후 유지됩니다.
/// </summary>
public class JumpTuningWindow : EditorWindow
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    private PlayerController targetController;
    private SerializedObject serializedController;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Jump Timing/Jump Tuning")]
    public static void OpenWindow()
    {
        JumpTuningWindow window = GetWindow<JumpTuningWindow>("Jump Tuning");
        window.minSize = new Vector2(390f, 520f);
        window.FindBestTarget();
        window.Show();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += HandleSelectionChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        JumpTuningPlayModePersistence.ApplyPendingTuning();
        FindBestTarget();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= HandleSelectionChanged;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Jump Tuning", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Play Mode에서 씬 Player를 대상으로 값을 변경하면 즉시 반영되고, 정지 후 다시 실행해도 유지됩니다.",
            MessageType.Info);

        DrawTargetControls();

        if (targetController == null)
        {
            EditorGUILayout.HelpBox(
                "PlayerController를 찾지 못했습니다. Player를 선택하거나 아래 버튼으로 씬 또는 프리팹을 지정하세요.",
                MessageType.Warning);
            return;
        }

        EnsureSerializedController();
        if (serializedController == null)
        {
            return;
        }

        SerializedProperty tuningProperty = serializedController.FindProperty("jumpTuning");
        if (tuningProperty == null)
        {
            EditorGUILayout.HelpBox("PlayerController에서 Jump Tuning 설정을 찾지 못했습니다.", MessageType.Error);
            return;
        }

        if (Application.isPlaying && PrefabUtility.IsPartOfPrefabAsset(targetController.gameObject))
        {
            EditorGUILayout.HelpBox(
                "Play Mode 실시간 테스트에는 씬 Player를 대상으로 사용하세요. 프리팹 변경은 현재 실행 중인 인스턴스에 바로 반영되지 않을 수 있습니다.",
                MessageType.Warning);
        }

        serializedController.UpdateIfRequiredOrScript();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUI.BeginChangeCheck();
        DrawPlayerSettings(tuningProperty);
        EditorGUILayout.Space(10f);
        DrawCollisionSettings(tuningProperty);
        EditorGUILayout.Space(10f);
        DrawDirectionSettings(tuningProperty);
        EditorGUILayout.Space(10f);
        DrawPowerGaugeSettings(tuningProperty);
        EditorGUILayout.Space(10f);
        DrawDebugModeSettings(tuningProperty);

        if (EditorGUI.EndChangeCheck())
        {
            serializedController.ApplyModifiedProperties();
            targetController.ApplyJumpTuningNow();
            PersistTargetChanges();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTargetControls()
    {
        EditorGUILayout.Space(4f);
        PlayerController selectedTarget = EditorGUILayout.ObjectField(
            "Target Player",
            targetController,
            typeof(PlayerController),
            true) as PlayerController;

        if (selectedTarget != targetController)
        {
            SetTarget(selectedTarget);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selection"))
            {
                SetTarget(FindControllerFromSelection());
            }

            if (GUILayout.Button("Find Scene Player"))
            {
                SetTarget(FindSceneController());
            }

            if (GUILayout.Button("Use Player Prefab"))
            {
                SetTarget(FindPrefabController());
            }
        }

        if (targetController != null)
        {
            EditorGUILayout.LabelField("Source", GetTargetDescription(targetController), EditorStyles.miniLabel);
        }
    }

    private static void DrawPlayerSettings(SerializedProperty tuningProperty)
    {
        EditorGUILayout.LabelField("Player Body", EditorStyles.boldLabel);
        DrawProperty(tuningProperty, "playerSquareSize", "Player Square Size");
        DrawProperty(tuningProperty, "playerVisualScale", "Player Visual Scale");
        DrawProperty(tuningProperty, "playerVisualYOffset", "Player Visual Y Offset");

        EditorGUILayout.HelpBox(
            "Player Visual Scale과 Y Offset은 충돌체 크기와 점프 판정은 유지하고 보이는 Player 스프라이트만 조정합니다. Y Offset 음수는 이미지를 아래로 내립니다.",
            MessageType.None);
    }

    private static void DrawCollisionSettings(SerializedProperty tuningProperty)
    {
        EditorGUILayout.LabelField("Collision", EditorStyles.boldLabel);
        DrawProperty(tuningProperty, "wallBounceElasticity", "Wall Bounce Elasticity");
        DrawProperty(tuningProperty, "wallBounceVerticalVelocityMode", "Wall Bounce Vertical Mode");

        EditorGUILayout.HelpBox(
            "Wall Bounce Elasticity는 벽에 부딪혔을 때만 가로 방향으로 되돌리는 비율입니다. " +
            "Vertical Mode는 충돌 직전 Y 속도를 보존할지, 기존처럼 충돌 처리 후 현재 Y 속도를 사용할지 결정합니다.",
            MessageType.None);
    }

    private static void DrawDirectionSettings(SerializedProperty tuningProperty)
    {
        EditorGUILayout.LabelField("Direction", EditorStyles.boldLabel);
        DrawProperty(tuningProperty, "minDirectionAngle", "Minimum Angle");
        DrawProperty(tuningProperty, "maxDirectionAngle", "Maximum Angle");
        DrawProperty(tuningProperty, "directionSweepSpeed", "Sweep Speed (Degrees/Second)");
        DrawProperty(tuningProperty, "directionStartNormalized", "Start Position (0-1)");
    }

    private static void DrawPowerGaugeSettings(SerializedProperty tuningProperty)
    {
        EditorGUILayout.LabelField("Power Gauge", EditorStyles.boldLabel);
        DrawProperty(tuningProperty, "gaugeChargeDuration", "Min to Max Duration (Seconds)");
        DrawProperty(tuningProperty, "minimumJumpPower", "Minimum Jump Power");
        DrawProperty(tuningProperty, "maximumJumpPower", "Maximum Jump Power");
        DrawProperty(tuningProperty, "gaugePowerResponse", "Gauge Power Response");

        EditorGUILayout.HelpBox(
            "Gauge Power Response의 X축은 게이지 값 0~1, Y축은 Minimum과 Maximum 사이의 보간 비율 0~1입니다.",
            MessageType.None);
    }

    private static void DrawDebugModeSettings(SerializedProperty tuningProperty)
    {
        EditorGUILayout.LabelField("Debug Mode", EditorStyles.boldLabel);
        DrawProperty(tuningProperty, "debugModeEnabled", "Enable Debug Mode");
    }

    private static void DrawProperty(SerializedProperty parent, string propertyName, string label)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label));
        }
    }

    private void HandleSelectionChanged()
    {
        PlayerController selectedController = FindControllerFromSelection();
        if (selectedController != null)
        {
            SetTarget(selectedController);
        }

        Repaint();
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += ApplyPendingPlayModeTuningAndFindTarget;
        }
        else if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.delayCall += FindBestTarget;
        }
    }

    private void ApplyPendingPlayModeTuningAndFindTarget()
    {
        JumpTuningPlayModePersistence.ApplyPendingTuning();
        FindBestTarget();
    }

    private void FindBestTarget()
    {
        PlayerController controller = FindControllerFromSelection();
        if (controller == null)
        {
            controller = FindSceneController();
        }

        if (controller == null)
        {
            controller = FindPrefabController();
        }

        SetTarget(controller);
    }

    private static PlayerController FindControllerFromSelection()
    {
        GameObject selectedObject = Selection.activeGameObject;
        return selectedObject != null ? selectedObject.GetComponentInParent<PlayerController>() : null;
    }

    private static PlayerController FindSceneController()
    {
        return Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    }

    private static PlayerController FindPrefabController()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        return prefab != null ? prefab.GetComponent<PlayerController>() : null;
    }

    private void SetTarget(PlayerController controller)
    {
        targetController = controller;
        serializedController = controller != null ? new SerializedObject(controller) : null;
        Repaint();
    }

    private void EnsureSerializedController()
    {
        if (serializedController == null || serializedController.targetObject != targetController)
        {
            serializedController = targetController != null ? new SerializedObject(targetController) : null;
        }
    }

    private void PersistTargetChanges()
    {
        if (targetController == null)
        {
            return;
        }

        EditorUtility.SetDirty(targetController);

        if (Application.isPlaying)
        {
            JumpTuningPlayModePersistence.StorePendingTuning(targetController);
            if (PrefabUtility.IsPartOfPrefabAsset(targetController.gameObject))
            {
                PrefabUtility.SavePrefabAsset(targetController.transform.root.gameObject);
            }

            return;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(targetController.gameObject))
        {
            PrefabUtility.SavePrefabAsset(targetController.transform.root.gameObject);
        }
        else if (targetController.gameObject.scene.IsValid())
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetController);
            EditorSceneManager.MarkSceneDirty(targetController.gameObject.scene);
        }
    }

    private static string GetTargetDescription(PlayerController controller)
    {
        string assetPath = AssetDatabase.GetAssetPath(controller.gameObject);
        if (!string.IsNullOrEmpty(assetPath))
        {
            return $"Prefab: {assetPath}";
        }

        return $"Scene: {controller.gameObject.scene.name}/{controller.gameObject.name}";
    }
}

[InitializeOnLoad]
internal static class JumpTuningPlayModePersistence
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string PendingJsonKey = "JumpTiming.JumpTuning.PendingJson";
    private const string PendingGlobalObjectIdKey = "JumpTiming.JumpTuning.PendingGlobalObjectId";
    private const string PendingPrefabPathKey = "JumpTiming.JumpTuning.PendingPrefabPath";

    static JumpTuningPlayModePersistence()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    public static void StorePendingTuning(PlayerController source)
    {
        if (source == null || source.JumpTuning == null)
        {
            return;
        }

        SessionState.SetString(PendingJsonKey, JsonUtility.ToJson(source.JumpTuning));
        SessionState.SetString(PendingGlobalObjectIdKey, GetGlobalObjectId(source));
        SessionState.SetString(PendingPrefabPathKey, AssetDatabase.GetAssetPath(source.gameObject));
    }

    public static void ApplyPendingTuning()
    {
        if (Application.isPlaying)
        {
            return;
        }

        string json = SessionState.GetString(PendingJsonKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        PlayerController destination = FindPendingDestination();
        if (destination == null)
        {
            Debug.LogWarning("Jump Tuning: Play Mode에서 변경한 튜닝 값을 적용할 PlayerController를 찾지 못했습니다.");
            return;
        }

        Undo.RecordObject(destination, "Apply Play Mode Jump Tuning");
        destination.ApplyJumpTuningNow();
        if (destination.JumpTuning == null)
        {
            return;
        }

        JsonUtility.FromJsonOverwrite(json, destination.JumpTuning);
        destination.ApplyJumpTuningNow();
        MarkDestinationDirty(destination);
        ClearPendingTuning();
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += ApplyPendingTuning;
        }
    }

    private static string GetGlobalObjectId(PlayerController controller)
    {
        GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(controller.gameObject);
        string id = globalObjectId.ToString();
        return id.Contains("-00000000000000000000000000000000-") ? string.Empty : id;
    }

    private static PlayerController FindPendingDestination()
    {
        PlayerController controller = FindControllerFromGlobalObjectId();
        if (controller != null)
        {
            return controller;
        }

        controller = FindControllerFromPrefabPath();
        if (controller != null)
        {
            return controller;
        }

        controller = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            return controller;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        return prefab != null ? prefab.GetComponent<PlayerController>() : null;
    }

    private static PlayerController FindControllerFromGlobalObjectId()
    {
        string id = SessionState.GetString(PendingGlobalObjectIdKey, string.Empty);
        if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out GlobalObjectId globalObjectId))
        {
            return null;
        }

        Object resolvedObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
        GameObject gameObject = resolvedObject as GameObject;
        if (gameObject != null)
        {
            return gameObject.GetComponent<PlayerController>();
        }

        Component component = resolvedObject as Component;
        return component != null ? component.GetComponent<PlayerController>() : null;
    }

    private static PlayerController FindControllerFromPrefabPath()
    {
        string prefabPath = SessionState.GetString(PendingPrefabPathKey, string.Empty);
        if (string.IsNullOrEmpty(prefabPath))
        {
            return null;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        return prefab != null ? prefab.GetComponent<PlayerController>() : null;
    }

    private static void MarkDestinationDirty(PlayerController controller)
    {
        EditorUtility.SetDirty(controller);

        if (PrefabUtility.IsPartOfPrefabAsset(controller.gameObject))
        {
            PrefabUtility.SavePrefabAsset(controller.transform.root.gameObject);
            return;
        }

        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        if (controller.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
    }

    private static void ClearPendingTuning()
    {
        SessionState.EraseString(PendingJsonKey);
        SessionState.EraseString(PendingGlobalObjectIdKey);
        SessionState.EraseString(PendingPrefabPathKey);
    }
}
