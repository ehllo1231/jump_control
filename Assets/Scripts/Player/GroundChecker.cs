using UnityEngine;

/// <summary>
/// 플레이어 발 위치 주변에 겹치는 지면 콜라이더가 있는지 검사합니다.
/// 자신의 콜라이더는 결과에서 제외하므로 별도 레이어를 만들지 않아도 바로 테스트할 수 있습니다.
/// </summary>
public class GroundChecker : MonoBehaviour
{
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float checkRadius = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool isGrounded;

    private readonly Collider2D[] overlapResults = new Collider2D[12];
    private Collider2D[] ownColliders;
    private ContactFilter2D groundFilter;

    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        ownColliders = GetComponentsInChildren<Collider2D>();
        ConfigureFilter();
    }

    private void Update()
    {
        CheckGroundedNow();
    }

    public bool CheckGroundedNow()
    {
        ConfigureFilter();

        Vector2 checkPosition = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
        int hitCount = Physics2D.OverlapCircle(checkPosition, checkRadius, groundFilter, overlapResults);

        isGrounded = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = overlapResults[i];
            if (hit != null && !IsOwnCollider(hit))
            {
                isGrounded = true;
                break;
            }
        }

        return isGrounded;
    }

    private void ConfigureFilter()
    {
        groundFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        groundFilter.SetLayerMask(groundLayers);
    }

    private bool IsOwnCollider(Collider2D candidate)
    {
        if (candidate == null)
        {
            return true;
        }

        if (ownColliders == null)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (candidate == ownColliders[i])
            {
                return true;
            }
        }

        return candidate.transform.IsChildOf(transform);
    }

    private void OnValidate()
    {
        checkRadius = Mathf.Max(0.01f, checkRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 checkPosition = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
        Gizmos.color = isGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(checkPosition, checkRadius);
    }
}
