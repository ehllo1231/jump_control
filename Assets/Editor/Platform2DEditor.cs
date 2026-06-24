using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Unity 초보도 Platform 크기와 적용 대상을 바로 이해할 수 있게 표시합니다.
/// </summary>
[CustomEditor(typeof(Platform2D))]
[CanEditMultipleObjects]
public sealed class Platform2DEditor : Editor
{
    private const float QuickRotationStep = 15f;

    private SerializedProperty shapeProperty;
    private SerializedProperty widthProperty;
    private SerializedProperty heightProperty;
    private SerializedProperty triangleVertexAProperty;
    private SerializedProperty triangleVertexBProperty;
    private SerializedProperty triangleVertexCProperty;

    private void OnEnable()
    {
        shapeProperty = serializedObject.FindProperty("shape");
        widthProperty = serializedObject.FindProperty("width");
        heightProperty = serializedObject.FindProperty("height");
        triangleVertexAProperty = serializedObject.FindProperty("triangleVertexA");
        triangleVertexBProperty = serializedObject.FindProperty("triangleVertexB");
        triangleVertexCProperty = serializedObject.FindProperty("triangleVertexC");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Shape와 Width/Height를 바꾸면 사각형은 SpriteRenderer/BoxCollider2D, 삼각형은 MeshRenderer/PolygonCollider2D가 함께 갱신됩니다. 삼각형은 Scene View에서 꼭지점을 드래그해 모양을 바꿀 수 있습니다.",
            MessageType.Info);

        PlatformShape2D[] previousShapes = GetTargetShapes();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(shapeProperty, new GUIContent("Shape"));
        bool shapeChanged = EditorGUI.EndChangeCheck();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(widthProperty, new GUIContent("Width"));
        EditorGUILayout.PropertyField(heightProperty, new GUIContent("Height"));
        bool sizeChanged = EditorGUI.EndChangeCheck();

        bool triangleVerticesChanged = false;
        if (AnySelectedTargetUsesTriangle())
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Triangle Vertices", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(triangleVertexAProperty, new GUIContent("Vertex A"));
            EditorGUILayout.PropertyField(triangleVertexBProperty, new GUIContent("Vertex B"));
            EditorGUILayout.PropertyField(triangleVertexCProperty, new GUIContent("Vertex C"));
            triangleVerticesChanged = EditorGUI.EndChangeCheck();

            if (GUILayout.Button("Reset Triangle To Shape Preset"))
            {
                ResetSelectedTriangles();
                serializedObject.Update();
            }
        }

        if (shapeChanged || sizeChanged || triangleVerticesChanged)
        {
            serializedObject.ApplyModifiedProperties();
            ApplyEditedProperties(previousShapes, shapeChanged, sizeChanged);
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(6f);
        DrawRotationControls();

        EditorGUILayout.Space(6f);
        using (new EditorGUI.DisabledScope(true))
        {
            Platform2D platform = target as Platform2D;
            if (platform != null)
            {
                EditorGUILayout.Vector2Field("Top Center", platform.TopCenter);
            }
        }
    }

    private void DrawRotationControls()
    {
        Platform2D platform = target as Platform2D;
        if (platform == null)
        {
            return;
        }

        float rotation = platform.RotationDegrees;
        EditorGUI.showMixedValue = HasMixedRotation(rotation);
        EditorGUI.BeginChangeCheck();
        float nextRotation = EditorGUILayout.FloatField("Rotation Z", rotation);
        EditorGUI.showMixedValue = false;
        if (EditorGUI.EndChangeCheck())
        {
            ApplyRotationToTargets(nextRotation, "Rotate Platform");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("-15"))
            {
                AddRotationToTargets(-QuickRotationStep);
            }

            if (GUILayout.Button("Reset"))
            {
                ApplyRotationToTargets(0f, "Reset Platform Rotation");
            }

            if (GUILayout.Button("+15"))
            {
                AddRotationToTargets(QuickRotationStep);
            }
        }
    }

    private void OnSceneGUI()
    {
        Platform2D platform = target as Platform2D;
        if (platform == null || !platform.UsesTriangleShape)
        {
            return;
        }

        Transform platformTransform = platform.transform;
        Vector2[] localVertices = platform.GetTriangleVertices();
        Vector3[] worldVertices =
        {
            platformTransform.TransformPoint(localVertices[0]),
            platformTransform.TransformPoint(localVertices[1]),
            platformTransform.TransformPoint(localVertices[2])
        };

        Handles.color = new Color(0.2f, 0.75f, 1f, 0.9f);
        Handles.DrawAAPolyLine(4f, worldVertices[0], worldVertices[1], worldVertices[2], worldVertices[0]);

        for (int i = 0; i < worldVertices.Length; i++)
        {
            float handleSize = HandleUtility.GetHandleSize(worldVertices[i]) * 0.08f;
            EditorGUI.BeginChangeCheck();
            Vector3 nextWorld = Handles.FreeMoveHandle(
                worldVertices[i],
                handleSize,
                Vector3.zero,
                Handles.DotHandleCap);
            if (!EditorGUI.EndChangeCheck())
            {
                continue;
            }

            Undo.RecordObject(platform, "Move Triangle Vertex");
            nextWorld.z = platformTransform.position.z;
            Vector2 nextLocal = platformTransform.InverseTransformPoint(nextWorld);
            if (platform.SetTriangleVertex(i, nextLocal))
            {
                EditorUtility.SetDirty(platform);
                if (!Application.isPlaying && platform.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(platform.gameObject.scene);
                }
            }
        }
    }

    private PlatformShape2D[] GetTargetShapes()
    {
        PlatformShape2D[] shapes = new PlatformShape2D[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            Platform2D platform = targets[i] as Platform2D;
            shapes[i] = platform != null ? platform.Shape : PlatformShape2D.Rectangle;
        }

        return shapes;
    }

    private bool AnySelectedTargetUsesTriangle()
    {
        foreach (Object selectedTarget in targets)
        {
            Platform2D platform = selectedTarget as Platform2D;
            if (platform != null && platform.UsesTriangleShape)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasMixedRotation(float firstRotation)
    {
        foreach (Object selectedTarget in targets)
        {
            Platform2D platform = selectedTarget as Platform2D;
            if (platform != null && !Mathf.Approximately(platform.RotationDegrees, firstRotation))
            {
                return true;
            }
        }

        return false;
    }

    private void AddRotationToTargets(float deltaDegrees)
    {
        foreach (Object selectedTarget in targets)
        {
            Platform2D platform = selectedTarget as Platform2D;
            if (platform == null)
            {
                continue;
            }

            ApplyRotation(platform, platform.RotationDegrees + deltaDegrees, "Rotate Platform");
        }
    }

    private void ApplyRotationToTargets(float rotationDegrees, string undoName)
    {
        foreach (Object selectedTarget in targets)
        {
            Platform2D platform = selectedTarget as Platform2D;
            if (platform == null)
            {
                continue;
            }

            ApplyRotation(platform, rotationDegrees, undoName);
        }
    }

    private static void ApplyRotation(Platform2D platform, float rotationDegrees, string undoName)
    {
        Undo.RecordObject(platform.transform, undoName);
        platform.SetRotationDegrees(rotationDegrees);
        EditorUtility.SetDirty(platform.transform);
        EditorUtility.SetDirty(platform);
        if (!Application.isPlaying && platform.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(platform.gameObject.scene);
        }
    }

    private void ApplyEditedProperties(PlatformShape2D[] previousShapes, bool shapeChanged, bool sizeChanged)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            Platform2D platform = targets[i] as Platform2D;
            if (platform == null)
            {
                continue;
            }

            if (shapeChanged && platform.Shape != PlatformShape2D.Rectangle && previousShapes[i] != platform.Shape)
            {
                platform.ResetTriangleVerticesToCurrentShape();
            }
            else if (sizeChanged && platform.UsesTriangleShape)
            {
                platform.SetSize(platform.Width, platform.Height);
            }
            else
            {
                platform.ApplySize();
            }

            EditorUtility.SetDirty(platform);
            if (!Application.isPlaying && platform.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(platform.gameObject.scene);
            }
        }
    }

    private void ResetSelectedTriangles()
    {
        foreach (Object selectedTarget in targets)
        {
            Platform2D platform = selectedTarget as Platform2D;
            if (platform == null || !platform.UsesTriangleShape)
            {
                continue;
            }

            Undo.RecordObject(platform, "Reset Triangle Vertices");
            platform.ResetTriangleVerticesToCurrentShape();
            EditorUtility.SetDirty(platform);
            if (!Application.isPlaying && platform.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(platform.gameObject.scene);
            }
        }
    }
}
