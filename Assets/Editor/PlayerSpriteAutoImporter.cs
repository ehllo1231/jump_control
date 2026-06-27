using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal sealed class PlayerSpriteAutoImporter : AssetPostprocessor
{
    private const string PlayerSpritePath = "Assets/IncomingImages/Player/stand/demonking_right.png";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string VisualChildName = "Visual";
    private const float SizeTolerance = 0.000001f;

    private static bool applyQueued;

    private void OnPreprocessTexture()
    {
        if (!IsPlayerSpritePath(assetPath))
        {
            return;
        }

        TextureImporter importer = assetImporter as TextureImporter;
        if (importer != null)
        {
            ConfigureTextureImporter(importer);
        }
    }

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (ContainsPlayerSpritePath(importedAssets) || ContainsPlayerSpritePath(movedAssets))
        {
            QueueApplyPlayerSprite();
        }
    }

    [MenuItem("Tools/Jump Timing/Refresh Player Sprite")]
    private static void RefreshPlayerSprite()
    {
        if (!File.Exists(PlayerSpritePath))
        {
            Debug.LogWarning($"Player sprite file is missing: {PlayerSpritePath}");
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(PlayerSpritePath) as TextureImporter;
        if (importer != null)
        {
            ConfigureTextureImporter(importer);
            importer.SaveAndReimport();
        }
        else
        {
            AssetDatabase.ImportAsset(PlayerSpritePath, ImportAssetOptions.ForceUpdate);
        }

        ApplyPlayerSprite();
    }

    private static void QueueApplyPlayerSprite()
    {
        if (applyQueued)
        {
            return;
        }

        applyQueued = true;
        EditorApplication.delayCall += ApplyQueuedPlayerSprite;
    }

    private static void ApplyQueuedPlayerSprite()
    {
        applyQueued = false;
        ApplyPlayerSprite();
    }

    private static void ApplyPlayerSprite()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"Player sprite could not be loaded as a Sprite: {PlayerSpritePath}");
            return;
        }

        bool changedPrefab = ApplySpriteToPlayerPrefab(sprite);
        int changedScenePlayers = ApplySpriteToOpenScenePlayers(sprite);

        if (changedPrefab || changedScenePlayers > 0)
        {
            AssetDatabase.SaveAssets();
        }
    }

    private static bool ApplySpriteToPlayerPrefab(Sprite sprite)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Player prefab could not be loaded: {PlayerPrefabPath}");
            return false;
        }

        SpriteRenderer renderer = FindPlayerBodyRenderer(prefab);
        if (renderer == null)
        {
            Debug.LogWarning($"Player prefab does not contain a body SpriteRenderer: {PlayerPrefabPath}");
            return false;
        }

        if (!ApplySpriteToRenderer(renderer, sprite))
        {
            return false;
        }

        EditorUtility.SetDirty(renderer);
        PrefabUtility.SavePrefabAsset(prefab);
        return true;
    }

    private static int ApplySpriteToOpenScenePlayers(Sprite sprite)
    {
        int changedCount = 0;
        PlayerVisual[] visuals = Object.FindObjectsByType<PlayerVisual>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < visuals.Length; i++)
        {
            PlayerVisual visual = visuals[i];
            if (visual == null || EditorUtility.IsPersistent(visual))
            {
                continue;
            }

            SpriteRenderer renderer = FindPlayerBodyRenderer(visual.gameObject);
            if (renderer == null || !ApplySpriteToRenderer(renderer, sprite))
            {
                continue;
            }

            EditorUtility.SetDirty(renderer);
            Scene scene = renderer.gameObject.scene;
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            changedCount++;
        }

        return changedCount;
    }

    private static SpriteRenderer FindPlayerBodyRenderer(GameObject root)
    {
        Transform visual = root.transform.Find(VisualChildName);
        if (visual != null && visual.TryGetComponent(out SpriteRenderer visualRenderer))
        {
            return visualRenderer;
        }

        PlayerVisual playerVisual = root.GetComponent<PlayerVisual>();
        if (playerVisual != null)
        {
            SerializedObject serializedVisual = new SerializedObject(playerVisual);
            SerializedProperty bodyRendererProperty = serializedVisual.FindProperty("bodyRenderer");
            if (bodyRendererProperty != null && bodyRendererProperty.objectReferenceValue is SpriteRenderer bodyRenderer)
            {
                return bodyRenderer;
            }

            SerializedProperty visualRootProperty = serializedVisual.FindProperty("visualRoot");
            Transform visualRoot = visualRootProperty != null ? visualRootProperty.objectReferenceValue as Transform : null;
            if (visualRoot != null)
            {
                SpriteRenderer renderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null)
                {
                    return renderer;
                }
            }
        }

        return root.GetComponentInChildren<SpriteRenderer>(true);
    }

    private static bool ApplySpriteToRenderer(SpriteRenderer renderer, Sprite sprite)
    {
        bool changed = false;

        if (renderer.sprite != sprite)
        {
            renderer.sprite = sprite;
            changed = true;
        }

        if (renderer.drawMode != SpriteDrawMode.Simple)
        {
            renderer.drawMode = SpriteDrawMode.Simple;
            changed = true;
        }

        Vector2 expectedSize = sprite != null ? new Vector2(sprite.bounds.size.x, sprite.bounds.size.y) : Vector2.one;
        if ((renderer.size - expectedSize).sqrMagnitude > SizeTolerance)
        {
            renderer.size = expectedSize;
            changed = true;
        }

        if (renderer.flipX)
        {
            renderer.flipX = false;
            changed = true;
        }

        if (renderer.flipY)
        {
            renderer.flipY = false;
            changed = true;
        }

        return changed;
    }

    private static void ConfigureTextureImporter(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        if (TryReadPngSize(importer.assetPath, out _, out int height) && height > 0)
        {
            importer.spritePixelsPerUnit = height;
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }

    private static bool ContainsPlayerSpritePath(string[] paths)
    {
        if (paths == null)
        {
            return false;
        }

        for (int i = 0; i < paths.Length; i++)
        {
            if (IsPlayerSpritePath(paths[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPlayerSpritePath(string path)
    {
        return string.Equals(NormalizeAssetPath(path), PlayerSpritePath);
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }

    private static bool TryReadPngSize(string assetPath, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
        {
            return false;
        }

        byte[] header = new byte[24];
        using (FileStream stream = File.OpenRead(assetPath))
        {
            if (stream.Read(header, 0, header.Length) != header.Length)
            {
                return false;
            }
        }

        if (header[0] != 0x89
            || header[1] != 0x50
            || header[2] != 0x4E
            || header[3] != 0x47
            || header[4] != 0x0D
            || header[5] != 0x0A
            || header[6] != 0x1A
            || header[7] != 0x0A)
        {
            return false;
        }

        width = ReadBigEndianInt32(header, 16);
        height = ReadBigEndianInt32(header, 20);
        return width > 0 && height > 0;
    }

    private static int ReadBigEndianInt32(byte[] bytes, int startIndex)
    {
        return (bytes[startIndex] << 24)
            | (bytes[startIndex + 1] << 16)
            | (bytes[startIndex + 2] << 8)
            | bytes[startIndex + 3];
    }
}
