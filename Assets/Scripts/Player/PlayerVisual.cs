using UnityEngine;

/// <summary>
/// 임시 플레이어 표시와 상태별 색상만 담당합니다.
/// 추후 캐릭터 스프라이트나 Animator를 붙여도 점프 로직은 PlayerController에 그대로 둘 수 있습니다.
/// </summary>
public class PlayerVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Header("State Colors")]
    [SerializeField] private Color idleColor = new Color(0.2f, 0.65f, 1f);
    [SerializeField] private Color powerReadyColor = new Color(1f, 0.82f, 0.25f);
    [SerializeField] private Color aimingColor = new Color(0.15f, 0.9f, 0.55f);
    [SerializeField] private Color jumpingColor = new Color(1f, 0.42f, 0.3f);

    private void Awake()
    {
        CacheReferences();
        SetState(PlayerJumpState.Idle);
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
        if (visualRoot != null)
        {
            float safeSize = Mathf.Max(0.1f, size);
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
    }

    private void OnValidate()
    {
        CacheReferences();
    }
}
