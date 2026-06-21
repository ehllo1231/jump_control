using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 점프 관련 튜닝 값을 한 화면에서 편집하는 전용 Unity Editor 창입니다.
/// Play Mode에서 씬 Player를 대상으로 사용하면 변경값이 즉시 반영됩니다.
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
            "Play Mode에서 씬 Player를 대상으로 값을 변경하면 현재 조준과 게이지에 즉시 반영됩니다.",
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
        DrawDirectionSettings(tuningProperty);
        EditorGUILayout.Space(10f);
        DrawPowerGaugeSettings(tuningProperty);

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
        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.delayCall += FindBestTarget;
        }
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
            return;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(targetController.gameObject))
        {
            PrefabUtility.SavePrefabAsset(targetController.transform.root.gameObject);
        }
        else if (targetController.gameObject.scene.IsValid())
        {
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
