using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DemonCastleInteriorDesigner
{
    private const string ScenePath = "Assets/Scenes/MVPJumpScene.unity";
    private const string RootName = "Demon Castle Interior";
    private const string WallSpritePath = "Assets/IncomingImages/BackGround/Walls/wall1.png";
    private const float WallPixelsPerUnit = 100f;
    private const float MinimumInteriorHeight = 5f;
    private const float MinimumInteriorWidth = 4f;
    private const float LowerWallPadding = 1.5f;
    private const float UpperWidenStartY = 160f;
    private const float UpperWideWallPadding = 1.5f;

    [MenuItem("Tools/Jump Timing/Apply Demon Castle Interior")]
    public static void ApplyDemonCastleInterior()
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"{ScenePath} does not exist.");
            return;
        }

        Scene scene;
        try
        {
            scene = OpenTargetScene();
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogWarning(exception.Message);
            return;
        }

        Sprite wallSprite = EnsureWallSprite();
        if (wallSprite == null)
        {
            Debug.LogError("Demon Castle Interior could not be applied because the wall sprite is missing.");
            return;
        }

        InteriorRegion region = ExtendRegionToTowerBounds(FindInteriorRegion());
        RebuildInterior(scene, region, wallSprite);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Applied demon castle interior between {region.LowerPlatformName} and {region.UpperPlatformName}. " +
            $"Bounds: x {region.Left:F2}..{region.Right:F2}, y {region.Bottom:F2}..{region.Top:F2}");
    }

    private static Scene OpenTargetScene()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.IsValid() && string.Equals(activeScene.path, ScenePath, StringComparison.Ordinal))
        {
            return activeScene;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            throw new InvalidOperationException("Scene switch was cancelled.");
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static Sprite EnsureWallSprite()
    {
        AssetDatabase.ImportAsset(WallSpritePath);
        TextureImporter importer = AssetImporter.GetAtPath(WallSpritePath) as TextureImporter;
        if (importer == null)
        {
            return null;
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool changed = false;
        changed |= SetImporterValue(importer.textureType, TextureImporterType.Sprite, value => importer.textureType = value);
        changed |= SetImporterValue(importer.spriteImportMode, SpriteImportMode.Single, value => importer.spriteImportMode = value);
        changed |= SetImporterValue(importer.spritePixelsPerUnit, WallPixelsPerUnit, value => importer.spritePixelsPerUnit = value);
        changed |= SetImporterValue(importer.mipmapEnabled, false, value => importer.mipmapEnabled = value);
        changed |= SetImporterValue(importer.filterMode, FilterMode.Point, value => importer.filterMode = value);
        changed |= SetImporterValue(importer.textureCompression, TextureImporterCompression.Uncompressed, value => importer.textureCompression = value);

        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(WallSpritePath);
    }

    private static bool SetImporterValue<T>(T currentValue, T targetValue, Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, targetValue))
        {
            return false;
        }

        setter(targetValue);
        return true;
    }

    private static InteriorRegion FindInteriorRegion()
    {
        PlatformCandidate[] platforms = GetHorizontalPlatformCandidates();
        if (platforms.Length < 2)
        {
            return CreateFallbackRegion("Fallback Bottom", "Fallback Top");
        }

        PlatformCandidate first = platforms[0];
        PlatformCandidate second = default;
        bool foundSecond = false;
        for (int i = 1; i < platforms.Length; i++)
        {
            if (Mathf.Abs(platforms[i].Bounds.center.y - first.Bounds.center.y) >= MinimumInteriorHeight)
            {
                second = platforms[i];
                foundSecond = true;
                break;
            }
        }

        if (!foundSecond)
        {
            second = platforms[1];
        }

        PlatformCandidate lower = first.Bounds.center.y <= second.Bounds.center.y ? first : second;
        PlatformCandidate upper = first.Bounds.center.y <= second.Bounds.center.y ? second : first;

        float left = Mathf.Max(lower.Bounds.min.x, upper.Bounds.min.x) + 0.25f;
        float right = Mathf.Min(lower.Bounds.max.x, upper.Bounds.max.x) - 0.25f;
        if (right - left < MinimumInteriorWidth)
        {
            float centerX = (lower.Bounds.center.x + upper.Bounds.center.x) * 0.5f;
            float width = Mathf.Max(MinimumInteriorWidth, Mathf.Min(lower.Bounds.size.x, upper.Bounds.size.x) - 0.5f);
            left = centerX - width * 0.5f;
            right = centerX + width * 0.5f;
        }

        float bottom = lower.Bounds.max.y + 0.2f;
        float top = upper.Bounds.min.y - 0.2f;
        if (top - bottom < MinimumInteriorHeight)
        {
            return CreateFallbackRegion(lower.Name, upper.Name);
        }

        return new InteriorRegion(left, right, bottom, top, lower.Name, upper.Name);
    }

    private static PlatformCandidate[] GetHorizontalPlatformCandidates()
    {
        Platform2D[] platforms = Object.FindObjectsByType<Platform2D>(FindObjectsSortMode.None);
        List<PlatformCandidate> candidates = new List<PlatformCandidate>();
        foreach (Platform2D platform in platforms)
        {
            if (platform == null || platform.gameObject.scene != SceneManager.GetActiveScene())
            {
                continue;
            }

            Bounds bounds = platform.WorldBounds;
            if (string.Equals(platform.name, "Floor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (bounds.size.x < MinimumInteriorWidth || bounds.size.x > 80f)
            {
                continue;
            }

            if (bounds.size.x < bounds.size.y * 1.2f)
            {
                continue;
            }

            candidates.Add(new PlatformCandidate(platform.name, bounds));
        }

        candidates.Sort((a, b) => b.Bounds.size.x.CompareTo(a.Bounds.size.x));
        return candidates.ToArray();
    }

    private static InteriorRegion CreateFallbackRegion(string lowerName, string upperName)
    {
        return new InteriorRegion(3.2f, 14.6f, 1.6f, 53.7f, lowerName, upperName);
    }

    private static InteriorRegion ExtendRegionToTowerBounds(InteriorRegion region)
    {
        float towerBottom = region.Bottom;
        float towerTop = region.Top;
        bool foundPlatform = false;
        Platform2D[] platforms = Object.FindObjectsByType<Platform2D>(FindObjectsSortMode.None);
        foreach (Platform2D platform in platforms)
        {
            if (platform == null
                || platform.gameObject.scene != SceneManager.GetActiveScene()
                || string.Equals(platform.name, "Floor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            towerBottom = foundPlatform
                ? Mathf.Min(towerBottom, platform.WorldBounds.min.y - LowerWallPadding)
                : platform.WorldBounds.min.y - LowerWallPadding;
            towerTop = foundPlatform
                ? Mathf.Max(towerTop, platform.WorldBounds.max.y + LowerWallPadding)
                : platform.WorldBounds.max.y + LowerWallPadding;
            foundPlatform = true;
        }

        if (!foundPlatform || (towerBottom >= region.Bottom && towerTop <= region.Top))
        {
            return region;
        }

        return new InteriorRegion(
            region.Left,
            region.Right,
            towerBottom,
            towerTop,
            region.LowerPlatformName,
            region.UpperPlatformName);
    }

    private static void RebuildInterior(Scene scene, InteriorRegion region, Sprite wallSprite)
    {
        RemoveExistingInterior(scene);

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        CreateWallTiles(root.transform, region, wallSprite);
    }

    private static void RemoveExistingInterior(Scene scene)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (string.Equals(rootObject.name, RootName, StringComparison.Ordinal))
            {
                Object.DestroyImmediate(rootObject);
                return;
            }
        }
    }

    private static void CreateWallTiles(Transform root, InteriorRegion region, Sprite wallSprite)
    {
        Vector2 tileSize = GetSpriteWorldSize(wallSprite);
        float wallWidth = region.Width + 2.4f;
        float wallHeight = region.Height + 1.8f;
        int columns = Mathf.Max(2, Mathf.CeilToInt(wallWidth / tileSize.x) + 1);
        int rows = Mathf.Max(2, Mathf.CeilToInt(wallHeight / tileSize.y) + 1);
        float startX = region.Center.x - columns * tileSize.x * 0.5f + tileSize.x * 0.5f;
        float startY = region.Center.y - rows * tileSize.y * 0.5f + tileSize.y * 0.5f;
        int upperLeftExtraColumns = CalculateUpperLeftExtraColumns(region, tileSize, startX);

        for (int row = 0; row < rows; row++)
        {
            float y = startY + row * tileSize.y;
            int extraColumns = y + tileSize.y * 0.5f >= UpperWidenStartY ? upperLeftExtraColumns : 0;
            int totalColumns = columns + extraColumns;

            for (int column = 0; column < totalColumns; column++)
            {
                int baseColumn = column - extraColumns;
                GameObject tile = CreateSpriteObject(
                    root,
                    $"Wall Tile {row + 1}-{column + 1}",
                    wallSprite,
                    new Vector2(startX + baseColumn * tileSize.x, y),
                    -220,
                    new Color(0.74f, 0.78f, 0.84f, 1f));

                SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
                renderer.flipX = (column & 1) == 1;
                renderer.flipY = (row & 1) == 1;
            }
        }
    }

    private static int CalculateUpperLeftExtraColumns(InteriorRegion region, Vector2 tileSize, float startX)
    {
        float baseLeftEdge = startX - tileSize.x * 0.5f;
        float upperLeftEdge = baseLeftEdge;
        bool foundUpperLeftWall = false;

        Platform2D[] platforms = Object.FindObjectsByType<Platform2D>(FindObjectsSortMode.None);
        foreach (Platform2D platform in platforms)
        {
            if (platform == null
                || platform.gameObject.scene != SceneManager.GetActiveScene()
                || string.Equals(platform.name, "Floor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Bounds bounds = platform.WorldBounds;
            if (bounds.max.y < UpperWidenStartY || bounds.min.x >= region.Left)
            {
                continue;
            }

            float candidateLeftEdge = bounds.min.x - UpperWideWallPadding;
            upperLeftEdge = foundUpperLeftWall
                ? Mathf.Min(upperLeftEdge, candidateLeftEdge)
                : candidateLeftEdge;
            foundUpperLeftWall = true;
        }

        if (!foundUpperLeftWall || upperLeftEdge >= baseLeftEdge)
        {
            return 0;
        }

        return Mathf.CeilToInt((baseLeftEdge - upperLeftEdge) / tileSize.x);
    }

    private static void CreateStoneRibs(Transform root, InteriorRegion region, Sprite wallSprite)
    {
        float ribHeight = region.Height + 2.6f;
        float centerY = region.Center.y;
        float[] ribXs =
        {
            region.Left - 0.95f,
            region.Right + 0.95f,
            Mathf.Lerp(region.Left, region.Right, 0.33f),
            Mathf.Lerp(region.Left, region.Right, 0.67f)
        };

        for (int i = 0; i < ribXs.Length; i++)
        {
            float ribWidth = i < 2 ? 1.3f : 0.28f;
            Color color = i < 2
                ? new Color(0.36f, 0.36f, 0.4f, 0.95f)
                : new Color(0f, 0f, 0f, 0.18f);

            CreateTiledSpriteObject(
                root,
                i < 2 ? $"Outer Stone Rib {i + 1}" : $"Back Wall Crevice {i - 1}",
                wallSprite,
                new Vector2(ribXs[i], centerY),
                new Vector2(ribWidth, ribHeight),
                i < 2 ? -190 : -175,
                color);
        }
    }

    private static void CreateDepthShadows(Transform root, InteriorRegion region, Sprite whiteSprite)
    {
        CreatePanel(root, "Top Ceiling Shadow", whiteSprite, new Vector2(region.Center.x, region.Top - 0.45f), new Vector2(region.Width + 2.8f, 1.8f), new Color(0f, 0f, 0f, 0.42f), -150);
        CreatePanel(root, "Bottom Floor Shadow", whiteSprite, new Vector2(region.Center.x, region.Bottom + 0.35f), new Vector2(region.Width + 2.4f, 1.2f), new Color(0f, 0f, 0f, 0.28f), -150);
        CreatePanel(root, "Left Side Shadow", whiteSprite, new Vector2(region.Left - 0.2f, region.Center.y), new Vector2(1.8f, region.Height + 1.2f), new Color(0f, 0f, 0f, 0.32f), -145);
        CreatePanel(root, "Right Side Shadow", whiteSprite, new Vector2(region.Right + 0.2f, region.Center.y), new Vector2(1.8f, region.Height + 1.2f), new Color(0f, 0f, 0f, 0.32f), -145);
        CreatePanel(root, "Central Blood Haze", whiteSprite, new Vector2(region.Center.x, region.Center.y), new Vector2(region.Width * 0.72f, region.Height * 0.9f), new Color(0.18f, 0.01f, 0.025f, 0.16f), -140);
    }

    private static void CreateDemonBanners(Transform root, InteriorRegion region, Sprite whiteSprite)
    {
        float bannerY = region.Top - Mathf.Min(5.2f, region.Height * 0.16f);
        CreateBanner(root, whiteSprite, "Left Crimson Banner", new Vector2(region.Left + 1.9f, bannerY), 4.9f);
        CreateBanner(root, whiteSprite, "Right Crimson Banner", new Vector2(region.Right - 1.9f, bannerY), 4.9f);

        if (region.Width > 8f)
        {
            CreateBanner(root, whiteSprite, "Center Torn Banner", new Vector2(region.Center.x, region.Center.y + region.Height * 0.14f), 6.2f);
        }
    }

    private static void CreateBanner(Transform root, Sprite whiteSprite, string name, Vector2 position, float height)
    {
        CreatePanel(root, name, whiteSprite, position, new Vector2(0.92f, height), new Color(0.42f, 0.02f, 0.045f, 0.88f), -130);
        CreatePanel(root, $"{name} Dark Edge Left", whiteSprite, position + new Vector2(-0.49f, 0f), new Vector2(0.09f, height), new Color(0.04f, 0f, 0.005f, 0.82f), -125);
        CreatePanel(root, $"{name} Dark Edge Right", whiteSprite, position + new Vector2(0.49f, 0f), new Vector2(0.09f, height), new Color(0.04f, 0f, 0.005f, 0.82f), -125);
        CreatePanel(root, $"{name} Top Bar", whiteSprite, position + new Vector2(0f, height * 0.5f + 0.08f), new Vector2(1.25f, 0.16f), new Color(0.03f, 0.025f, 0.025f, 0.95f), -120);
        CreatePanel(root, $"{name} Lower Tear", whiteSprite, position + new Vector2(0.24f, -height * 0.5f + 0.24f), new Vector2(0.34f, 0.62f), new Color(0.02f, 0f, 0f, 0.55f), -120);
    }

    private static void CreateTorchRows(Transform root, InteriorRegion region, Sprite whiteSprite)
    {
        float[] yValues =
        {
            Mathf.Lerp(region.Bottom, region.Top, 0.22f),
            Mathf.Lerp(region.Bottom, region.Top, 0.48f),
            Mathf.Lerp(region.Bottom, region.Top, 0.74f)
        };

        foreach (float y in yValues)
        {
            CreateTorch(root, whiteSprite, new Vector2(region.Left + 0.8f, y), false);
            CreateTorch(root, whiteSprite, new Vector2(region.Right - 0.8f, y), true);
        }
    }

    private static void CreateTorch(Transform root, Sprite whiteSprite, Vector2 position, bool faceLeft)
    {
        float direction = faceLeft ? -1f : 1f;
        CreatePanel(root, "Torch Red Glow", whiteSprite, position + new Vector2(direction * 0.2f, 0.05f), new Vector2(2.7f, 2.5f), new Color(0.75f, 0.04f, 0.015f, 0.2f), -118);
        CreatePanel(root, "Torch Amber Glow", whiteSprite, position + new Vector2(direction * 0.22f, 0.05f), new Vector2(1.35f, 1.5f), new Color(1f, 0.38f, 0.05f, 0.28f), -117);
        CreatePanel(root, "Torch Bracket", whiteSprite, position + new Vector2(-direction * 0.16f, -0.28f), new Vector2(0.74f, 0.12f), new Color(0.025f, 0.02f, 0.018f, 0.95f), -116);
        CreatePanel(root, "Torch Flame Outer", whiteSprite, position + new Vector2(direction * 0.16f, 0.12f), new Vector2(0.46f, 0.82f), new Color(0.84f, 0.08f, 0.02f, 0.82f), -115);
        CreatePanel(root, "Torch Flame Core", whiteSprite, position + new Vector2(direction * 0.16f, 0.22f), new Vector2(0.22f, 0.44f), new Color(1f, 0.68f, 0.18f, 0.92f), -114);
    }

    private static void CreateThroneRecess(Transform root, InteriorRegion region, Sprite whiteSprite)
    {
        float recessHeight = Mathf.Min(8f, region.Height * 0.18f);
        float recessWidth = Mathf.Min(4.4f, region.Width * 0.42f);
        Vector2 position = new Vector2(region.Center.x, region.Top - recessHeight * 0.6f - 1.1f);

        CreatePanel(root, "Demon Throne Recess", whiteSprite, position, new Vector2(recessWidth, recessHeight), new Color(0.015f, 0.008f, 0.011f, 0.78f), -128);
        CreatePanel(root, "Recess Crimson Core", whiteSprite, position + new Vector2(0f, -0.25f), new Vector2(recessWidth * 0.55f, recessHeight * 0.74f), new Color(0.32f, 0.01f, 0.035f, 0.35f), -126);
        CreatePanel(root, "Recess Top Lintel", whiteSprite, position + new Vector2(0f, recessHeight * 0.5f + 0.16f), new Vector2(recessWidth + 0.8f, 0.32f), new Color(0.02f, 0.018f, 0.02f, 0.9f), -124);
        CreatePanel(root, "Recess Left Pillar", whiteSprite, position + new Vector2(-recessWidth * 0.5f - 0.18f, 0f), new Vector2(0.32f, recessHeight + 0.4f), new Color(0.02f, 0.018f, 0.02f, 0.88f), -124);
        CreatePanel(root, "Recess Right Pillar", whiteSprite, position + new Vector2(recessWidth * 0.5f + 0.18f, 0f), new Vector2(0.32f, recessHeight + 0.4f), new Color(0.02f, 0.018f, 0.02f, 0.88f), -124);
    }

    private static GameObject CreateSpriteObject(Transform root, string name, Sprite sprite, Vector2 position, int sortingOrder, Color color)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(root, false);
        gameObject.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return gameObject;
    }

    private static void CreateTiledSpriteObject(Transform root, string name, Sprite sprite, Vector2 position, Vector2 size, int sortingOrder, Color color)
    {
        GameObject gameObject = CreateSpriteObject(root, name, sprite, position, sortingOrder, color);
        SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = size;
    }

    private static void CreatePanel(Transform root, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int sortingOrder)
    {
        GameObject gameObject = CreateSpriteObject(root, name, sprite, position, sortingOrder, color);
        SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
    }

    private static Vector2 GetSpriteWorldSize(Sprite sprite)
    {
        return new Vector2(sprite.rect.width / sprite.pixelsPerUnit, sprite.rect.height / sprite.pixelsPerUnit);
    }

    private readonly struct PlatformCandidate
    {
        public PlatformCandidate(string name, Bounds bounds)
        {
            Name = name;
            Bounds = bounds;
        }

        public string Name { get; }
        public Bounds Bounds { get; }
    }

    private readonly struct InteriorRegion
    {
        public InteriorRegion(float left, float right, float bottom, float top, string lowerPlatformName, string upperPlatformName)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
            LowerPlatformName = lowerPlatformName;
            UpperPlatformName = upperPlatformName;
        }

        public float Left { get; }
        public float Right { get; }
        public float Bottom { get; }
        public float Top { get; }
        public string LowerPlatformName { get; }
        public string UpperPlatformName { get; }
        public float Width => Right - Left;
        public float Height => Top - Bottom;
        public Vector2 Center => new Vector2((Left + Right) * 0.5f, (Bottom + Top) * 0.5f);
    }
}
