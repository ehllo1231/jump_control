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
    private const float TriangleMinimumResizeSize = 0.1f;
    private const float TriangleMoveHandleScale = 0.1f;
    private const float TriangleVertexHandleScale = 0.08f;
    private const float TriangleResizeHandleScale = 0.065f;

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
            "Shape와 Width/Height를 바꾸면 사각형은 SpriteRenderer/BoxCollider2D, 일반 삼각형은 MeshRenderer/PolygonCollider2D, 직각 삼각형은 부모 SpriteRenderer/PolygonCollider2D가 함께 갱신됩니다.",
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
        if (AnySelectedTargetUsesFreeformTriangle())
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

        if (platform.UsesRightTriangleShape)
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

        Bounds localBounds = CalculateLocalBounds(localVertices);
        DrawTriangleBounds(platformTransform, localBounds);
        DrawTriangleMoveHandle(platform, localBounds);
        DrawTriangleResizeHandles(platform, localVertices, localBounds);

        if (!platform.UsesRightTriangleShape)
        {
            for (int i = 0; i < worldVertices.Length; i++)
            {
                float handleSize = HandleUtility.GetHandleSize(worldVertices[i]) * TriangleVertexHandleScale;
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
                    MarkPlatformDirty(platform);
                }
            }
        }
    }

    private static void DrawTriangleBounds(Transform platformTransform, Bounds localBounds)
    {
        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;
        Vector3 bottomLeft = platformTransform.TransformPoint(new Vector3(min.x, min.y, 0f));
        Vector3 bottomRight = platformTransform.TransformPoint(new Vector3(max.x, min.y, 0f));
        Vector3 topRight = platformTransform.TransformPoint(new Vector3(max.x, max.y, 0f));
        Vector3 topLeft = platformTransform.TransformPoint(new Vector3(min.x, max.y, 0f));

        Color previousColor = Handles.color;
        Handles.color = new Color(1f, 0.78f, 0.2f, 0.75f);
        Handles.DrawAAPolyLine(2f, bottomLeft, bottomRight, topRight, topLeft, bottomLeft);
        Handles.color = previousColor;
    }

    private static void DrawTriangleMoveHandle(Platform2D platform, Bounds localBounds)
    {
        Transform platformTransform = platform.transform;
        Vector3 center = platformTransform.TransformPoint(localBounds.center);
        float handleSize = HandleUtility.GetHandleSize(center) * TriangleMoveHandleScale;

        Color previousColor = Handles.color;
        Handles.color = new Color(1f, 0.78f, 0.2f, 0.35f);

        EditorGUI.BeginChangeCheck();
        Vector3 nextCenter = Handles.FreeMoveHandle(
            center,
            handleSize,
            Vector3.zero,
            Handles.RectangleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(platformTransform, "Move Triangle Platform");
            Vector3 delta = nextCenter - center;
            Vector3 position = platformTransform.position;
            platformTransform.position = new Vector3(position.x + delta.x, position.y + delta.y, position.z);
            Selection.activeGameObject = platform.gameObject;
            EditorUtility.SetDirty(platformTransform);
            MarkPlatformDirty(platform);
        }

        Handles.color = previousColor;
    }

    private static void DrawTriangleResizeHandles(Platform2D platform, Vector2[] localVertices, Bounds localBounds)
    {
        float offset = GetTriangleResizeHandleOffset(localBounds);

        DrawTriangleResizeHandle(platform, localVertices, localBounds, true, false, false, false, offset);
        DrawTriangleResizeHandle(platform, localVertices, localBounds, false, true, false, false, offset);
        DrawTriangleResizeHandle(platform, localVertices, localBounds, false, false, true, false, offset);
        DrawTriangleResizeHandle(platform, localVertices, localBounds, false, false, false, true, offset);
        DrawTriangleResizeHandle(platform, localVertices, localBounds, true, false, true, false, offset);
        DrawTriangleResizeHandle(platform, localVertices, localBounds, true, false, false, true, offset);
        DrawTriangleResizeHandle(platform, localVertices, localBounds, false, true, true, false, offset);
        DrawTriangleResizeHandle(platform, localVertices, localBounds, false, true, false, true, offset);
    }

    private static void DrawTriangleResizeHandle(
        Platform2D platform,
        Vector2[] localVertices,
        Bounds localBounds,
        bool moveMinX,
        bool moveMaxX,
        bool moveMinY,
        bool moveMaxY,
        float offset)
    {
        Transform platformTransform = platform.transform;
        Vector2 handleLocal = GetResizeHandleLocalPosition(localBounds, moveMinX, moveMaxX, moveMinY, moveMaxY, offset);
        Vector3 handleWorld = platformTransform.TransformPoint(handleLocal);
        float handleSize = HandleUtility.GetHandleSize(handleWorld) * TriangleResizeHandleScale;

        Color previousColor = Handles.color;
        Handles.color = new Color(1f, 0.78f, 0.2f, 0.95f);

        EditorGUI.BeginChangeCheck();
        Vector3 nextWorld = Handles.FreeMoveHandle(
            handleWorld,
            handleSize,
            Vector3.zero,
            Handles.RectangleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Vector2 nextLocal = platformTransform.InverseTransformPoint(nextWorld);
            Vector2 targetMin = localBounds.min;
            Vector2 targetMax = localBounds.max;

            if (moveMinX)
            {
                targetMin.x = Mathf.Min(nextLocal.x + offset, targetMax.x - TriangleMinimumResizeSize);
            }

            if (moveMaxX)
            {
                targetMax.x = Mathf.Max(nextLocal.x - offset, targetMin.x + TriangleMinimumResizeSize);
            }

            if (moveMinY)
            {
                targetMin.y = Mathf.Min(nextLocal.y + offset, targetMax.y - TriangleMinimumResizeSize);
            }

            if (moveMaxY)
            {
                targetMax.y = Mathf.Max(nextLocal.y - offset, targetMin.y + TriangleMinimumResizeSize);
            }

            Vector2[] resizedVertices = ResizeTriangleVerticesToBounds(localVertices, localBounds, targetMin, targetMax);
            Undo.RecordObject(platform, "Resize Triangle Platform");
            if (platform.SetTriangleVertices(resizedVertices[0], resizedVertices[1], resizedVertices[2]))
            {
                Selection.activeGameObject = platform.gameObject;
                MarkPlatformDirty(platform);
            }
        }

        Handles.color = previousColor;
    }

    private static Vector2 GetResizeHandleLocalPosition(
        Bounds localBounds,
        bool moveMinX,
        bool moveMaxX,
        bool moveMinY,
        bool moveMaxY,
        float offset)
    {
        Vector2 min = localBounds.min;
        Vector2 max = localBounds.max;
        Vector2 center = localBounds.center;

        float x = center.x;
        if (moveMinX)
        {
            x = min.x - offset;
        }
        else if (moveMaxX)
        {
            x = max.x + offset;
        }

        float y = center.y;
        if (moveMinY)
        {
            y = min.y - offset;
        }
        else if (moveMaxY)
        {
            y = max.y + offset;
        }

        return new Vector2(x, y);
    }

    private static float GetTriangleResizeHandleOffset(Bounds localBounds)
    {
        float smallestSide = Mathf.Min(localBounds.size.x, localBounds.size.y);
        float largestSide = Mathf.Max(localBounds.size.x, localBounds.size.y);
        return Mathf.Clamp(smallestSide * 0.08f, 0.12f, Mathf.Max(0.12f, largestSide * 0.08f));
    }

    private static Vector2[] ResizeTriangleVerticesToBounds(
        Vector2[] localVertices,
        Bounds sourceBounds,
        Vector2 targetMin,
        Vector2 targetMax)
    {
        Vector2 sourceMin = sourceBounds.min;
        Vector2 sourceSize = sourceBounds.size;
        Vector2 targetSize = targetMax - targetMin;
        Vector2[] resizedVertices = new Vector2[localVertices.Length];

        for (int i = 0; i < localVertices.Length; i++)
        {
            float normalizedX = sourceSize.x > 0f ? (localVertices[i].x - sourceMin.x) / sourceSize.x : 0.5f;
            float normalizedY = sourceSize.y > 0f ? (localVertices[i].y - sourceMin.y) / sourceSize.y : 0.5f;
            resizedVertices[i] = new Vector2(
                targetMin.x + targetSize.x * normalizedX,
                targetMin.y + targetSize.y * normalizedY);
        }

        return resizedVertices;
    }

    private static Bounds CalculateLocalBounds(Vector2[] points)
    {
        Bounds bounds = new Bounds(points[0], Vector3.zero);
        for (int i = 1; i < points.Length; i++)
        {
            bounds.Encapsulate(points[i]);
        }

        return bounds;
    }

    private static void MarkPlatformDirty(Platform2D platform)
    {
        EditorUtility.SetDirty(platform);
        if (!Application.isPlaying && platform.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(platform.gameObject.scene);
        }

        SceneView.RepaintAll();
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

    private bool AnySelectedTargetUsesFreeformTriangle()
    {
        foreach (Object selectedTarget in targets)
        {
            Platform2D platform = selectedTarget as Platform2D;
            if (platform != null && platform.UsesTriangleShape && !platform.UsesRightTriangleShape)
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

[InitializeOnLoad]
public static class PlatformSelectionRedirector
{
    private static bool isRedirectingSelection;

    static PlatformSelectionRedirector()
    {
        Selection.selectionChanged += RedirectTriangleVisualSelection;
    }

    private static void RedirectTriangleVisualSelection()
    {
        if (isRedirectingSelection)
        {
            return;
        }

        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null || selectedObject.GetComponent<Platform2D>() != null)
        {
            return;
        }

        Platform2D parentPlatform = selectedObject.GetComponentInParent<Platform2D>();
        if (parentPlatform == null || !parentPlatform.UsesTriangleShape)
        {
            return;
        }

        if (selectedObject.GetComponent<MeshRenderer>() == null && selectedObject.GetComponent<MeshFilter>() == null)
        {
            return;
        }

        Object[] nextSelection = ReplaceTriangleVisualsWithParentPlatforms(Selection.objects);
        isRedirectingSelection = true;
        Selection.objects = nextSelection;
        Selection.activeGameObject = parentPlatform.gameObject;
        isRedirectingSelection = false;
    }

    private static Object[] ReplaceTriangleVisualsWithParentPlatforms(Object[] selectedObjects)
    {
        Object[] replacements = new Object[selectedObjects.Length];
        int replacementCount = 0;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            Object replacement = GetTriangleVisualSelectionReplacement(selectedObjects[i]);
            if (replacement == null || ContainsObject(replacements, replacementCount, replacement))
            {
                continue;
            }

            replacements[replacementCount] = replacement;
            replacementCount++;
        }

        if (replacementCount == replacements.Length)
        {
            return replacements;
        }

        Object[] compactReplacements = new Object[replacementCount];
        for (int i = 0; i < replacementCount; i++)
        {
            compactReplacements[i] = replacements[i];
        }

        return compactReplacements;
    }

    private static Object GetTriangleVisualSelectionReplacement(Object selectedObject)
    {
        GameObject selectedGameObject = selectedObject as GameObject;
        if (selectedGameObject == null || selectedGameObject.GetComponent<Platform2D>() != null)
        {
            return selectedObject;
        }

        Platform2D parentPlatform = selectedGameObject.GetComponentInParent<Platform2D>();
        if (parentPlatform == null || !parentPlatform.UsesTriangleShape)
        {
            return selectedObject;
        }

        bool isTriangleVisual = selectedGameObject.GetComponent<MeshRenderer>() != null
            || selectedGameObject.GetComponent<MeshFilter>() != null;
        return isTriangleVisual ? parentPlatform.gameObject : selectedObject;
    }

    private static bool ContainsObject(Object[] objects, int count, Object target)
    {
        for (int i = 0; i < count; i++)
        {
            if (objects[i] == target)
            {
                return true;
            }
        }

        return false;
    }
}
