using UnityEngine;

/// <summary>
/// 임시 플레이어 표시와 상태별 색상만 담당합니다.
/// 추후 캐릭터 스프라이트나 Animator를 붙여도 점프 로직은 PlayerController에 그대로 둘 수 있습니다.
/// </summary>
[ExecuteAlways]
public class PlayerVisual : MonoBehaviour
{
    private const float MinimumBodySize = 0.1f;
    private const float AlignmentTolerance = 0.000001f;

    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private BoxCollider2D bodyCollider;

    [Header("State Colors")]
    [SerializeField] private Color idleColor = new Color(0.2f, 0.65f, 1f);
    [SerializeField] private Color powerReadyColor = new Color(1f, 0.82f, 0.25f);
    [SerializeField] private Color aimingColor = new Color(0.15f, 0.9f, 0.55f);
    [SerializeField] private Color jumpingColor = new Color(1f, 0.42f, 0.3f);

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

    public void SetBodySize(float size)
    {
        CacheReferences();

        float safeSize = Mathf.Max(MinimumBodySize, size);
        if (bodyCollider != null)
        {
            bodyCollider.offset = Vector2.zero;
            bodyCollider.size = Vector2.one * safeSize;
        }

        if (visualRoot != null)
        {
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = new Vector3(safeSize, safeSize, 1f);
        }
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
        ResetVisualLocalTransform();
        SyncColliderToVisual();
        isSyncing = false;
    }

    private void ResetVisualLocalTransform()
    {
        if (visualRoot == null || visualRoot == transform)
        {
            return;
        }

        if (visualRoot.localPosition.sqrMagnitude > AlignmentTolerance)
        {
            visualRoot.localPosition = Vector3.zero;
        }

        if (visualRoot.localRotation != Quaternion.identity)
        {
            visualRoot.localRotation = Quaternion.identity;
        }
    }

    private void SyncColliderToVisual()
    {
        if (bodyCollider == null)
        {
            return;
        }

        bodyCollider.offset = Vector2.zero;

        if (visualRoot == null || visualRoot == transform)
        {
            return;
        }

        Vector3 localScale = visualRoot.localScale;
        Vector2 visualSize = new Vector2(
            Mathf.Max(MinimumBodySize, Mathf.Abs(localScale.x)),
            Mathf.Max(MinimumBodySize, Mathf.Abs(localScale.y)));

        if ((bodyCollider.size - visualSize).sqrMagnitude > AlignmentTolerance)
        {
            bodyCollider.size = visualSize;
        }
    }

    private void OnValidate()
    {
        CacheReferences();
        SyncVisualAndCollider();
    }
}
