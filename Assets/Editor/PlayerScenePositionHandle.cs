using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Scene View에서 Player 루트 Transform을 직접 드래그할 수 있는 2D 위치 핸들을 제공합니다.
/// </summary>
[InitializeOnLoad]
public static class PlayerScenePositionHandle
{
    private static readonly Color handleColor = new Color(0.2f, 0.65f, 1f, 0.35f);

    static PlayerScenePositionHandle()
    {
        SceneView.duringSceneGui += DrawPlayerHandles;
    }

    private static void DrawPlayerHandles(SceneView sceneView)
    {
        PlayerController[] players = Resources.FindObjectsOfTypeAll<PlayerController>();
        foreach (PlayerController player in players)
        {
            if (!CanDrawHandle(player))
            {
                continue;
            }

            DrawPositionHandle(player);
        }
    }

    private static bool CanDrawHandle(PlayerController player)
    {
        return player != null
            && !EditorUtility.IsPersistent(player)
            && player.gameObject.scene.IsValid()
            && player.gameObject.activeInHierarchy;
    }

    private static void DrawPositionHandle(PlayerController player)
    {
        Transform playerTransform = player.transform;
        Vector3 position = playerTransform.position;
        float handleSize = GetHandleSize(player);

        Color previousColor = Handles.color;
        Handles.color = handleColor;

        EditorGUI.BeginChangeCheck();
        Vector3 nextPosition = Handles.FreeMoveHandle(
            position,
            handleSize,
            Vector3.zero,
            Handles.RectangleHandleCap);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(playerTransform, "Move Player");
            playerTransform.position = new Vector3(nextPosition.x, nextPosition.y, position.z);
            Selection.activeGameObject = player.gameObject;
            EditorUtility.SetDirty(playerTransform);

            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            }
        }

        Handles.color = previousColor;
    }

    private static float GetHandleSize(PlayerController player)
    {
        BoxCollider2D collider = player.GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            Vector3 size = collider.bounds.size;
            return Mathf.Max(0.2f, Mathf.Max(size.x, size.y));
        }

        return HandleUtility.GetHandleSize(player.transform.position) * 0.15f;
    }
}
