using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public enum PlatformShape2D
{
    Rectangle = 0,
    [InspectorName("Triangle Up")]
    TriangleUp = 1,
    [InspectorName("Triangle Right")]
    TriangleRight = 2,
    [InspectorName("Triangle Down")]
    TriangleDown = 3,
    [InspectorName("Triangle Left")]
    TriangleLeft = 4,
    [InspectorName("Right Triangle Bottom Left")]
    RightTriangleBottomLeft = 5,
    [InspectorName("Right Triangle Bottom Right")]
    RightTriangleBottomRight = 6,
    [InspectorName("Right Triangle Top Right")]
    RightTriangleTopRight = 7,
    [InspectorName("Right Triangle Top Left")]
    RightTriangleTopLeft = 8
}

/// <summary>
/// 맵 제작용 Platform의 도형, 크기와 표면 정보를 관리합니다.
/// Width/Height와 삼각형 꼭지점을 변경하면 표시와 충돌체를 항상 같은 좌표로 갱신합니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class Platform2D : MonoBehaviour, IPlatformSurface
{
    private const float MinimumSize = 0.1f;
    private const float SizeTolerance = 0.0001f;
    private const float MinimumTriangleArea = 0.0005f;
    private const int RightTriangleSpritePixels = 2048;
    private const int RightTriangleSpriteSupersampling = 2;
    private const string VisualRootName = "Visual";
    private const string TriangleVisualName = "Triangle Visual";
    private const string RightTriangleSpriteNamePrefix = "Platform2D Right Triangle";
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int RendererColorPropertyId = Shader.PropertyToID("_RendererColor");
    private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");
    private static Sprite rightTriangleBottomLeftSprite;
    private static Sprite rightTriangleBottomRightSprite;
    private static Sprite rightTriangleTopRightSprite;
    private static Sprite rightTriangleTopLeftSprite;

    [Header("Platform Size")]
    [SerializeField] private PlatformShape2D shape = PlatformShape2D.Rectangle;
    [SerializeField, Min(MinimumSize)] private float width = 2.5f;
    [SerializeField, Min(MinimumSize)] private float height = 0.3f;

    [Header("Triangle Vertices")]
    [SerializeField] private Vector2 triangleVertexA = new Vector2(-1.25f, -0.15f);
    [SerializeField] private Vector2 triangleVertexB = new Vector2(1.25f, -0.15f);
    [SerializeField] private Vector2 triangleVertexC = new Vector2(0f, 0.15f);

    [Header("Visual")]
    [SerializeField] private bool renderVisuals = true;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private BoxCollider2D boxCollider;
    [SerializeField] private PolygonCollider2D polygonCollider;
    [SerializeField] private MeshFilter triangleMeshFilter;
    [SerializeField] private MeshRenderer triangleMeshRenderer;
    [SerializeField] private Sprite rectangleSprite;

#if UNITY_EDITOR
    [System.NonSerialized] private bool applySizeScheduled;
#endif
    private static Material fallbackTriangleMaterial;
    private Mesh triangleMesh;
    private MaterialPropertyBlock trianglePropertyBlock;

    public PlatformShape2D Shape => NormalizeShape(shape);
    public float Width => width;
    public float Height => height;
    public Vector2 Size => new Vector2(width, height);
    public float RotationDegrees => NormalizeSignedDegrees(transform.localEulerAngles.z);
    public bool UsesTriangleShape => IsTriangleShape;
    public bool UsesRightTriangleShape => IsRightTriangleShape;
    public bool RendersVisuals => renderVisuals;
    public SpriteRenderer VisualRenderer
    {
        get
        {
            CacheReferences();
            return spriteRenderer;
        }
    }
    public Bounds WorldBounds
    {
        get
        {
            if (IsTriangleShape && polygonCollider != null)
            {
                return polygonCollider.bounds;
            }

            return boxCollider != null ? boxCollider.bounds : new Bounds(transform.position, Size);
        }
    }

    public Vector2 TopCenter
    {
        get
        {
            Bounds bounds = WorldBounds;
            return new Vector2(bounds.center.x, bounds.max.y);
        }
    }

    private void OnEnable()
    {
        ApplySize();
    }

#if UNITY_EDITOR
    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        SyncColliderToVisibleSize();
    }
#endif

    public void SetSize(float newWidth, float newHeight)
    {
        Vector2 newSize = ClampSize(new Vector2(newWidth, newHeight));
        width = newSize.x;
        height = newSize.y;

        if (IsTriangleShape)
        {
            ResizeTriangleToSize(newSize);
        }

        ApplySize();
    }

    public void SetShape(PlatformShape2D newShape)
    {
        PlatformShape2D previousShape = Shape;
        shape = NormalizeShape(newShape);
        if (IsTriangleShape && previousShape != shape)
        {
            ResetTriangleVerticesToCurrentShape();
        }

        ApplySize();
    }

    public void SetRotationDegrees(float degrees)
    {
        transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }

    public void SetVisualRenderingEnabled(bool enabled)
    {
        if (renderVisuals == enabled)
        {
            return;
        }

        renderVisuals = enabled;
        ApplySize();
    }

    public Vector2 GetTriangleVertex(int index)
    {
        if (IsTriangleShape)
        {
            EnsureTriangleVertices();
        }

        Vector2[] points = GetStoredTrianglePoints();
        int clampedIndex = Mathf.Clamp(index, 0, points.Length - 1);
        return points[clampedIndex];
    }

    public Vector2[] GetTriangleVertices()
    {
        if (IsTriangleShape)
        {
            EnsureTriangleVertices();
        }

        return GetStoredTrianglePoints();
    }

    public bool SetTriangleVertex(int index, Vector2 localPosition)
    {
        if (!IsTriangleShape || index < 0 || index > 2)
        {
            return false;
        }

        if (IsRightTriangleShape)
        {
            return SetRightTriangleVertex(index, localPosition);
        }

        Vector2[] points = GetTrianglePoints();
        points[index] = localPosition;
        if (!HasUsableTriangle(points))
        {
            return false;
        }

        SetStoredTrianglePoints(points);
        UpdateSizeFromTrianglePoints(points);
        ApplySize();
        return true;
    }

    public bool SetTriangleVertices(Vector2 vertexA, Vector2 vertexB, Vector2 vertexC)
    {
        if (!IsTriangleShape)
        {
            return false;
        }

        Vector2[] points = { vertexA, vertexB, vertexC };
        if (IsRightTriangleShape)
        {
            points = GetRightTrianglePointsForBounds(CalculateLocalBounds(points), Shape);
        }

        if (!HasUsableTriangle(points))
        {
            return false;
        }

        SetStoredTrianglePoints(points);
        UpdateSizeFromTrianglePoints(points);
        ApplySize();
        return true;
    }

    public void ResetTriangleVerticesToCurrentShape()
    {
        if (!IsTriangleShape)
        {
            return;
        }

        Vector2[] points = GetTrianglePresetPoints(Size, Shape);
        SetStoredTrianglePoints(points);
        UpdateSizeFromTrianglePoints(points);
        ApplySize();
    }

    public bool TryGetTopLandingSegment(float playerHalfWidth, out float minStandX, out float maxStandX, out float centerLandingY)
    {
        Bounds bounds = WorldBounds;
        centerLandingY = bounds.max.y;
        minStandX = bounds.min.x + playerHalfWidth;
        maxStandX = bounds.max.x - playerHalfWidth;

        switch (Shape)
        {
            case PlatformShape2D.Rectangle:
                return TryGetRectangleTopLandingSegment(playerHalfWidth, out minStandX, out maxStandX, out centerLandingY);
            case PlatformShape2D.TriangleUp:
            case PlatformShape2D.TriangleRight:
            case PlatformShape2D.TriangleDown:
            case PlatformShape2D.TriangleLeft:
            case PlatformShape2D.RightTriangleBottomLeft:
            case PlatformShape2D.RightTriangleBottomRight:
            case PlatformShape2D.RightTriangleTopRight:
            case PlatformShape2D.RightTriangleTopLeft:
                return TryGetTriangleTopLandingSegment(playerHalfWidth, out minStandX, out maxStandX, out centerLandingY);
            default:
                return false;
        }
    }

    public void ApplySize()
    {
        CacheReferences();

        shape = NormalizeShape(shape);
        width = Mathf.Max(MinimumSize, width);
        height = Mathf.Max(MinimumSize, height);

        if (IsTriangleShape)
        {
            EnsureTriangleVertices();
            ApplyTriangleShape();
            return;
        }

        ApplyRectangleShape();
    }

    private void SyncColliderToVisibleSize()
    {
#if UNITY_EDITOR
        if (applySizeScheduled)
        {
            return;
        }
#endif

        CacheReferences();

        if (IsTriangleShape)
        {
            if (IsRightTriangleShape)
            {
                SyncRightTriangleToVisibleSize();
                return;
            }

            ApplyTriangleShape();
            return;
        }

        Vector2 visibleSize = Size;
        if (renderVisuals && spriteRenderer != null)
        {
            if (spriteRenderer.drawMode != SpriteDrawMode.Sliced)
            {
                spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            }

            visibleSize = ClampSize(spriteRenderer.size);
            if (!Approximately(spriteRenderer.size, visibleSize))
            {
                spriteRenderer.size = visibleSize;
            }
        }

#if UNITY_EDITOR
        bool changed = false;
#endif
        if (!Approximately(Size, visibleSize))
        {
            width = visibleSize.x;
            height = visibleSize.y;
#if UNITY_EDITOR
            changed = true;
#endif
        }

        if (boxCollider != null)
        {
            if (!Approximately(boxCollider.size, visibleSize))
            {
                boxCollider.size = visibleSize;
#if UNITY_EDITOR
                changed = true;
#endif
            }

            if (!Approximately(boxCollider.offset, Vector2.zero))
            {
                boxCollider.offset = Vector2.zero;
#if UNITY_EDITOR
                changed = true;
#endif
            }
        }

#if UNITY_EDITOR
        if (changed)
        {
            EditorUtility.SetDirty(this);
            if (boxCollider != null)
            {
                EditorUtility.SetDirty(boxCollider);
            }

            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif
    }

    private void CacheReferences()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = FindVisualSpriteRenderer();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        StoreRectangleSpriteIfNeeded();

        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider2D>();
        }

        if (polygonCollider == null)
        {
            polygonCollider = GetComponent<PolygonCollider2D>();
        }

        CacheTriangleVisualReferences();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        shape = NormalizeShape(shape);
        width = Mathf.Max(MinimumSize, width);
        height = Mathf.Max(MinimumSize, height);

        ScheduleApplySize();
        return;
#else
        ApplySize();
#endif
    }

#if UNITY_EDITOR
    private void ScheduleApplySize()
    {
        if (applySizeScheduled)
        {
            return;
        }

        applySizeScheduled = true;
        EditorApplication.delayCall += ApplySizeDelayed;
    }

    private void ApplySizeDelayed()
    {
        applySizeScheduled = false;
        if (this == null)
        {
            return;
        }

        ApplySize();
    }
#endif

    private static Vector2 ClampSize(Vector2 size)
    {
        return new Vector2(
            Mathf.Max(MinimumSize, Mathf.Abs(size.x)),
            Mathf.Max(MinimumSize, Mathf.Abs(size.y)));
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return (a - b).sqrMagnitude <= SizeTolerance * SizeTolerance;
    }

    private static float NormalizeSignedDegrees(float degrees)
    {
        return Mathf.Abs(degrees) <= SizeTolerance ? 0f : Mathf.DeltaAngle(0f, degrees);
    }

    private bool IsTriangleShape => Shape != PlatformShape2D.Rectangle;
    private bool IsRightTriangleShape => IsRightTriangleShapeValue(Shape);

    private void ApplyRectangleShape()
    {
        Vector2 platformSize = Size;

        if (spriteRenderer != null)
        {
            if (!renderVisuals)
            {
                spriteRenderer.enabled = false;
            }
            else
            {
                if (rectangleSprite != null && spriteRenderer.sprite != rectangleSprite)
                {
                    spriteRenderer.sprite = rectangleSprite;
                }

                spriteRenderer.enabled = true;
                spriteRenderer.drawMode = SpriteDrawMode.Sliced;
                spriteRenderer.size = platformSize;
            }
        }

        if (boxCollider != null)
        {
            boxCollider.enabled = true;
            boxCollider.size = platformSize;
            boxCollider.offset = Vector2.zero;
        }

        if (polygonCollider != null)
        {
            polygonCollider.enabled = false;
        }

        if (triangleMeshRenderer != null)
        {
            triangleMeshRenderer.enabled = false;
        }
    }

    private void ApplyTriangleShape()
    {
        Vector2[] points = GetTrianglePoints();
        if (IsRightTriangleShape)
        {
            ApplyRightTriangleShape(points);
            return;
        }

        EnsurePolygonCollider();

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }

        if (polygonCollider != null)
        {
            polygonCollider.enabled = true;
            polygonCollider.offset = Vector2.zero;
            polygonCollider.pathCount = 1;
            polygonCollider.SetPath(0, points);
        }

        if (!renderVisuals)
        {
            if (triangleMeshRenderer != null)
            {
                triangleMeshRenderer.enabled = false;
            }

            return;
        }

        EnsureTriangleReferences();

        if (triangleMeshFilter != null)
        {
            Mesh mesh = GetOrCreateTriangleMesh();
            UpdateTriangleMesh(mesh, points);
            triangleMeshFilter.sharedMesh = mesh;
        }

        if (triangleMeshRenderer != null)
        {
            triangleMeshRenderer.enabled = true;
            triangleMeshRenderer.sharedMaterial = GetTriangleMaterial();
            SyncTriangleRendererAppearance();
        }
    }

    private void ApplyRightTriangleShape(Vector2[] points)
    {
        RemoveTriangleVisualIfPresent();
        EnsurePolygonCollider();

        Vector2 platformSize = Size;
        if (spriteRenderer != null)
        {
            if (!renderVisuals)
            {
                spriteRenderer.enabled = false;
            }
            else
            {
                StoreRectangleSpriteIfNeeded();
                spriteRenderer.enabled = true;
                spriteRenderer.sprite = GetRightTriangleSprite(Shape);
                spriteRenderer.drawMode = SpriteDrawMode.Sliced;
                spriteRenderer.size = platformSize;
            }
        }

        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }

        if (polygonCollider != null)
        {
            polygonCollider.enabled = true;
            polygonCollider.offset = Vector2.zero;
            polygonCollider.pathCount = 1;
            polygonCollider.SetPath(0, points);
        }
    }

    private void SyncRightTriangleToVisibleSize()
    {
        Vector2 visibleSize = Size;
        if (renderVisuals && spriteRenderer != null)
        {
            StoreRectangleSpriteIfNeeded();
            if (spriteRenderer.sprite == null || !IsGeneratedRightTriangleSprite(spriteRenderer.sprite))
            {
                spriteRenderer.sprite = GetRightTriangleSprite(Shape);
            }

            if (spriteRenderer.drawMode != SpriteDrawMode.Sliced)
            {
                spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            }

            visibleSize = ClampSize(spriteRenderer.size);
            if (!Approximately(spriteRenderer.size, visibleSize))
            {
                spriteRenderer.size = visibleSize;
            }
        }

#if UNITY_EDITOR
        bool changed = false;
#endif
        if (!Approximately(Size, visibleSize))
        {
            width = visibleSize.x;
            height = visibleSize.y;
            ResizeTriangleToSize(visibleSize);
#if UNITY_EDITOR
            changed = true;
#endif
        }

        ApplyRightTriangleShape(GetTrianglePoints());

#if UNITY_EDITOR
        if (changed)
        {
            EditorUtility.SetDirty(this);
            if (spriteRenderer != null)
            {
                EditorUtility.SetDirty(spriteRenderer);
            }

            if (polygonCollider != null)
            {
                EditorUtility.SetDirty(polygonCollider);
            }

            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif
    }

    private void EnsureTriangleReferences()
    {
        EnsurePolygonCollider();

        Transform triangleVisual = GetOrCreateTriangleVisual();

        if (triangleMeshFilter == null || triangleMeshFilter.transform != triangleVisual)
        {
            triangleMeshFilter = triangleVisual.GetComponent<MeshFilter>();
            if (triangleMeshFilter == null)
            {
                triangleMeshFilter = triangleVisual.gameObject.AddComponent<MeshFilter>();
            }
        }

        if (triangleMeshRenderer == null || triangleMeshRenderer.transform != triangleVisual)
        {
            triangleMeshRenderer = triangleVisual.GetComponent<MeshRenderer>();
            if (triangleMeshRenderer == null)
            {
                triangleMeshRenderer = triangleVisual.gameObject.AddComponent<MeshRenderer>();
            }
        }
    }

    private void EnsurePolygonCollider()
    {
        if (polygonCollider == null)
        {
            polygonCollider = GetComponent<PolygonCollider2D>();
            if (polygonCollider == null)
            {
                polygonCollider = gameObject.AddComponent<PolygonCollider2D>();
            }
        }
    }

    private void StoreRectangleSpriteIfNeeded()
    {
        if (spriteRenderer != null
            && spriteRenderer.sprite != null
            && rectangleSprite == null
            && !IsGeneratedRightTriangleSprite(spriteRenderer.sprite))
        {
            rectangleSprite = spriteRenderer.sprite;
        }
    }

    private void CacheTriangleVisualReferences()
    {
        Transform triangleVisual = FindTriangleVisual();
        if (triangleVisual == null)
        {
            if (triangleMeshFilter != null && triangleMeshFilter.transform == transform)
            {
                triangleMeshFilter = null;
            }

            if (triangleMeshRenderer != null && triangleMeshRenderer.transform == transform)
            {
                triangleMeshRenderer = null;
            }

            return;
        }

        if (triangleMeshFilter == null || triangleMeshFilter.transform != triangleVisual)
        {
            triangleMeshFilter = triangleVisual.GetComponent<MeshFilter>();
        }

        if (triangleMeshRenderer == null || triangleMeshRenderer.transform != triangleVisual)
        {
            triangleMeshRenderer = triangleVisual.GetComponent<MeshRenderer>();
        }
    }

    private Transform GetOrCreateTriangleVisual()
    {
        Transform triangleVisual = FindTriangleVisual();
        if (triangleVisual != null)
        {
            Transform visualRoot = GetOrCreateVisualRoot();
            if (triangleVisual.parent != visualRoot)
            {
                triangleVisual.SetParent(visualRoot, false);
            }

            ResetTriangleVisualTransform(triangleVisual);
            return triangleVisual;
        }

        GameObject visualObject = new GameObject(TriangleVisualName);
        visualObject.layer = gameObject.layer;
        triangleVisual = visualObject.transform;
        triangleVisual.SetParent(GetOrCreateVisualRoot(), false);
        ResetTriangleVisualTransform(triangleVisual);
#if UNITY_EDITOR
        MarkTriangleVisualDirty(visualObject);
#endif
        return triangleVisual;
    }

    private void RemoveTriangleVisualIfPresent()
    {
        Transform triangleVisual = FindTriangleVisual();
        if (triangleVisual == null)
        {
            return;
        }

        if (triangleMeshFilter != null && triangleMeshFilter.transform == triangleVisual)
        {
            triangleMeshFilter = null;
        }

        if (triangleMeshRenderer != null && triangleMeshRenderer.transform == triangleVisual)
        {
            triangleMeshRenderer = null;
        }

        if (Application.isPlaying)
        {
            Destroy(triangleVisual.gameObject);
        }
        else
        {
            DestroyImmediate(triangleVisual.gameObject);
#if UNITY_EDITOR
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }
    }

    private Transform FindTriangleVisual()
    {
        Transform visualRoot = FindVisualRoot();
        if (visualRoot != null)
        {
            Transform nestedVisual = visualRoot.Find(TriangleVisualName);
            if (nestedVisual != null)
            {
                return nestedVisual;
            }
        }

        Transform directChild = transform.Find(TriangleVisualName);
        if (directChild != null)
        {
            return directChild;
        }

        if (triangleMeshRenderer != null && triangleMeshRenderer.transform != transform)
        {
            return triangleMeshRenderer.transform;
        }

        if (triangleMeshFilter != null && triangleMeshFilter.transform != transform)
        {
            return triangleMeshFilter.transform;
        }

        return null;
    }

    private SpriteRenderer FindVisualSpriteRenderer()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name != VisualRootName)
            {
                continue;
            }

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                return renderer;
            }
        }

        return null;
    }

    private Transform FindVisualRoot()
    {
        SpriteRenderer renderer = FindVisualSpriteRenderer();
        if (renderer != null)
        {
            return renderer.transform;
        }

        return transform.Find(VisualRootName);
    }

    private Transform GetOrCreateVisualRoot()
    {
        Transform visualRoot = FindVisualRoot();
        if (visualRoot != null)
        {
            return visualRoot;
        }

        GameObject visualObject = new GameObject(VisualRootName);
        visualObject.layer = gameObject.layer;
        visualRoot = visualObject.transform;
        visualRoot.SetParent(transform, false);
#if UNITY_EDITOR
        MarkTriangleVisualDirty(visualObject);
#endif
        return visualRoot;
    }

    private static void ResetTriangleVisualTransform(Transform triangleVisual)
    {
        triangleVisual.localPosition = Vector3.zero;
        triangleVisual.localRotation = Quaternion.identity;
        triangleVisual.localScale = Vector3.one;
    }

#if UNITY_EDITOR
    private void MarkTriangleVisualDirty(GameObject visualObject)
    {
        if (visualObject == null || Application.isPlaying)
        {
            return;
        }

        EditorUtility.SetDirty(visualObject);
        EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }
#endif

    private Mesh GetOrCreateTriangleMesh()
    {
        if (triangleMesh == null)
        {
            triangleMesh = new Mesh
            {
                name = "Platform2D Triangle Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        return triangleMesh;
    }

    private void UpdateTriangleMesh(Mesh mesh, Vector2[] points)
    {
        mesh.Clear();
        mesh.vertices = new[]
        {
            new Vector3(points[0].x, points[0].y, 0f),
            new Vector3(points[1].x, points[1].y, 0f),
            new Vector3(points[2].x, points[2].y, 0f)
        };
        mesh.uv = GetTriangleUvs(points);
        mesh.colors = new[] { Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 1, 2, 2, 1, 0 };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    private Material GetTriangleMaterial()
    {
        if (spriteRenderer != null && spriteRenderer.sharedMaterial != null)
        {
            return spriteRenderer.sharedMaterial;
        }

        if (fallbackTriangleMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                fallbackTriangleMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                fallbackTriangleMaterial.mainTexture = Texture2D.whiteTexture;
                if (fallbackTriangleMaterial.HasProperty(ColorPropertyId))
                {
                    fallbackTriangleMaterial.SetColor(ColorPropertyId, Color.white);
                }
            }
        }

        return fallbackTriangleMaterial;
    }

    private void SyncTriangleRendererAppearance()
    {
        if (triangleMeshRenderer == null)
        {
            return;
        }

        if (spriteRenderer != null)
        {
            triangleMeshRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            triangleMeshRenderer.sortingOrder = spriteRenderer.sortingOrder;
        }

        if (trianglePropertyBlock == null)
        {
            trianglePropertyBlock = new MaterialPropertyBlock();
        }

        Color color = spriteRenderer != null ? spriteRenderer.color : Color.white;
        triangleMeshRenderer.GetPropertyBlock(trianglePropertyBlock);
        trianglePropertyBlock.Clear();
        trianglePropertyBlock.SetTexture(MainTexPropertyId, Texture2D.whiteTexture);
        trianglePropertyBlock.SetColor(ColorPropertyId, color);
        trianglePropertyBlock.SetColor(RendererColorPropertyId, Color.white);
        triangleMeshRenderer.SetPropertyBlock(trianglePropertyBlock);
    }

    private Vector2[] GetTrianglePoints()
    {
        EnsureTriangleVertices();
        return GetStoredTrianglePoints();
    }

    private Vector2[] GetStoredTrianglePoints()
    {
        return new[] { triangleVertexA, triangleVertexB, triangleVertexC };
    }

    private void SetStoredTrianglePoints(Vector2[] points)
    {
        triangleVertexA = points[0];
        triangleVertexB = points[1];
        triangleVertexC = points[2];
    }

    private void EnsureTriangleVertices()
    {
        if (!IsTriangleShape)
        {
            return;
        }

        Vector2[] points = GetStoredTrianglePoints();
        if (!HasUsableTriangle(points))
        {
            points = GetTrianglePresetPoints(Size, Shape);
            SetStoredTrianglePoints(points);
        }
        else if (IsRightTriangleShape)
        {
            points = GetRightTrianglePointsForBounds(CalculateLocalBounds(points), Shape);
            SetStoredTrianglePoints(points);
        }

        UpdateSizeFromTrianglePoints(points);
    }

    private void ResizeTriangleToSize(Vector2 targetSize)
    {
        Vector2[] points = GetStoredTrianglePoints();
        if (!HasUsableTriangle(points))
        {
            SetStoredTrianglePoints(GetTrianglePresetPoints(targetSize, Shape));
            return;
        }

        if (IsRightTriangleShape)
        {
            Bounds currentBounds = CalculateLocalBounds(points);
            Bounds targetBounds = new Bounds(
                currentBounds.center,
                new Vector3(targetSize.x, targetSize.y, 0f));
            SetStoredTrianglePoints(GetRightTrianglePointsForBounds(targetBounds, Shape));
            return;
        }

        Bounds bounds = CalculateLocalBounds(points);
        Vector2 currentSize = new Vector2(bounds.size.x, bounds.size.y);
        if (currentSize.x <= SizeTolerance || currentSize.y <= SizeTolerance)
        {
            SetStoredTrianglePoints(GetTrianglePresetPoints(targetSize, Shape));
            return;
        }

        Vector2 center = bounds.center;
        float scaleX = targetSize.x / currentSize.x;
        float scaleY = targetSize.y / currentSize.y;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 delta = points[i] - center;
            points[i] = new Vector2(center.x + delta.x * scaleX, center.y + delta.y * scaleY);
        }

        SetStoredTrianglePoints(points);
    }

    private bool SetRightTriangleVertex(int index, Vector2 localPosition)
    {
        Vector2[] points = GetTrianglePoints();
        if (!TryGetRightTriangleAxes(Shape, out Vector2 horizontalAxis, out Vector2 verticalAxis))
        {
            return false;
        }

        Vector2 rightAngleCorner = points[0];
        float legWidth = Mathf.Max(MinimumSize, Vector2.Dot(points[1] - rightAngleCorner, horizontalAxis));
        float legHeight = Mathf.Max(MinimumSize, Vector2.Dot(points[2] - rightAngleCorner, verticalAxis));

        switch (index)
        {
            case 0:
                rightAngleCorner = localPosition;
                break;
            case 1:
                legWidth = Mathf.Max(MinimumSize, Vector2.Dot(localPosition - rightAngleCorner, horizontalAxis));
                break;
            case 2:
                legHeight = Mathf.Max(MinimumSize, Vector2.Dot(localPosition - rightAngleCorner, verticalAxis));
                break;
            default:
                return false;
        }

        Vector2[] nextPoints = GetRightTrianglePoints(rightAngleCorner, legWidth, legHeight, Shape);
        if (!HasUsableTriangle(nextPoints))
        {
            return false;
        }

        SetStoredTrianglePoints(nextPoints);
        UpdateSizeFromTrianglePoints(nextPoints);
        ApplySize();
        return true;
    }

    private void UpdateSizeFromTrianglePoints(Vector2[] points)
    {
        Bounds bounds = CalculateLocalBounds(points);
        width = Mathf.Max(MinimumSize, bounds.size.x);
        height = Mathf.Max(MinimumSize, bounds.size.y);
    }

    private bool TryGetRectangleTopLandingSegment(
        float playerHalfWidth,
        out float minStandX,
        out float maxStandX,
        out float centerLandingY)
    {
        float halfWidth = Width * 0.5f;
        float halfHeight = Height * 0.5f;
        Vector2[] localPoints =
        {
            new Vector2(-halfWidth, -halfHeight),
            new Vector2(halfWidth, -halfHeight),
            new Vector2(halfWidth, halfHeight),
            new Vector2(-halfWidth, halfHeight)
        };

        return TryGetHorizontalTopLandingSegment(
            localPoints,
            playerHalfWidth,
            out minStandX,
            out maxStandX,
            out centerLandingY);
    }

    private bool TryGetTriangleTopLandingSegment(
        float playerHalfWidth,
        out float minStandX,
        out float maxStandX,
        out float centerLandingY)
    {
        return TryGetHorizontalTopLandingSegment(
            GetTrianglePoints(),
            playerHalfWidth,
            out minStandX,
            out maxStandX,
            out centerLandingY);
    }

    private bool TryGetHorizontalTopLandingSegment(
        Vector2[] localPoints,
        float playerHalfWidth,
        out float minStandX,
        out float maxStandX,
        out float centerLandingY)
    {
        Vector2[] worldPoints = new Vector2[localPoints.Length];
        for (int i = 0; i < localPoints.Length; i++)
        {
            worldPoints[i] = transform.TransformPoint(localPoints[i]);
        }

        float maxY = Mathf.Max(worldPoints[0].y, Mathf.Max(worldPoints[1].y, worldPoints[2].y));
        for (int i = 3; i < worldPoints.Length; i++)
        {
            maxY = Mathf.Max(maxY, worldPoints[i].y);
        }

        int firstTopIndex = -1;
        int secondTopIndex = -1;
        float topTolerance = Mathf.Max(SizeTolerance, 0.001f);
        for (int i = 0; i < worldPoints.Length; i++)
        {
            if (Mathf.Abs(worldPoints[i].y - maxY) > topTolerance)
            {
                continue;
            }

            if (firstTopIndex < 0)
            {
                firstTopIndex = i;
            }
            else
            {
                secondTopIndex = i;
                break;
            }
        }

        if (firstTopIndex < 0 || secondTopIndex < 0)
        {
            Bounds bounds = WorldBounds;
            centerLandingY = bounds.max.y;
            minStandX = bounds.min.x + playerHalfWidth;
            maxStandX = bounds.max.x - playerHalfWidth;
            return false;
        }

        float left = Mathf.Min(worldPoints[firstTopIndex].x, worldPoints[secondTopIndex].x);
        float right = Mathf.Max(worldPoints[firstTopIndex].x, worldPoints[secondTopIndex].x);
        centerLandingY = maxY;
        minStandX = left + playerHalfWidth;
        maxStandX = right - playerHalfWidth;
        return minStandX <= maxStandX;
    }

    private static bool HasUsableTriangle(Vector2[] points)
    {
        if (points == null || points.Length != 3)
        {
            return false;
        }

        for (int i = 0; i < points.Length; i++)
        {
            if (!IsFinite(points[i]))
            {
                return false;
            }
        }

        Bounds bounds = CalculateLocalBounds(points);
        if (bounds.size.x < MinimumSize || bounds.size.y < MinimumSize)
        {
            return false;
        }

        return CalculateTriangleArea(points) >= MinimumTriangleArea;
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsNaN(value.y)
            && !float.IsInfinity(value.x)
            && !float.IsInfinity(value.y);
    }

    private static float CalculateTriangleArea(Vector2[] points)
    {
        Vector2 ab = points[1] - points[0];
        Vector2 ac = points[2] - points[0];
        return Mathf.Abs(ab.x * ac.y - ab.y * ac.x) * 0.5f;
    }

    private static Bounds CalculateLocalBounds(Vector2[] points)
    {
        Bounds bounds = new Bounds(points[0], Vector3.zero);
        bounds.Encapsulate(points[1]);
        bounds.Encapsulate(points[2]);
        return bounds;
    }

    private static Vector2[] GetTrianglePresetPoints(Vector2 size, PlatformShape2D platformShape)
    {
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;

        switch (platformShape)
        {
            case PlatformShape2D.RightTriangleBottomLeft:
            case PlatformShape2D.RightTriangleBottomRight:
            case PlatformShape2D.RightTriangleTopRight:
            case PlatformShape2D.RightTriangleTopLeft:
                return GetRightTrianglePointsForBounds(
                    new Bounds(Vector3.zero, new Vector3(size.x, size.y, 0f)),
                    platformShape);
            case PlatformShape2D.TriangleRight:
                return new[]
                {
                    new Vector2(-halfWidth, -halfHeight),
                    new Vector2(halfWidth, 0f),
                    new Vector2(-halfWidth, halfHeight)
                };
            case PlatformShape2D.TriangleDown:
                return new[]
                {
                    new Vector2(-halfWidth, halfHeight),
                    new Vector2(0f, -halfHeight),
                    new Vector2(halfWidth, halfHeight)
                };
            case PlatformShape2D.TriangleLeft:
                return new[]
                {
                    new Vector2(halfWidth, halfHeight),
                    new Vector2(-halfWidth, 0f),
                    new Vector2(halfWidth, -halfHeight)
                };
            case PlatformShape2D.TriangleUp:
            default:
                return new[]
                {
                    new Vector2(-halfWidth, -halfHeight),
                    new Vector2(halfWidth, -halfHeight),
                    new Vector2(0f, halfHeight)
                };
        }
    }

    private static Vector2[] GetRightTrianglePoints(Vector2 rightAngleCorner, float legWidth, float legHeight, PlatformShape2D platformShape)
    {
        if (!TryGetRightTriangleAxes(platformShape, out Vector2 horizontalAxis, out Vector2 verticalAxis))
        {
            horizontalAxis = Vector2.right;
            verticalAxis = Vector2.up;
        }

        return new[]
        {
            rightAngleCorner,
            rightAngleCorner + horizontalAxis * Mathf.Max(MinimumSize, legWidth),
            rightAngleCorner + verticalAxis * Mathf.Max(MinimumSize, legHeight)
        };
    }

    private static Vector2[] GetRightTrianglePointsForBounds(Bounds bounds, PlatformShape2D platformShape)
    {
        Vector2 min = bounds.min;
        Vector2 max = bounds.max;

        switch (platformShape)
        {
            case PlatformShape2D.RightTriangleBottomRight:
                return new[]
                {
                    new Vector2(max.x, min.y),
                    new Vector2(min.x, min.y),
                    new Vector2(max.x, max.y)
                };
            case PlatformShape2D.RightTriangleTopRight:
                return new[]
                {
                    new Vector2(max.x, max.y),
                    new Vector2(min.x, max.y),
                    new Vector2(max.x, min.y)
                };
            case PlatformShape2D.RightTriangleTopLeft:
                return new[]
                {
                    new Vector2(min.x, max.y),
                    new Vector2(max.x, max.y),
                    new Vector2(min.x, min.y)
                };
            case PlatformShape2D.RightTriangleBottomLeft:
            default:
                return new[]
                {
                    new Vector2(min.x, min.y),
                    new Vector2(max.x, min.y),
                    new Vector2(min.x, max.y)
                };
        }
    }

    private static bool TryGetRightTriangleAxes(
        PlatformShape2D platformShape,
        out Vector2 horizontalAxis,
        out Vector2 verticalAxis)
    {
        switch (platformShape)
        {
            case PlatformShape2D.RightTriangleBottomLeft:
                horizontalAxis = Vector2.right;
                verticalAxis = Vector2.up;
                return true;
            case PlatformShape2D.RightTriangleBottomRight:
                horizontalAxis = Vector2.left;
                verticalAxis = Vector2.up;
                return true;
            case PlatformShape2D.RightTriangleTopRight:
                horizontalAxis = Vector2.left;
                verticalAxis = Vector2.down;
                return true;
            case PlatformShape2D.RightTriangleTopLeft:
                horizontalAxis = Vector2.right;
                verticalAxis = Vector2.down;
                return true;
            default:
                horizontalAxis = Vector2.right;
                verticalAxis = Vector2.up;
                return false;
        }
    }

    private static Sprite GetRightTriangleSprite(PlatformShape2D platformShape)
    {
        switch (platformShape)
        {
            case PlatformShape2D.RightTriangleBottomRight:
                if (rightTriangleBottomRightSprite == null)
                {
                    rightTriangleBottomRightSprite = CreateRightTriangleSprite(platformShape);
                }

                return rightTriangleBottomRightSprite;
            case PlatformShape2D.RightTriangleTopRight:
                if (rightTriangleTopRightSprite == null)
                {
                    rightTriangleTopRightSprite = CreateRightTriangleSprite(platformShape);
                }

                return rightTriangleTopRightSprite;
            case PlatformShape2D.RightTriangleTopLeft:
                if (rightTriangleTopLeftSprite == null)
                {
                    rightTriangleTopLeftSprite = CreateRightTriangleSprite(platformShape);
                }

                return rightTriangleTopLeftSprite;
            case PlatformShape2D.RightTriangleBottomLeft:
            default:
                if (rightTriangleBottomLeftSprite == null)
                {
                    rightTriangleBottomLeftSprite = CreateRightTriangleSprite(PlatformShape2D.RightTriangleBottomLeft);
                }

                return rightTriangleBottomLeftSprite;
        }
    }

    private static Sprite CreateRightTriangleSprite(PlatformShape2D platformShape)
    {
        Texture2D texture = new Texture2D(
            RightTriangleSpritePixels,
            RightTriangleSpritePixels,
            TextureFormat.RGBA32,
            true)
        {
            name = $"{RightTriangleSpriteNamePrefix} Texture",
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.anisoLevel = 1;

        Color32[] pixels = new Color32[RightTriangleSpritePixels * RightTriangleSpritePixels];
        Color32 transparent = new Color32(255, 255, 255, 0);
        for (int y = 0; y < RightTriangleSpritePixels; y++)
        {
            for (int x = 0; x < RightTriangleSpritePixels; x++)
            {
                byte alpha = GetRightTrianglePixelAlpha(x, y, platformShape);
                pixels[y * RightTriangleSpritePixels + x] = alpha > 0
                    ? new Color32(255, 255, 255, alpha)
                    : transparent;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(true, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, RightTriangleSpritePixels, RightTriangleSpritePixels),
            new Vector2(0.5f, 0.5f),
            RightTriangleSpritePixels,
            0,
            SpriteMeshType.FullRect);
        sprite.name = $"{RightTriangleSpriteNamePrefix} {platformShape}";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static byte GetRightTrianglePixelAlpha(int x, int y, PlatformShape2D platformShape)
    {
        int filledSamples = 0;
        int sampleCount = RightTriangleSpriteSupersampling * RightTriangleSpriteSupersampling;
        float sampleStep = 1f / RightTriangleSpriteSupersampling;
        for (int sampleY = 0; sampleY < RightTriangleSpriteSupersampling; sampleY++)
        {
            for (int sampleX = 0; sampleX < RightTriangleSpriteSupersampling; sampleX++)
            {
                float normalizedX = (x + (sampleX + 0.5f) * sampleStep) / RightTriangleSpritePixels;
                float normalizedY = (y + (sampleY + 0.5f) * sampleStep) / RightTriangleSpritePixels;
                if (IsRightTriangleSampleFilled(normalizedX, normalizedY, platformShape))
                {
                    filledSamples++;
                }
            }
        }

        return (byte)Mathf.RoundToInt(255f * filledSamples / sampleCount);
    }

    private static bool IsRightTriangleSampleFilled(float normalizedX, float normalizedY, PlatformShape2D platformShape)
    {
        switch (platformShape)
        {
            case PlatformShape2D.RightTriangleBottomRight:
                return normalizedY <= normalizedX;
            case PlatformShape2D.RightTriangleTopRight:
                return normalizedX + normalizedY >= 1f;
            case PlatformShape2D.RightTriangleTopLeft:
                return normalizedX <= normalizedY;
            case PlatformShape2D.RightTriangleBottomLeft:
            default:
                return normalizedX + normalizedY <= 1f;
        }
    }

    private static bool IsGeneratedRightTriangleSprite(Sprite sprite)
    {
        return sprite != null && sprite.name.StartsWith(RightTriangleSpriteNamePrefix);
    }

    private static Vector2[] GetTriangleUvs(Vector2[] points)
    {
        Bounds bounds = CalculateLocalBounds(points);
        Vector2 min = bounds.min;
        Vector2 size = new Vector2(
            Mathf.Max(SizeTolerance, bounds.size.x),
            Mathf.Max(SizeTolerance, bounds.size.y));

        Vector2[] uvs = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            uvs[i] = new Vector2(
                (points[i].x - min.x) / size.x,
                (points[i].y - min.y) / size.y);
        }

        return uvs;
    }

    private static PlatformShape2D NormalizeShape(PlatformShape2D platformShape)
    {
        switch (platformShape)
        {
            case PlatformShape2D.Rectangle:
            case PlatformShape2D.TriangleUp:
            case PlatformShape2D.TriangleRight:
            case PlatformShape2D.TriangleDown:
            case PlatformShape2D.TriangleLeft:
            case PlatformShape2D.RightTriangleBottomLeft:
            case PlatformShape2D.RightTriangleBottomRight:
            case PlatformShape2D.RightTriangleTopRight:
            case PlatformShape2D.RightTriangleTopLeft:
                return platformShape;
            default:
                return PlatformShape2D.Rectangle;
        }
    }

    private static bool IsRightTriangleShapeValue(PlatformShape2D platformShape)
    {
        switch (platformShape)
        {
            case PlatformShape2D.RightTriangleBottomLeft:
            case PlatformShape2D.RightTriangleBottomRight:
            case PlatformShape2D.RightTriangleTopRight:
            case PlatformShape2D.RightTriangleTopLeft:
                return true;
            default:
                return false;
        }
    }
}
