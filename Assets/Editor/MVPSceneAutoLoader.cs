using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 프로젝트를 열었을 때 비어 있는 기본 씬에서 Play를 누르는 상황을 줄이기 위한 보조 스크립트입니다.
/// 저장되지 않은 변경 사항이 있는 씬은 건드리지 않고, 메뉴로도 MVP 씬을 바로 열 수 있습니다.
/// </summary>
[InitializeOnLoad]
public static class MVPSceneAutoLoader
{
    private const string ScenePath = "Assets/Scenes/MVPJumpScene.unity";
    private const string SessionKey = "JumpTiming.MVPSceneAutoLoader.Opened";

    static MVPSceneAutoLoader()
    {
        EditorApplication.delayCall += OpenMVPSceneIfEditorStartedOnBlankScene;
    }

    [MenuItem("Tools/Jump Timing/Open MVP Scene")]
    public static void OpenMVPScene()
    {
        if (!File.Exists(ScenePath))
        {
            MVPSceneBuilder.BuildMVPScene();
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void OpenMVPSceneIfEditorStartedOnBlankScene()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);

        if (!File.Exists(ScenePath))
        {
            return;
        }

        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path == ScenePath || activeScene.isDirty)
        {
            return;
        }

        if (string.IsNullOrEmpty(activeScene.path))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
