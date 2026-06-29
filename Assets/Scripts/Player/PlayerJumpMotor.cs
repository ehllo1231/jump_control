using UnityEngine;

/// <summary>
/// Rigidbody2D에 실제 점프 충격량을 적용하는 물리 담당 컴포넌트입니다.
/// 점프 입력 순서나 UI 상태는 알지 못하도록 분리했습니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerJumpMotor : MonoBehaviour
{
    private const float DefaultWallBounceElasticity = 0.18f;
    private const float MaximumWallBounceElasticity = 2f;
    private const float DefaultMinimumWallBounceExitSpeed = 1.25f;
    private const float DefaultWallBounceCooldown = 0.08f;
    private const float DefaultWallBounceSeparationDistance = 0.015f;
    private const WallBounceVerticalVelocityMode DefaultWallBounceVerticalVelocityMode =
        WallBounceVerticalVelocityMode.PreservePreCollisionVelocity;

    [Header("Rigidbody")]
    [SerializeField] private Rigidbody2D body;

    [Header("Jump Force")]
    [SerializeField] private float impulseMultiplier = 1f;
    [SerializeField] private bool clearVelocityBeforeJump = true;
    [SerializeField] private bool keepHorizontalVelocityOnJump;

    [Header("Wall Bounce")]
    [SerializeField, Range(0f, MaximumWallBounceElasticity)] private float wallBounceElasticity = DefaultWallBounceElasticity;
    [SerializeField] private WallBounceVerticalVelocityMode wallBounceVerticalVelocityMode =
        DefaultWallBounceVerticalVelocityMode;
    [SerializeField, Range(0f, 1f)] private float minimumWallNormalX = 0.55f;
    [SerializeField, Min(0f)] private float minimumWallBounceExitSpeed = DefaultMinimumWallBounceExitSpeed;
    [SerializeField, Min(0f)] private float wallBounceCooldown = DefaultWallBounceCooldown;
    [SerializeField, Min(0f)] private float wallBounceSeparationDistance = DefaultWallBounceSeparationDistance;

    [Header("Debug")]
    [SerializeField] private Vector2 lastAppliedImpulse;
    [SerializeField] private Vector2 lastWallBounceVelocity;
    [SerializeField] private Vector2 velocityBeforePhysicsStep;

    private float lastWallBounceTime = float.NegativeInfinity;

    public Vector2 LastAppliedImpulse => lastAppliedImpulse;
    public Vector2 LastWallBounceVelocity => lastWallBounceVelocity;
    public float VerticalVelocity => body != null ? body.linearVelocity.y : 0f;
    public float ImpulseMultiplier => Mathf.Max(0f, impulseMultiplier);
    public bool ClearVelocityBeforeJump => clearVelocityBeforeJump;
    public bool KeepHorizontalVelocityOnJump => keepHorizontalVelocityOnJump;
    public float WallBounceElasticity => Mathf.Clamp(wallBounceElasticity, 0f, MaximumWallBounceElasticity);
    public WallBounceVerticalVelocityMode WallBounceVerticalVelocityMode =>
        NormalizeWallBounceVerticalVelocityMode(wallBounceVerticalVelocityMode);
    public float MinimumWallNormalX => Mathf.Clamp01(minimumWallNormalX);
    public float MinimumWallBounceExitSpeed => Mathf.Max(0f, minimumWallBounceExitSpeed);
    public float WallBounceCooldown => Mathf.Max(0f, wallBounceCooldown);
    public float WallBounceSeparationDistance => Mathf.Max(0f, wallBounceSeparationDistance);

    private void Awake()
    {
        CacheBodyReference();
        CacheVelocityBeforePhysicsStep();
    }

    private void FixedUpdate()
    {
        CacheVelocityBeforePhysicsStep();
    }

    public Vector2 Jump(float power, Vector2 direction)
    {
        CacheBodyReference();

        Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;

        if (clearVelocityBeforeJump)
        {
            float xVelocity = keepHorizontalVelocityOnJump ? body.linearVelocity.x : 0f;
            body.linearVelocity = new Vector2(xVelocity, 0f);
        }

        lastAppliedImpulse = normalizedDirection * power * impulseMultiplier;
        body.AddForce(lastAppliedImpulse, ForceMode2D.Impulse);

        return lastAppliedImpulse;
    }

    public void SetWallBounceElasticity(float elasticity)
    {
        wallBounceElasticity = Mathf.Clamp(elasticity, 0f, MaximumWallBounceElasticity);
    }

    public void SetWallBounceVerticalVelocityMode(WallBounceVerticalVelocityMode mode)
    {
        wallBounceVerticalVelocityMode = NormalizeWallBounceVerticalVelocityMode(mode);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ApplyWallBounce(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        ApplyWallBounce(collision);
    }

    private void ApplyWallBounce(Collision2D collision)
    {
        float elasticity = WallBounceElasticity;
        if (elasticity <= 0f || collision == null || collision.contactCount == 0)
        {
            return;
        }

        if (Time.time - lastWallBounceTime < WallBounceCooldown)
        {
            return;
        }

        CacheBodyReference();

        if (!TryGetWallNormal(collision, out Vector2 wallNormal))
        {
            return;
        }

        ApplyWallBounceVelocity(collision, wallNormal, elasticity);
    }

    private bool TryGetWallNormal(Collision2D collision, out Vector2 wallNormal)
    {
        wallNormal = Vector2.zero;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.x) >= minimumWallNormalX && Mathf.Abs(normal.x) > Mathf.Abs(wallNormal.x))
            {
                wallNormal = normal;
            }
        }

        return wallNormal != Vector2.zero;
    }

    private void ApplyWallBounceVelocity(Collision2D collision, Vector2 wallNormal, float elasticity)
    {
        float wallDirection = Mathf.Sign(wallNormal.x);
        Vector2 currentVelocity = body.linearVelocity;
        float relativeImpactSpeed = Mathf.Abs(collision.relativeVelocity.x);
        float inwardSpeed = Mathf.Max(0f, -currentVelocity.x * wallDirection);
        float impactSpeed = Mathf.Max(relativeImpactSpeed, inwardSpeed);
        float exitSpeedFloor = MinimumWallBounceExitSpeed * Mathf.Clamp01(elasticity);
        float bounceSpeed = Mathf.Max(impactSpeed * elasticity, exitSpeedFloor);
        float currentOutwardSpeed = currentVelocity.x * wallDirection;

        if (currentOutwardSpeed >= bounceSpeed)
        {
            return;
        }

        float bounceX = wallDirection * bounceSpeed;
        body.linearVelocity = new Vector2(bounceX, GetWallBounceVerticalVelocity(currentVelocity.y));
        SeparateFromWall(wallNormal);
        lastWallBounceVelocity = body.linearVelocity;
        lastWallBounceTime = Time.time;
    }

    private float GetWallBounceVerticalVelocity(float currentVelocityY)
    {
        switch (WallBounceVerticalVelocityMode)
        {
            case WallBounceVerticalVelocityMode.PreservePreCollisionVelocity:
                return velocityBeforePhysicsStep.y;
            case WallBounceVerticalVelocityMode.UseCurrentCollisionVelocity:
                return currentVelocityY;
            default:
                return velocityBeforePhysicsStep.y;
        }
    }

    private void CacheVelocityBeforePhysicsStep()
    {
        CacheBodyReference();

        if (body != null)
        {
            velocityBeforePhysicsStep = body.linearVelocity;
        }
    }

    private void SeparateFromWall(Vector2 wallNormal)
    {
        float separationDistance = WallBounceSeparationDistance;
        if (separationDistance <= 0f || wallNormal.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        body.position += wallNormal.normalized * separationDistance;
    }

    private void OnValidate()
    {
        impulseMultiplier = Mathf.Max(0f, impulseMultiplier);
        wallBounceElasticity = Mathf.Clamp(wallBounceElasticity, 0f, MaximumWallBounceElasticity);
        wallBounceVerticalVelocityMode = NormalizeWallBounceVerticalVelocityMode(wallBounceVerticalVelocityMode);
        minimumWallNormalX = Mathf.Clamp01(minimumWallNormalX);
        minimumWallBounceExitSpeed = Mathf.Max(0f, minimumWallBounceExitSpeed);
        wallBounceCooldown = Mathf.Max(0f, wallBounceCooldown);
        wallBounceSeparationDistance = Mathf.Max(0f, wallBounceSeparationDistance);

        CacheBodyReference();
    }

    private void CacheBodyReference()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private static WallBounceVerticalVelocityMode NormalizeWallBounceVerticalVelocityMode(
        WallBounceVerticalVelocityMode mode)
    {
        switch (mode)
        {
            case WallBounceVerticalVelocityMode.PreservePreCollisionVelocity:
            case WallBounceVerticalVelocityMode.UseCurrentCollisionVelocity:
                return mode;
            default:
                return DefaultWallBounceVerticalVelocityMode;
        }
    }
}
