using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates timestamped map scene backups before risky editor actions.
/// </summary>
[InitializeOnLoad]
public static class MapSceneBackupUtility
{
    private const string DefaultMapScenePath = "Assets/Scenes/MVPJumpScene.unity";
    private const string RecoveryFolder = "Assets/_Recovery";
    private const string BackupFolder = "Assets/_Recovery/MapSceneBackups";
    private const int MaximumBackupCount = 20;

    static MapSceneBackupUtility()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [MenuItem("Tools/Jump Timing/Create Map Scene Backup", false, 80)]
    public static void CreateActiveSceneBackup()
    {
        string backupPath = CreateBackupForScene(SceneManager.GetActiveScene(), "manual");
        if (string.IsNullOrEmpty(backupPath))
        {
            EditorUtility.DisplayDialog(
                "Map Backup Skipped",
                "백업할 열린 맵 씬을 찾지 못했습니다.",
                "OK");
            return;
        }

        EditorUtility.DisplayDialog("Map Backup Created", backupPath, "OK");
    }

    public static string CreateBackupForScenePath(string scenePath, string reason)
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            return string.Empty;
        }

        Scene loadedScene = FindLoadedScene(scenePath);
        if (loadedScene.IsValid() && loadedScene.isLoaded)
        {
            return CreateBackupForScene(loadedScene, reason);
        }

        if (!File.Exists(scenePath))
        {
            return string.Empty;
        }

        EnsureBackupFolder();
        string backupPath = GetUniqueBackupPath(scenePath, reason);
        FileUtil.CopyFileOrDirectory(scenePath, backupPath);
        AssetDatabase.ImportAsset(backupPath);
        PruneOldBackups();
        Debug.Log($"Map scene backup created: {backupPath}");
        return backupPath;
    }

    public static string CreateBackupForScene(Scene scene, string reason)
    {
        if (!ShouldBackupScene(scene))
        {
            return string.Empty;
        }

        EnsureBackupFolder();
        string backupPath = GetUniqueBackupPath(scene.path, reason);
        if (!EditorSceneManager.SaveScene(scene, backupPath, true))
        {
            Debug.LogWarning($"Map scene backup failed: {scene.path}");
            return string.Empty;
        }

        AssetDatabase.ImportAsset(backupPath);
        PruneOldBackups();
        Debug.Log($"Map scene backup created: {backupPath}");
        return backupPath;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
        {
            return;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty)
            {
                CreateBackupForScene(scene, "before-play");
            }
        }
    }

    private static bool ShouldBackupScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            return false;
        }

        if (scene.path == DefaultMapScenePath)
        {
            return true;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].GetComponentInChildren<Platform2D>(true) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static Scene FindLoadedScene(string scenePath)
    {
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

    private static void EnsureBackupFolder()
    {
        if (!AssetDatabase.IsValidFolder(RecoveryFolder))
        {
            AssetDatabase.CreateFolder("Assets", "_Recovery");
        }

        if (!AssetDatabase.IsValidFolder(BackupFolder))
        {
            AssetDatabase.CreateFolder(RecoveryFolder, "MapSceneBackups");
        }
    }

    private static string GetUniqueBackupPath(string sourceScenePath, string reason)
    {
        string sceneName = Path.GetFileNameWithoutExtension(sourceScenePath);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string safeReason = SanitizeFilePart(reason);
        string basePath = $"{BackupFolder}/{sceneName}-{timestamp}-{safeReason}";
        string backupPath = $"{basePath}.unity";

        int suffix = 1;
        while (File.Exists(backupPath))
        {
            backupPath = $"{basePath}-{suffix}.unity";
            suffix++;
        }

        return backupPath;
    }

    private static string SanitizeFilePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "backup";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] chars = value.ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalidChars, chars[i]) >= 0 || char.IsWhiteSpace(chars[i]))
            {
                chars[i] = '-';
            }
        }

        return new string(chars);
    }

    private static void PruneOldBackups()
    {
        if (!Directory.Exists(BackupFolder))
        {
            return;
        }

        string[] backups = Directory.GetFiles(BackupFolder, "*.unity", SearchOption.TopDirectoryOnly);
        Array.Sort(
            backups,
            (left, right) => File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left)));

        for (int i = MaximumBackupCount; i < backups.Length; i++)
        {
            FileUtil.DeleteFileOrDirectory(backups[i]);
            FileUtil.DeleteFileOrDirectory($"{backups[i]}.meta");
        }
    }
}
