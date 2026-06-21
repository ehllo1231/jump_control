using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 맵 제작용 Platform의 크기와 표면 정보를 관리합니다.
/// Width와 Height를 변경하면 표시와 충돌체 크기를 항상 함께 갱신합니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class Platform2D : MonoBehaviour, IPlatformSurface
{
    private const float MinimumSize = 0.1f;

    [Header("Platform Size")]
    [SerializeField, Min(MinimumSize)] private float width = 2.5f;
    [SerializeField, Min(MinimumSize)] private float height = 0.3f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private BoxCollider2D boxCollider;

#if UNITY_EDITOR
    [System.NonSerialized] private bool applySizeScheduled;
#endif

    public float Width => width;
    public float Height => height;
    public Vector2 Size => new Vector2(width, height);
    public Bounds WorldBounds => boxCollider != null ? boxCollider.bounds : new Bounds(transform.position, Size);

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

    public void SetSize(float newWidth, float newHeight)
    {
        width = Mathf.Max(MinimumSize, newWidth);
        height = Mathf.Max(MinimumSize, newHeight);
        ApplySize();
    }

    public void ApplySize()
    {
        CacheReferences();

        width = Mathf.Max(MinimumSize, width);
        height = Mathf.Max(MinimumSize, height);
        Vector2 platformSize = Size;

        if (spriteRenderer != null)
        {
            spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            spriteRenderer.size = platformSize;
        }

        if (boxCollider != null)
        {
            boxCollider.size = platformSize;
        }
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
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
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
}
