using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage1의 충돌 데이터를 읽기만 하면서 독립 StageArt 테스트를 생성하고 검증합니다.
/// </summary>
public static class Stage1BasicSpriteTestBuilder
{
    private const string StageRootName = "Stage1";
    private const string CollisionPath = "Collision";
    private const string StageArtPath = "ForegroundDecor/StageArt";
    private const string CollisionGuidesPath = "ForegroundDecor/CollisionGuides";
    private const string TestRootName = "BasicSpriteTest";
    private const string SpriteFolder = "Assets/Art/Stage1/basic_sprite";
    private const float TopRoomCutoffY = 159.5f;
    private const float TestAreaMinY = -3f;
    private const float TestAreaMinX = -35f;
    private const float TestAreaMaxX = 27f;
    private const float MinimumRectSize = 0.08f;
    private const float VisualInset = 0.015f;
    private const float FillTileTargetSize = 2.4f;
    private const int FillSortingOrder = 1;
    private const int DetailSortingOrder = 2;

    private static readonly int[] BasicBrickSprites = { 1, 3, 6, 8 };
    private static readonly int[] CrackedBrickSprites = { 11, 14, 18 };
    private static readonly int[] PlatformSprites = { 41, 43, 45, 47 };
    private static readonly int[] ShortWallSprites = { 59, 60, 63, 65, 66 };
    private static readonly int[] CornerSprites = { 69, 70, 72, 76 };
    private static readonly int[] PillarSprites = { 84, 86, 88, 90 };

    [MenuItem("Tools/Jump Timing/Stage1 Basic Sprite Test/Apply Test Art _F6", false, 92)]
    public static void ApplyTestArt()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("STAGE1_BASIC_SPRITE_APPLY_FAILED Play Mode에서는 적용할 수 없습니다.");
            return;
        }

        if (!TryFindStageRoots(out Transform collision, out Transform stageArt, out Transform guides, out string error))
        {
            Debug.LogError($"STAGE1_BASIC_SPRITE_APPLY_FAILED {error}");
            return;
        }

        string protectedStateBefore = CaptureProtectedState(collision, guides);
        Dictionary<int, Sprite> sprites;
        try
        {
            sprites = LoadRequiredSprites();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.LogError($"STAGE1_BASIC_SPRITE_APPLY_FAILED {exception.Message}");
            return;
        }

        Transform existingRoot = stageArt.Find(TestRootName);
        if (existingRoot != null)
        {
            Undo.DestroyObjectImmediate(existingRoot.gameObject);
        }

        GameObject testRootObject = new GameObject(TestRootName);
        Undo.RegisterCreatedObjectUndo(testRootObject, "Apply Stage1 Basic Sprite Test");
        Transform testRoot = testRootObject.transform;
        testRoot.SetParent(stageArt, false);
        ResetLocalTransform(testRoot);

        Dictionary<string, Transform> regionRoots = CreateRegionRoots(testRoot);
        Collider2D[] activeColliders = collision
            .GetComponentsInChildren<Collider2D>(true)
            .Where(item => item.enabled && item.gameObject.activeInHierarchy && !item.isTrigger)
            .ToArray();
        Renderer[] guideRenderers = guides.GetComponentsInChildren<Renderer>(true);
        List<Rect> sourceRects = CollectSourceRects(activeColliders, guideRenderers);
        List<Rect> unionRects = BuildUnionRects(sourceRects);
        Dictionary<int, int> usageCounts = new Dictionary<int, int>();
        Dictionary<string, int> regionCounts = regionRoots.Keys.ToDictionary(key => key, _ => 0);
        int serial = 1;

        foreach (Rect rect in unionRects.OrderBy(item => item.center.y).ThenBy(item => item.xMin))
        {
            string regionName = GetRegionName(rect.center.y);
            Transform parent = regionRoots[regionName];
            int beforeCount = serial;
            CreateFillTiles(rect, parent, sprites, usageCounts, ref serial);
            CreateSurfaceDetail(rect, parent, sprites, usageCounts, ref serial);
            regionCounts[regionName] += serial - beforeCount;
        }

        int polygonTiles = CreatePolygonFillTiles(
            activeColliders.OfType<PolygonCollider2D>().ToArray(),
            guideRenderers,
            regionRoots,
            sprites,
            usageCounts,
            regionCounts,
            ref serial);

        Physics2D.SyncTransforms();
        string protectedStateAfter = CaptureProtectedState(collision, guides);
        if (!string.Equals(protectedStateBefore, protectedStateAfter, StringComparison.Ordinal))
        {
            UnityEngine.Object.DestroyImmediate(testRootObject);
            Debug.LogError(
                "STAGE1_BASIC_SPRITE_APPLY_FAILED Collision 또는 CollisionGuides 상태가 변경되어 테스트 루트를 제거했습니다.");
            return;
        }

        if (!TryValidateTestRoot(testRoot, activeColliders, out string validationSummary, out error))
        {
            UnityEngine.Object.DestroyImmediate(testRootObject);
            Debug.LogError($"STAGE1_BASIC_SPRITE_APPLY_FAILED {error}");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("STAGE1_BASIC_SPRITE_APPLY_FAILED 씬을 저장하지 못했습니다.");
            return;
        }

        Selection.activeGameObject = testRootObject;
        SceneView.RepaintAll();
        string usedSprites = string.Join(",", usageCounts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key:00}:{pair.Value}"));
        string regions = string.Join(",", regionCounts.Select(pair => $"{pair.Key}:{pair.Value}"));
        Debug.Log(
            $"STAGE1_BASIC_SPRITE_APPLY_OK cutoffY={TopRoomCutoffY:F2} " +
            $"sourceRects={sourceRects.Count} unionRects={unionRects.Count} polygonTiles={polygonTiles} " +
            $"renderers={serial - 1} " +
            $"usedSprites=[{usedSprites}] regions=[{regions}] {validationSummary}");
    }

    [MenuItem("Tools/Jump Timing/Stage1 Basic Sprite Test/Verify Test Art", false, 93)]
    public static void VerifyTestArt()
    {
        if (!TryFindStageRoots(out Transform collision, out Transform stageArt, out _, out string error))
        {
            Debug.LogError($"STAGE1_BASIC_SPRITE_VERIFY_FAILED {error}");
            return;
        }

        Transform testRoot = stageArt.Find(TestRootName);
        if (testRoot == null)
        {
            Debug.LogError($"STAGE1_BASIC_SPRITE_VERIFY_FAILED {TestRootName} 루트를 찾지 못했습니다.");
            return;
        }

        Collider2D[] activeColliders = collision
            .GetComponentsInChildren<Collider2D>(true)
            .Where(item => item.enabled && item.gameObject.activeInHierarchy && !item.isTrigger)
            .ToArray();
        Physics2D.SyncTransforms();
        if (!TryValidateTestRoot(testRoot, activeColliders, out string summary, out error))
        {
            Debug.LogError($"STAGE1_BASIC_SPRITE_VERIFY_FAILED {error}");
            return;
        }

        Debug.Log($"STAGE1_BASIC_SPRITE_VERIFY_OK {summary}");
    }

    [MenuItem("Tools/Jump Timing/Stage1 Basic Sprite Test/Reload Scene And Verify _F5", false, 93)]
    public static void ReloadSceneAndVerify()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("STAGE1_BASIC_SPRITE_RELOAD_VERIFY_FAILED 저장된 활성 씬 경로가 없습니다.");
            return;
        }

        if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("STAGE1_BASIC_SPRITE_RELOAD_VERIFY_FAILED 활성 씬을 저장하지 못했습니다.");
            return;
        }

        string path = scene.path;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        VerifyTestArt();
        if (!TryFindStageRoots(out Transform collision, out _, out Transform guides, out string error))
        {
            Debug.LogError($"STAGE1_BASIC_SPRITE_RELOAD_VERIFY_FAILED {error}");
            return;
        }

        Debug.Log(
            $"STAGE1_BASIC_SPRITE_RELOAD_VERIFY_OK scene={path} " +
            $"colliders={collision.GetComponentsInChildren<Collider2D>(true).Length} " +
            $"guides={guides.GetComponentsInChildren<Renderer>(true).Length}");
    }

    [MenuItem("Tools/Jump Timing/Stage1 Basic Sprite Test/Remove Test Art", false, 94)]
    public static void RemoveTestArt()
    {
        if (!TryFindStageRoots(out _, out Transform stageArt, out _, out string error))
        {
            Debug.LogError($"STAGE1_BASIC_SPRITE_REMOVE_FAILED {error}");
            return;
        }

        Transform testRoot = stageArt.Find(TestRootName);
        if (testRoot == null)
        {
            Debug.Log($"STAGE1_BASIC_SPRITE_REMOVE_OK {TestRootName} 루트가 이미 없습니다.");
            return;
        }

        Undo.DestroyObjectImmediate(testRoot.gameObject);
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"STAGE1_BASIC_SPRITE_REMOVE_OK {TestRootName} 루트를 제거했습니다.");
    }

    [MenuItem("Tools/Jump Timing/Stage1 Basic Sprite Test/Report Layout _F8", false, 90)]
    public static void ReportLayout()
    {
        if (!TryFindStageRoots(out Transform collision, out Transform stageArt, out Transform guides, out string error))
        {
            Debug.LogError($"STAGE1_BASIC_SPRITE_REPORT_FAILED {error}");
            return;
        }

        Collider2D[] colliders = collision.GetComponentsInChildren<Collider2D>(true);
        Renderer[] guideRenderers = guides.GetComponentsInChildren<Renderer>(true);
        SpriteRenderer playerRenderer = FindPlayerRenderer();
        StringBuilder report = new StringBuilder(8192);
        report.AppendLine(
            $"STAGE1_BASIC_SPRITE_REPORT scene={SceneManager.GetActiveScene().name} " +
            $"colliders={colliders.Length} guides={guideRenderers.Length} stageArtChildren={stageArt.childCount}");
        report.AppendLine(
            "colliderTypes=" + string.Join(",", colliders
                .GroupBy(item => item.GetType().Name)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}:{group.Count()}")));

        if (playerRenderer != null)
        {
            report.AppendLine(
                $"playerSorting layer={playerRenderer.sortingLayerName} order={playerRenderer.sortingOrder} " +
                $"path={GetPath(playerRenderer.transform)}");
        }

        IGrouping<int, Collider2D>[] heightBins = colliders
            .GroupBy(item => Mathf.FloorToInt(item.bounds.center.y / 5f) * 5)
            .OrderBy(group => group.Key)
            .ToArray();
        report.AppendLine("colliderYBins=" + string.Join(", ", heightBins.Select(group => $"{group.Key}:{group.Count()}")));

        report.AppendLine("topColliders:");
        foreach (Collider2D item in colliders
                     .OrderByDescending(collider => collider.bounds.max.y)
                     .ThenBy(collider => collider.bounds.min.x)
                     .Take(70))
        {
            Bounds bounds = item.bounds;
            report.AppendLine(
                $"  y={bounds.min.y:F3}..{bounds.max.y:F3} x={bounds.min.x:F3}..{bounds.max.x:F3} " +
                $"type={item.GetType().Name} path={GetPath(item.transform)}");
        }

        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Jump Timing/Stage1 Basic Sprite Test/Capture Guides For Review _F7", false, 91)]
    public static void CaptureGuidesForReview()
    {
        if (!TryFindStageRoots(out _, out _, out Transform guides, out string error))
        {
            Debug.LogError($"STAGE1_GUIDE_CAPTURE_FAILED {error}");
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("STAGE1_GUIDE_CAPTURE_FAILED Main Camera를 찾지 못했습니다.");
            return;
        }

        const int width = 1200;
        Rect area = new Rect(-35f, -3f, 62f, 225f);
        int height = Mathf.CeilToInt(width * area.height / area.width);
        StageCapturePlan plan = new StageCapturePlan(
            SceneManager.GetActiveScene(),
            camera,
            area,
            width,
            height,
            1024,
            1,
            guides.GetComponentsInChildren<Renderer>(true).Length);

        string path = Path.Combine(Path.GetTempPath(), "Stage1-CollisionGuides-review.png");
        if (!StageCaptureUtility.Capture(plan, path))
        {
            Debug.LogError("STAGE1_GUIDE_CAPTURE_FAILED 캡처가 취소되었습니다.");
            return;
        }

        Debug.Log(
            $"STAGE1_GUIDE_CAPTURE_OK path={path} resolution={plan.Width}x{plan.Height} " +
            $"area={plan.Area}");
    }

    private static Dictionary<int, Sprite> LoadRequiredSprites()
    {
        int[] required = BasicBrickSprites
            .Concat(CrackedBrickSprites)
            .Concat(PlatformSprites)
            .Concat(ShortWallSprites)
            .Concat(CornerSprites)
            .Concat(PillarSprites)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        Dictionary<int, Sprite> result = new Dictionary<int, Sprite>();

        foreach (int index in required)
        {
            string path = GetSpritePath(index);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"TextureImporter를 찾지 못했습니다: {path}");
            }

            TextureImporterSettings textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            bool needsReimport = importer.textureType != TextureImporterType.Sprite ||
                                 importer.spriteImportMode != SpriteImportMode.Single ||
                                 !Mathf.Approximately(importer.spritePixelsPerUnit, 100f) ||
                                 textureSettings.spriteMeshType != SpriteMeshType.FullRect ||
                                 importer.mipmapEnabled ||
                                 !importer.alphaIsTransparency;
            if (needsReimport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(textureSettings);
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException($"Sprite를 불러오지 못했습니다: {path}");
            }

            result.Add(index, sprite);
        }

        return result;
    }

    private static Dictionary<string, Transform> CreateRegionRoots(Transform testRoot)
    {
        string[] names =
        {
            "01_LowerDungeon_Y-3_55",
            "02_MidDungeon_Y55_110",
            "03_UpperDungeon_Y110_159.5"
        };
        Dictionary<string, Transform> result = new Dictionary<string, Transform>();
        foreach (string name in names)
        {
            GameObject region = new GameObject(name);
            region.transform.SetParent(testRoot, false);
            ResetLocalTransform(region.transform);
            result.Add(name, region.transform);
        }

        return result;
    }

    private static string GetRegionName(float centerY)
    {
        if (centerY < 55f)
        {
            return "01_LowerDungeon_Y-3_55";
        }

        return centerY < 110f
            ? "02_MidDungeon_Y55_110"
            : "03_UpperDungeon_Y110_159.5";
    }

    private static List<Rect> CollectSourceRects(Collider2D[] colliders, Renderer[] guideRenderers)
    {
        List<Rect> guideRects = guideRenderers
            .Select(renderer => ToRect(renderer.bounds))
            .Where(rect => rect.width > MinimumRectSize && rect.height > MinimumRectSize)
            .ToList();
        List<Rect> result = new List<Rect>();

        foreach (BoxCollider2D box in colliders.OfType<BoxCollider2D>())
        {
            if (!IsWorldAxisAligned(box))
            {
                continue;
            }

            Rect rect = ToRect(box.bounds);
            rect.xMin = Mathf.Max(rect.xMin, TestAreaMinX);
            rect.xMax = Mathf.Min(rect.xMax, TestAreaMaxX);
            rect.yMin = Mathf.Max(rect.yMin, TestAreaMinY);
            rect.yMax = Mathf.Min(rect.yMax, TopRoomCutoffY);
            if (rect.width < MinimumRectSize || rect.height < MinimumRectSize)
            {
                continue;
            }

            if (!guideRects.Any(guide => RectsOverlap(rect, guide)))
            {
                continue;
            }

            result.Add(rect);
        }

        return result;
    }

    private static List<Rect> BuildUnionRects(List<Rect> sources)
    {
        if (sources.Count == 0)
        {
            return new List<Rect>();
        }

        List<Rect> consolidated = new List<Rect>();
        foreach (Rect source in sources.OrderByDescending(rect => rect.width * rect.height))
        {
            if (consolidated.Any(rect => ContainsRect(rect, source)))
            {
                continue;
            }

            consolidated.RemoveAll(rect => ContainsRect(source, rect));
            consolidated.Add(source);
        }

        bool changed;
        do
        {
            changed = false;
            for (int first = 0; first < consolidated.Count && !changed; first++)
            {
                for (int second = first + 1; second < consolidated.Count; second++)
                {
                    if (!TryMergeCollinear(consolidated[first], consolidated[second], out Rect merged))
                    {
                        continue;
                    }

                    consolidated[first] = merged;
                    consolidated.RemoveAt(second);
                    changed = true;
                    break;
                }
            }
        }
        while (changed);

        return consolidated
            .Where(rect => rect.width >= MinimumRectSize && rect.height >= MinimumRectSize)
            .OrderBy(rect => rect.center.y)
            .ThenBy(rect => rect.xMin)
            .ToList();
    }

    private static bool TryMergeCollinear(Rect first, Rect second, out Rect merged)
    {
        const float alignmentTolerance = 0.035f;
        const float joinTolerance = 0.06f;
        bool sameHorizontalBand = Mathf.Abs(first.yMin - second.yMin) <= alignmentTolerance &&
                                  Mathf.Abs(first.yMax - second.yMax) <= alignmentTolerance;
        bool horizontalIntervalsJoin = first.xMin <= second.xMax + joinTolerance &&
                                       second.xMin <= first.xMax + joinTolerance;
        if (sameHorizontalBand && horizontalIntervalsJoin)
        {
            merged = Rect.MinMaxRect(
                Mathf.Min(first.xMin, second.xMin),
                Mathf.Max(first.yMin, second.yMin),
                Mathf.Max(first.xMax, second.xMax),
                Mathf.Min(first.yMax, second.yMax));
            return true;
        }

        bool sameVerticalBand = Mathf.Abs(first.xMin - second.xMin) <= alignmentTolerance &&
                                Mathf.Abs(first.xMax - second.xMax) <= alignmentTolerance;
        bool verticalIntervalsJoin = first.yMin <= second.yMax + joinTolerance &&
                                     second.yMin <= first.yMax + joinTolerance;
        if (sameVerticalBand && verticalIntervalsJoin)
        {
            merged = Rect.MinMaxRect(
                Mathf.Max(first.xMin, second.xMin),
                Mathf.Min(first.yMin, second.yMin),
                Mathf.Min(first.xMax, second.xMax),
                Mathf.Max(first.yMax, second.yMax));
            return true;
        }

        merged = default;
        return false;
    }

    private static bool ContainsRect(Rect outer, Rect inner)
    {
        const float tolerance = 0.001f;
        return outer.xMin <= inner.xMin + tolerance && outer.xMax >= inner.xMax - tolerance &&
               outer.yMin <= inner.yMin + tolerance && outer.yMax >= inner.yMax - tolerance;
    }

    private static void CreateFillTiles(
        Rect source,
        Transform parent,
        Dictionary<int, Sprite> sprites,
        Dictionary<int, int> usageCounts,
        ref int serial)
    {
        Rect rect = InsetRect(source, VisualInset);
        if (rect.width < MinimumRectSize || rect.height < MinimumRectSize)
        {
            return;
        }

        int seed = StableSeed(rect.center, Mathf.RoundToInt(rect.width * 10f), Mathf.RoundToInt(rect.height * 10f));
        int[] variants = PositiveModulo(seed, 6) == 0 ? CrackedBrickSprites : BasicBrickSprites;
        int spriteIndex = variants[PositiveModulo(seed, variants.Length)];
        CreateTiledSpriteObject(
            parent,
            $"Fill_{serial:0000}_Sprite{spriteIndex:00}",
            sprites[spriteIndex],
            spriteIndex,
            rect.center,
            new Vector2(rect.width * 0.995f, rect.height * 0.995f),
            FillTileTargetSize / Mathf.Max(sprites[spriteIndex].bounds.size.x, sprites[spriteIndex].bounds.size.y),
            FillSortingOrder,
            usageCounts);
        serial++;
    }

    private static void CreateSurfaceDetail(
        Rect source,
        Transform parent,
        Dictionary<int, Sprite> sprites,
        Dictionary<int, int> usageCounts,
        ref int serial)
    {
        Rect rect = InsetRect(source, VisualInset * 1.5f);
        if (rect.width < MinimumRectSize || rect.height < MinimumRectSize)
        {
            return;
        }

        float horizontalRatio = rect.width / Mathf.Max(rect.height, MinimumRectSize);
        float verticalRatio = rect.height / Mathf.Max(rect.width, MinimumRectSize);
        int seed = StableSeed(rect.center, Mathf.RoundToInt(rect.width * 10f), Mathf.RoundToInt(rect.height * 10f));

        if (horizontalRatio >= 1.8f)
        {
            int spriteIndex = PlatformSprites[PositiveModulo(seed, PlatformSprites.Length)];
            float uniformScale = rect.height / Mathf.Max(sprites[spriteIndex].bounds.size.y, 0.0001f);
            CreateTiledSpriteObject(
                parent,
                $"Platform_{serial:0000}_Sprite{spriteIndex:00}",
                sprites[spriteIndex],
                spriteIndex,
                rect.center,
                new Vector2(rect.width * 0.995f, rect.height * 0.995f),
                uniformScale,
                DetailSortingOrder,
                usageCounts);
            serial++;

            return;
        }

        if (verticalRatio >= 1.8f)
        {
            int spriteIndex = PillarSprites[PositiveModulo(seed, PillarSprites.Length)];
            float uniformScale = rect.width / Mathf.Max(sprites[spriteIndex].bounds.size.x, 0.0001f);
            CreateTiledSpriteObject(
                parent,
                $"Pillar_{serial:0000}_Sprite{spriteIndex:00}",
                sprites[spriteIndex],
                spriteIndex,
                rect.center,
                new Vector2(rect.width * 0.995f, rect.height * 0.995f),
                uniformScale,
                DetailSortingOrder,
                usageCounts);
            serial++;

            return;
        }

        if (horizontalRatio >= 1.2f && rect.height <= 2f)
        {
            int spriteIndex = ShortWallSprites[PositiveModulo(seed, ShortWallSprites.Length)];
            CreateSpriteObject(
                parent,
                $"ShortWall_{serial:0000}_Sprite{spriteIndex:00}",
                sprites[spriteIndex],
                spriteIndex,
                rect.center,
                new Vector2(rect.width * 0.995f, rect.height * 0.995f),
                DetailSortingOrder,
                usageCounts);
            serial++;
            return;
        }

        if (rect.width >= 1.15f && rect.height >= 1.15f && PositiveModulo(seed, 3) == 0)
        {
            int spriteIndex = CornerSprites[PositiveModulo(seed, CornerSprites.Length)];
            float size = Mathf.Min(1.4f, rect.width * 0.72f, rect.height * 0.72f);
            Vector2 center = new Vector2(rect.xMin + size * 0.5f, rect.yMax - size * 0.5f);
            CreateSpriteObject(
                parent,
                $"Corner_{serial:0000}_Sprite{spriteIndex:00}",
                sprites[spriteIndex],
                spriteIndex,
                center,
                new Vector2(size * 0.995f, size * 0.995f),
                DetailSortingOrder,
                usageCounts);
            serial++;
        }
    }

    private static int CreatePolygonFillTiles(
        PolygonCollider2D[] polygons,
        Renderer[] guideRenderers,
        Dictionary<string, Transform> regionRoots,
        Dictionary<int, Sprite> sprites,
        Dictionary<int, int> usageCounts,
        Dictionary<string, int> regionCounts,
        ref int serial)
    {
        const float targetCellSize = 0.65f;
        List<Rect> guideRects = guideRenderers.Select(renderer => ToRect(renderer.bounds)).ToList();
        int created = 0;
        foreach (PolygonCollider2D polygon in polygons)
        {
            Rect bounds = ToRect(polygon.bounds);
            bounds.xMin = Mathf.Max(bounds.xMin, TestAreaMinX);
            bounds.xMax = Mathf.Min(bounds.xMax, TestAreaMaxX);
            bounds.yMin = Mathf.Max(bounds.yMin, TestAreaMinY);
            bounds.yMax = Mathf.Min(bounds.yMax, TopRoomCutoffY);
            if (bounds.width < targetCellSize * 0.8f || bounds.height < targetCellSize * 0.8f ||
                !guideRects.Any(guide => RectsOverlap(bounds, guide)))
            {
                continue;
            }

            int columns = Mathf.Max(1, Mathf.CeilToInt(bounds.width / targetCellSize));
            int rows = Mathf.Max(1, Mathf.CeilToInt(bounds.height / targetCellSize));
            float cellWidth = bounds.width / columns;
            float cellHeight = bounds.height / rows;
            for (int y = 0; y < rows; y++)
            {
                int runStart = -1;
                for (int x = 0; x <= columns; x++)
                {
                    bool safe = false;
                    if (x < columns)
                    {
                        Vector2 cellCenter = new Vector2(
                            bounds.xMin + (x + 0.5f) * cellWidth,
                            bounds.yMin + (y + 0.5f) * cellHeight);
                        Vector2 cellSize = new Vector2(cellWidth * 0.94f, cellHeight * 0.94f);
                        Bounds candidate = new Bounds(cellCenter, new Vector3(cellSize.x, cellSize.y, 0f));
                        safe = BoundsInsideColliderUnion(candidate, new Collider2D[] { polygon });
                    }

                    if (safe && runStart < 0)
                    {
                        runStart = x;
                    }

                    if (safe || runStart < 0)
                    {
                        continue;
                    }

                    int runEnd = x;
                    float runWidth = (runEnd - runStart) * cellWidth * 0.97f;
                    Vector2 center = new Vector2(
                        bounds.xMin + (runStart + runEnd) * 0.5f * cellWidth,
                        bounds.yMin + (y + 0.5f) * cellHeight);
                    Vector2 size = new Vector2(runWidth, cellHeight * 0.94f);
                    int seed = StableSeed(center, runStart, y);
                    int spriteIndex = ShortWallSprites[PositiveModulo(seed, ShortWallSprites.Length)];
                    string regionName = GetRegionName(center.y);
                    CreateSpriteObject(
                        regionRoots[regionName],
                        $"PolygonFill_{serial:0000}_Sprite{spriteIndex:00}",
                        sprites[spriteIndex],
                        spriteIndex,
                        center,
                        size,
                        FillSortingOrder,
                        usageCounts);
                    serial++;
                    created++;
                    regionCounts[regionName]++;
                    runStart = -1;
                }
            }
        }

        return created;
    }

    private static void CreateSpriteObject(
        Transform parent,
        string name,
        Sprite sprite,
        int spriteIndex,
        Vector2 worldCenter,
        Vector2 worldSize,
        int sortingOrder,
        Dictionary<int, int> usageCounts)
    {
        GameObject item = new GameObject(name);
        item.transform.SetParent(parent, false);
        item.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
        item.transform.rotation = Quaternion.identity;
        SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;

        Vector3 parentScale = item.transform.parent != null ? item.transform.parent.lossyScale : Vector3.one;
        item.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(sprite.bounds.size.x * Mathf.Abs(parentScale.x), 0.0001f),
            worldSize.y / Mathf.Max(sprite.bounds.size.y * Mathf.Abs(parentScale.y), 0.0001f),
            1f);
        usageCounts.TryGetValue(spriteIndex, out int current);
        usageCounts[spriteIndex] = current + 1;
    }

    private static void CreateTiledSpriteObject(
        Transform parent,
        string name,
        Sprite sprite,
        int spriteIndex,
        Vector2 worldCenter,
        Vector2 worldSize,
        float uniformWorldScale,
        int sortingOrder,
        Dictionary<int, int> usageCounts)
    {
        GameObject item = new GameObject(name);
        item.transform.SetParent(parent, false);
        item.transform.position = new Vector3(worldCenter.x, worldCenter.y, 0f);
        item.transform.rotation = Quaternion.identity;
        SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;

        float safeScale = Mathf.Max(uniformWorldScale, 0.0001f);
        Vector3 parentScale = item.transform.parent != null ? item.transform.parent.lossyScale : Vector3.one;
        item.transform.localScale = new Vector3(
            safeScale / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
            safeScale / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
            1f);
        renderer.size = new Vector2(worldSize.x / safeScale, worldSize.y / safeScale);
        usageCounts.TryGetValue(spriteIndex, out int current);
        usageCounts[spriteIndex] = current + 1;
    }

    private static bool TryValidateTestRoot(
        Transform testRoot,
        Collider2D[] colliders,
        out string summary,
        out string error)
    {
        Collider2D[] addedColliders = testRoot.GetComponentsInChildren<Collider2D>(true);
        if (addedColliders.Length != 0)
        {
            summary = string.Empty;
            error = $"테스트 아트 아래에 Collider2D {addedColliders.Length}개가 있습니다.";
            return false;
        }

        if (testRoot.GetComponentsInChildren<Platform2D>(true).Length != 0)
        {
            summary = string.Empty;
            error = "테스트 아트 아래에 Platform2D가 있습니다.";
            return false;
        }

        SpriteRenderer[] renderers = testRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0)
        {
            summary = string.Empty;
            error = "생성된 SpriteRenderer가 없습니다.";
            return false;
        }

        SpriteRenderer playerRenderer = FindPlayerRenderer();
        int playerOrder = playerRenderer != null ? playerRenderer.sortingOrder : 10;
        HashSet<string> spritePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (!string.Equals(renderer.sortingLayerName, "Default", StringComparison.Ordinal) ||
                renderer.sortingOrder < FillSortingOrder ||
                renderer.sortingOrder > DetailSortingOrder ||
                renderer.sortingOrder >= playerOrder)
            {
                summary = string.Empty;
                error = $"정렬 설정이 잘못되었습니다: {GetPath(renderer.transform)}";
                return false;
            }

            if (renderer.bounds.max.y > TopRoomCutoffY + 0.001f)
            {
                summary = string.Empty;
                error = $"상단 방 경계를 넘은 아트가 있습니다: {GetPath(renderer.transform)}";
                return false;
            }

            if (!BoundsInsideColliderUnion(renderer.bounds, colliders))
            {
                summary = string.Empty;
                error = $"Collider 범위를 벗어난 아트 Bounds가 있습니다: {GetPath(renderer.transform)} bounds={renderer.bounds}";
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(renderer.sprite);
            if (!IsAllowedSpritePath(assetPath))
            {
                summary = string.Empty;
                error = $"허용되지 않은 스프라이트를 사용했습니다: {assetPath}";
                return false;
            }

            spritePaths.Add(assetPath);
        }

        summary =
            $"renderers={renderers.Length} uniqueSprites={spritePaths.Count} " +
            $"sorting=Default:{FillSortingOrder}-{DetailSortingOrder} playerOrder={playerOrder} " +
            $"topY={renderers.Max(renderer => renderer.bounds.max.y):F3}";
        error = string.Empty;
        return true;
    }

    private static bool BoundsInsideColliderUnion(Bounds bounds, Collider2D[] colliders)
    {
        const int samples = 5;
        for (int y = 0; y < samples; y++)
        {
            for (int x = 0; x < samples; x++)
            {
                float tx = Mathf.Lerp(0.03f, 0.97f, x / (samples - 1f));
                float ty = Mathf.Lerp(0.03f, 0.97f, y / (samples - 1f));
                Vector2 point = new Vector2(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, tx),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, ty));
                if (!colliders.Any(collider => collider.OverlapPoint(point)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsAllowedSpritePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(SpriteFolder + "/sprite_", StringComparison.Ordinal))
        {
            return false;
        }

        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        if (!int.TryParse(fileName.Substring("sprite_".Length), out int index))
        {
            return false;
        }

        return BasicBrickSprites.Contains(index) ||
               CrackedBrickSprites.Contains(index) ||
               PlatformSprites.Contains(index) ||
               ShortWallSprites.Contains(index) ||
               CornerSprites.Contains(index) ||
               PillarSprites.Contains(index);
    }

    private static string CaptureProtectedState(Transform collision, Transform guides)
    {
        StringBuilder state = new StringBuilder(65536);
        AppendProtectedHierarchy(state, "Collision", collision);
        AppendProtectedHierarchy(state, "CollisionGuides", guides);
        return state.ToString();
    }

    private static void AppendProtectedHierarchy(StringBuilder state, string label, Transform root)
    {
        state.AppendLine(label);
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true)
            .OrderBy(GetIndexedPath, StringComparer.Ordinal)
            .ToArray();
        foreach (Transform transform in transforms)
        {
            state.Append(GetIndexedPath(transform)).Append('|')
                .Append(transform.gameObject.activeSelf).Append('|')
                .Append(transform.localPosition.ToString("R")).Append('|')
                .Append(transform.localRotation.ToString("R")).Append('|')
                .Append(transform.localScale.ToString("R")).AppendLine();
        }

        Component[] components = root.GetComponentsInChildren<Component>(true)
            .Where(component => component != null && !(component is Transform))
            .OrderBy(component => GetIndexedPath(component.transform), StringComparer.Ordinal)
            .ThenBy(component => GetComponentIndex(component))
            .ToArray();
        foreach (Component component in components)
        {
            state.Append(GetIndexedPath(component.transform)).Append('|')
                .Append(component.GetType().FullName).Append('|')
                .Append(EditorJsonUtility.ToJson(component, false));
            if (component is Collider2D collider)
            {
                state.Append('|').Append(collider.bounds.ToString("R"));
            }
            else if (component is Renderer renderer)
            {
                state.Append('|').Append(renderer.bounds.ToString("R"));
            }

            state.AppendLine();
        }
    }

    private static int GetComponentIndex(Component target)
    {
        Component[] components = target.gameObject.GetComponents<Component>();
        return Array.IndexOf(components, target);
    }

    private static string GetIndexedPath(Transform transform)
    {
        List<string> parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add($"{current.GetSiblingIndex():D4}:{current.name}");
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static bool IsWorldAxisAligned(BoxCollider2D box)
    {
        Vector2 right = box.transform.TransformVector(Vector3.right);
        Vector2 up = box.transform.TransformVector(Vector3.up);
        if (right.sqrMagnitude < 0.000001f || up.sqrMagnitude < 0.000001f)
        {
            return false;
        }

        right.Normalize();
        up.Normalize();
        float rightAxis = Mathf.Max(Mathf.Abs(Vector2.Dot(right, Vector2.right)), Mathf.Abs(Vector2.Dot(right, Vector2.up)));
        float upAxis = Mathf.Max(Mathf.Abs(Vector2.Dot(up, Vector2.right)), Mathf.Abs(Vector2.Dot(up, Vector2.up)));
        return rightAxis > 0.9995f && upAxis > 0.9995f;
    }

    private static Rect ToRect(Bounds bounds)
    {
        return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
    }

    private static Rect InsetRect(Rect rect, float inset)
    {
        float xInset = Mathf.Min(inset, rect.width * 0.08f);
        float yInset = Mathf.Min(inset, rect.height * 0.08f);
        return Rect.MinMaxRect(rect.xMin + xInset, rect.yMin + yInset, rect.xMax - xInset, rect.yMax - yInset);
    }

    private static bool RectsOverlap(Rect a, Rect b)
    {
        return a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
    }

    private static int StableSeed(Vector2 center, int x, int y)
    {
        int centerX = Mathf.RoundToInt(center.x * 100f);
        int centerY = Mathf.RoundToInt(center.y * 100f);
        unchecked
        {
            return centerX * 73856093 ^ centerY * 19349663 ^ x * 83492791 ^ y * 297121507;
        }
    }

    private static int PositiveModulo(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static string GetSpritePath(int index)
    {
        return $"{SpriteFolder}/sprite_{index:00}.png";
    }

    private static void ResetLocalTransform(Transform transform)
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private static bool TryFindStageRoots(
        out Transform collision,
        out Transform stageArt,
        out Transform guides,
        out string error)
    {
        collision = null;
        stageArt = null;
        guides = null;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            error = "활성 씬이 없습니다.";
            return false;
        }

        Transform stage = scene.GetRootGameObjects()
            .Select(root => root.transform)
            .FirstOrDefault(root => string.Equals(root.name, StageRootName, StringComparison.Ordinal));
        if (stage == null)
        {
            error = $"활성 씬에서 {StageRootName} 루트를 찾지 못했습니다.";
            return false;
        }

        collision = stage.Find(CollisionPath);
        stageArt = stage.Find(StageArtPath);
        guides = stage.Find(CollisionGuidesPath);
        if (collision == null || stageArt == null || guides == null)
        {
            error =
                $"필수 계층을 찾지 못했습니다. Collision={collision != null}, " +
                $"StageArt={stageArt != null}, CollisionGuides={guides != null}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static SpriteRenderer FindPlayerRenderer()
    {
        GameObject player = GameObject.Find("Player");
        return player != null ? player.GetComponentInChildren<SpriteRenderer>(true) : null;
    }

    private static string GetPath(Transform transform)
    {
        List<string> names = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }
}
