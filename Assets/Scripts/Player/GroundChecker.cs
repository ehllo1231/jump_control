using UnityEngine;

/// <summary>
/// 플레이어 발 전체 폭 아래에 겹치는 지면 콜라이더가 있는지 검사합니다.
/// 자신의 콜라이더는 결과에서 제외하므로 별도 레이어를 만들지 않아도 바로 테스트할 수 있습니다.
/// </summary>
public class GroundChecker : MonoBehaviour
{
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float checkRadius = 0.08f;
    [SerializeField] private float horizontalInset = 0f;

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

        GetGroundCheckArea(out Vector2 checkPosition, out Vector2 checkSize, out Bounds ownBounds, out bool hasOwnBounds);
        int hitCount = Physics2D.OverlapBox(checkPosition, checkSize, 0f, groundFilter, overlapResults);

        isGrounded = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = overlapResults[i];
            if (hit != null && !IsOwnCollider(hit) && IsSurfaceAtFeet(hit, ownBounds, hasOwnBounds))
            {
                isGrounded = true;
                break;
            }
        }

        return isGrounded;
    }

    private void GetGroundCheckArea(
        out Vector2 checkPosition,
        out Vector2 checkSize,
        out Bounds ownBounds,
        out bool hasOwnBounds)
    {
        hasOwnBounds = TryGetOwnColliderBounds(out ownBounds);

        if (!hasOwnBounds)
        {
            checkPosition = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
            checkSize = Vector2.one * (checkRadius * 2f);
            return;
        }

        float checkWidth = Mathf.Max(checkRadius * 2f, ownBounds.size.x - horizontalInset * 2f);
        float checkY = ownBounds.min.y - checkRadius * 0.25f;

        checkPosition = new Vector2(ownBounds.center.x, checkY);
        checkSize = new Vector2(checkWidth, checkRadius * 2f);
    }

    private bool TryGetOwnColliderBounds(out Bounds combinedBounds)
    {
        EnsureOwnColliders();

        combinedBounds = default;
        bool foundCollider = false;

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider2D ownCollider = ownColliders[i];
            if (ownCollider == null || !ownCollider.enabled || ownCollider.isTrigger)
            {
                continue;
            }

            if (!foundCollider)
            {
                combinedBounds = ownCollider.bounds;
                foundCollider = true;
            }
            else
            {
                combinedBounds.Encapsulate(ownCollider.bounds);
            }
        }

        return foundCollider;
    }

    private bool IsSurfaceAtFeet(Collider2D candidate, Bounds ownBounds, bool hasOwnBounds)
    {
        if (!hasOwnBounds)
        {
            return true;
        }

        float feetY = ownBounds.min.y;
        float surfaceY = candidate.bounds.max.y;
        float allowedAboveFeet = checkRadius * 1.5f;
        float allowedBelowFeet = checkRadius * 2f;

        return surfaceY >= feetY - allowedBelowFeet && surfaceY <= feetY + allowedAboveFeet;
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

        EnsureOwnColliders();

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (candidate == ownColliders[i])
            {
                return true;
            }
        }

        return candidate.transform.IsChildOf(transform);
    }

    private void EnsureOwnColliders()
    {
        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider2D>();
        }
    }

    private void OnValidate()
    {
        checkRadius = Mathf.Max(0.01f, checkRadius);
        horizontalInset = Mathf.Max(0f, horizontalInset);
    }

    private void OnDrawGizmosSelected()
    {
        GetGroundCheckArea(out Vector2 checkPosition, out Vector2 checkSize, out _, out _);
        Gizmos.color = isGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireCube(checkPosition, checkSize);
    }
}
