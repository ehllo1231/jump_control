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
    private SerializedProperty widthProperty;
    private SerializedProperty heightProperty;

    private void OnEnable()
    {
        widthProperty = serializedObject.FindProperty("width");
        heightProperty = serializedObject.FindProperty("height");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Width와 Height를 바꾸면 SpriteRenderer와 BoxCollider2D 크기가 함께 변경됩니다.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(widthProperty, new GUIContent("Width"));
        EditorGUILayout.PropertyField(heightProperty, new GUIContent("Height"));

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();

            foreach (Object selectedTarget in targets)
            {
                Platform2D platform = selectedTarget as Platform2D;
                if (platform == null)
                {
                    continue;
                }

                platform.ApplySize();
                EditorUtility.SetDirty(platform);
                if (!Application.isPlaying && platform.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(platform.gameObject.scene);
                }
            }
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }

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
}
