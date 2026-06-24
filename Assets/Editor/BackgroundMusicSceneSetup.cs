using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BackgroundMusicSceneSetup
{
    private const string ScenePath = "Assets/Scenes/MVPJumpScene.unity";
    private const string MusicClipPath = "Assets/music/first_castle.mp3";
    private const string MusicObjectName = "Background Music";
    private const float DefaultVolume = 0.75f;
    private const float DefaultFadeOutDuration = 3f;

    [MenuItem("Tools/Jump Timing/Setup Background Music", false, 81)]
    public static void SetupActiveScene()
    {
        SetupScene(SceneManager.GetActiveScene(), saveScene: true);
    }

    public static void ApplyToMVPScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SetupScene(scene, saveScene: true);
    }

    public static void SetupScene(Scene scene, bool saveScene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("Background music setup failed because the target scene is not loaded.");
            return;
        }

        AssetDatabase.ImportAsset(MusicClipPath);
        AudioClip musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicClipPath);
        if (musicClip == null)
        {
            Debug.LogError($"Background music setup failed because {MusicClipPath} could not be loaded.");
            return;
        }

        BackgroundMusicLoop musicLoop = FindMusicLoop(scene);
        GameObject musicObject = musicLoop != null
            ? musicLoop.gameObject
            : new GameObject(MusicObjectName);

        if (musicObject.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(musicObject, scene);
        }

        AudioSource audioSource = musicObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = musicObject.AddComponent<AudioSource>();
        }

        musicLoop = musicObject.GetComponent<BackgroundMusicLoop>();
        if (musicLoop == null)
        {
            musicLoop = musicObject.AddComponent<BackgroundMusicLoop>();
        }

        audioSource.clip = musicClip;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        musicLoop.Configure(musicClip, DefaultVolume, DefaultFadeOutDuration);

        EditorUtility.SetDirty(musicObject);
        EditorUtility.SetDirty(audioSource);
        EditorUtility.SetDirty(musicLoop);
        EditorSceneManager.MarkSceneDirty(scene);

        if (saveScene)
        {
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static BackgroundMusicLoop FindMusicLoop(Scene scene)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            BackgroundMusicLoop musicLoop = rootObjects[i].GetComponentInChildren<BackgroundMusicLoop>(true);
            if (musicLoop != null)
            {
                return musicLoop;
            }
        }

        return null;
    }
}
