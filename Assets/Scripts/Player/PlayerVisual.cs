using UnityEngine;

/// <summary>
/// 플레이어 시각 요소와 상태별 표시를 담당합니다.
/// 캐릭터 스프라이트나 Animator를 붙여도 점프 로직은 PlayerController에 그대로 둘 수 있습니다.
/// </summary>
[ExecuteAlways]
public class PlayerVisual : MonoBehaviour
{
    private const float MinimumBodySize = 0.1f;
    private const float MinimumVisualScale = 0.1f;
    private const float AlignmentTolerance = 0.000001f;

    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private BoxCollider2D bodyCollider;

    [Header("State Colors")]
    [SerializeField] private bool tintByState = true;
    [SerializeField] private Color idleColor = new Color(0.2f, 0.65f, 1f);
    [SerializeField] private Color powerReadyColor = new Color(1f, 0.82f, 0.25f);
    [SerializeField] private Color aimingColor = new Color(0.15f, 0.9f, 0.55f);
    [SerializeField] private Color jumpingColor = new Color(1f, 0.42f, 0.3f);
    [SerializeField, HideInInspector] private float configuredBodySize = 0.72f;
    [SerializeField, HideInInspector] private float configuredVisualScale = 1f;

    private bool isSyncing;

    private void Awake()
    {
        CacheReferences();
        SyncVisualAndCollider();

        if (Application.isPlaying)
        {
            SetState(PlayerJumpState.Idle);
        }
    }

    private void LateUpdate()
    {
        SyncVisualAndCollider();
    }

    public void SetState(PlayerJumpState state)
    {
        if (bodyRenderer == null)
        {
            return;
        }

        if (!tintByState)
        {
            bodyRenderer.color = Color.white;
            return;
        }

        switch (state)
        {
            case PlayerJumpState.ChargingPower:
                bodyRenderer.color = powerReadyColor;
                break;
            case PlayerJumpState.PowerGaugeReady:
            case PlayerJumpState.Aiming:
                bodyRenderer.color = aimingColor;
                break;
            case PlayerJumpState.Jumping:
                bodyRenderer.color = jumpingColor;
                break;
            default:
                bodyRenderer.color = idleColor;
                break;
        }
    }

    public void OnJump(Vector2 direction)
    {
        if (bodyRenderer != null && Mathf.Abs(direction.x) > 0.01f)
        {
            bodyRenderer.flipX = direction.x < 0f;
        }
    }

    public void SetBodySize(float size, float visualScale = 1f)
    {
        CacheReferences();

        float safeSize = Mathf.Max(MinimumBodySize, size);
        float safeVisualScale = Mathf.Max(MinimumVisualScale, visualScale);
        configuredBodySize = safeSize;
        configuredVisualScale = safeVisualScale;
        if (bodyCollider != null)
        {
            bodyCollider.offset = Vector2.zero;
            bodyCollider.size = Vector2.one * safeSize;
        }

        ApplyVisualTransform(safeSize, safeVisualScale);
    }

    private void CacheReferences()
    {
        if (visualRoot == null)
        {
            Transform found = transform.Find("Visual");
            visualRoot = found != null ? found : transform;
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<BoxCollider2D>();
        }
    }

    private void SyncVisualAndCollider()
    {
        if (isSyncing)
        {
            return;
        }

        isSyncing = true;
        CacheReferences();
        SyncColliderOffset();
        ApplyVisualTransform(GetConfiguredBodySize(), GetConfiguredVisualScale());
        isSyncing = false;
    }

    private void ApplyVisualTransform(float bodySize, float visualScale)
    {
        if (visualRoot == null || visualRoot == transform)
        {
            return;
        }

        float safeBodySize = Mathf.Max(MinimumBodySize, bodySize);
        float targetVisualHeight = safeBodySize * Mathf.Max(MinimumVisualScale, visualScale);
        float spriteHeight = GetSpriteLocalHeight();
        float visualScaleFactor = targetVisualHeight / spriteHeight;
        float spriteBottom = GetSpriteLocalBottom() * visualScaleFactor;
        float bodyBottom = -safeBodySize * 0.5f;
        float visualOffsetY = bodyBottom - spriteBottom;
        Vector3 targetPosition = new Vector3(0f, visualOffsetY, 0f);
        Vector3 targetScale = new Vector3(visualScaleFactor, visualScaleFactor, 1f);

        if ((visualRoot.localPosition - targetPosition).sqrMagnitude > AlignmentTolerance)
        {
            visualRoot.localPosition = targetPosition;
        }

        if (visualRoot.localRotation != Quaternion.identity)
        {
            visualRoot.localRotation = Quaternion.identity;
        }

        if ((visualRoot.localScale - targetScale).sqrMagnitude > AlignmentTolerance)
        {
            visualRoot.localScale = targetScale;
        }
    }

    private void SyncColliderOffset()
    {
        if (bodyCollider == null)
        {
            return;
        }

        bodyCollider.offset = Vector2.zero;
    }

    private float GetConfiguredBodySize()
    {
        if (configuredBodySize >= MinimumBodySize)
        {
            return configuredBodySize;
        }

        if (bodyCollider == null)
        {
            return MinimumBodySize;
        }

        return Mathf.Max(MinimumBodySize, Mathf.Max(bodyCollider.size.x, bodyCollider.size.y));
    }

    private float GetConfiguredVisualScale()
    {
        return Mathf.Max(MinimumVisualScale, configuredVisualScale);
    }

    private float GetSpriteLocalHeight()
    {
        if (bodyRenderer == null || bodyRenderer.sprite == null)
        {
            return 1f;
        }

        return Mathf.Max(0.0001f, bodyRenderer.sprite.bounds.size.y);
    }

    private float GetSpriteLocalBottom()
    {
        if (bodyRenderer == null || bodyRenderer.sprite == null)
        {
            return -0.5f;
        }

        return bodyRenderer.sprite.bounds.min.y;
    }

    private void OnValidate()
    {
        CacheReferences();
        SyncVisualAndCollider();
    }
}
