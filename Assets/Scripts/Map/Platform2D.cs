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
    TriangleLeft = 4
}

/// <summary>
/// 맵 제작용 Platform의 도형, 크기와 표면 정보를 관리합니다.
/// Width/Height와 삼각형 꼭지점을 변경하면 표시와 충돌체를 항상 같은 좌표로 갱신합니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class Platform2D : MonoBehaviour, IPlatformSurface
{
    private const float MinimumSize = 0.1f;
    private const float SizeTolerance = 0.0001f;
    private const float MinimumTriangleArea = 0.0005f;
    private const string TriangleVisualName = "Triangle Visual";
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int RendererColorPropertyId = Shader.PropertyToID("_RendererColor");
    private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");

    [Header("Platform Size")]
    [SerializeField] private PlatformShape2D shape = PlatformShape2D.Rectangle;
    [SerializeField, Min(MinimumSize)] private float width = 2.5f;
    [SerializeField, Min(MinimumSize)] private float height = 0.3f;

    [Header("Triangle Vertices")]
    [SerializeField] private Vector2 triangleVertexA = new Vector2(-1.25f, -0.15f);
    [SerializeField] private Vector2 triangleVertexB = new Vector2(1.25f, -0.15f);
    [SerializeField] private Vector2 triangleVertexC = new Vector2(0f, 0.15f);

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private BoxCollider2D boxCollider;
    [SerializeField] private PolygonCollider2D polygonCollider;
    [SerializeField] private MeshFilter triangleMeshFilter;
    [SerializeField] private MeshRenderer triangleMeshRenderer;

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

    private void LateUpdate()
    {
        SyncColliderToVisibleSize();
    }

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
            ApplyTriangleShape();
            return;
        }

        Vector2 visibleSize = Size;
        if (spriteRenderer != null)
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
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

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

    private void ApplyRectangleShape()
    {
        Vector2 platformSize = Size;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.size = platformSize;
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
        EnsureTriangleReferences();

        Vector2[] points = GetTrianglePoints();
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

    private void EnsureTriangleReferences()
    {
        if (polygonCollider == null)
        {
            polygonCollider = GetComponent<PolygonCollider2D>();
            if (polygonCollider == null)
            {
                polygonCollider = gameObject.AddComponent<PolygonCollider2D>();
            }
        }

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
            ResetTriangleVisualTransform(triangleVisual);
            return triangleVisual;
        }

        GameObject visualObject = new GameObject(TriangleVisualName);
        visualObject.layer = gameObject.layer;
        triangleVisual = visualObject.transform;
        triangleVisual.SetParent(transform, false);
        ResetTriangleVisualTransform(triangleVisual);
#if UNITY_EDITOR
        MarkTriangleVisualDirty(visualObject);
#endif
        return triangleVisual;
    }

    private Transform FindTriangleVisual()
    {
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
                return platformShape;
            default:
                return PlatformShape2D.Rectangle;
        }
    }
}
