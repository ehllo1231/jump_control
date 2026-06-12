using UnityEngine;

/// <summary>
/// Rigidbody2D에 실제 점프 충격량을 적용하는 물리 담당 컴포넌트입니다.
/// 점프 입력 순서나 UI 상태는 알지 못하도록 분리했습니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerJumpMotor : MonoBehaviour
{
    [Header("Rigidbody")]
    [SerializeField] private Rigidbody2D body;

    [Header("Jump Force")]
    [SerializeField] private float impulseMultiplier = 1f;
    [SerializeField] private bool clearVelocityBeforeJump = true;
    [SerializeField] private bool keepHorizontalVelocityOnJump;

    [Header("Debug")]
    [SerializeField] private Vector2 lastAppliedImpulse;

    public Vector2 LastAppliedImpulse => lastAppliedImpulse;
    public float VerticalVelocity => body != null ? body.linearVelocity.y : 0f;

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    public Vector2 Jump(float power, Vector2 direction)
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

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

    private void OnValidate()
    {
        impulseMultiplier = Mathf.Max(0f, impulseMultiplier);

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }
}
