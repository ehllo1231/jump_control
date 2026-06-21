using UnityEngine;

public enum PlayerJumpState
{
    Idle,
    PowerGaugeReady,
    Aiming,
    Jumping,
    ChargingPower
}

/// <summary>
/// 점프 입력 흐름과 상태 전환만 담당합니다.
/// 실제 힘 적용, 입력 읽기, 게이지 표시, 각도 표시, 비주얼은 별도 컴포넌트에 위임합니다.
/// </summary>
[RequireComponent(typeof(JumpInputReader))]
[RequireComponent(typeof(PlayerJumpMotor))]
[RequireComponent(typeof(GroundChecker))]
[RequireComponent(typeof(JumpPowerGauge))]
[RequireComponent(typeof(JumpAngleAim))]
[RequireComponent(typeof(PlayerVisual))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private JumpInputReader inputReader;
    [SerializeField] private JumpPowerGauge powerGauge;
    [SerializeField] private JumpAngleAim angleAim;
    [SerializeField] private PlayerJumpMotor jumpMotor;
    [SerializeField] private GroundChecker groundChecker;
    [SerializeField] private PlayerVisual playerVisual;
    [SerializeField] private BoxCollider2D bodyCollider;

    [Header("Jump Tuning")]
    [SerializeField] private JumpTuningConfig jumpTuning = new JumpTuningConfig();

    [Header("Landing")]
    [SerializeField] private float minimumJumpingTime = 0.08f;
    [SerializeField] private float landingVerticalSpeedThreshold = 0.35f;

    [Header("Debug")]
    [SerializeField] private PlayerJumpState currentState = PlayerJumpState.Idle;
    [SerializeField] private float lockedPower;
    [SerializeField] private float lockedJumpAngle;
    [SerializeField] private Vector2 lockedJumpDirection = Vector2.up;
    [SerializeField] private float lastJumpAngle;
    [SerializeField] private Vector2 lastJumpVector;

    private float jumpStartedAt;
    private bool hasLeftGround;

    public PlayerJumpState CurrentState => currentState;
    public float LockedPower => lockedPower;
    public float LastJumpAngle => lastJumpAngle;
    public Vector2 LastJumpVector => lastJumpVector;
    public JumpTuningConfig JumpTuning => jumpTuning;

    public void ApplyJumpTuningNow()
    {
        CacheReferences();
        ApplyJumpTuning();
    }

    private void Awake()
    {
        CacheReferences();
        ApplyJumpTuning();
    }

    private void Start()
    {
        if (groundChecker.CheckGroundedNow())
        {
            EnterIdle();
        }
        else
        {
            CancelPreparationAndWaitForLanding();
        }
    }

    private void Update()
    {
        inputReader.Tick();

        bool grounded = groundChecker.CheckGroundedNow();

        switch (currentState)
        {
            case PlayerJumpState.Idle:
                UpdateIdle(grounded);
                break;
            case PlayerJumpState.PowerGaugeReady:
                UpdatePowerGaugeReady(grounded);
                break;
            case PlayerJumpState.Aiming:
                UpdateAiming(grounded);
                break;
            case PlayerJumpState.ChargingPower:
                UpdateChargingPower(grounded);
                break;
            case PlayerJumpState.Jumping:
                UpdateJumping(grounded);
                break;
        }
    }

    private void UpdateIdle(bool grounded)
    {
        if (!grounded)
        {
            currentState = PlayerJumpState.Jumping;
            hasLeftGround = true;
            playerVisual.SetState(currentState);
            return;
        }

        if (inputReader.WasPressed)
        {
            BeginAiming();
        }
    }

    private void UpdatePowerGaugeReady(bool grounded)
    {
        if (!grounded)
        {
            CancelPreparationAndWaitForLanding();
            return;
        }

        if (inputReader.WasPressed)
        {
            lockedJumpAngle = angleAim.CurrentAngle;
            lockedJumpDirection = angleAim.CurrentDirection;
            BeginPowerGauge();
            return;
        }

        angleAim.TickAim(Time.deltaTime);
    }

    private void UpdateAiming(bool grounded)
    {
        if (!grounded)
        {
            CancelPreparationAndWaitForLanding();
            return;
        }

        angleAim.TickAim(Time.deltaTime);

        if (inputReader.WasReleased)
        {
            currentState = PlayerJumpState.PowerGaugeReady;
            playerVisual.SetState(currentState);
        }
    }

    private void UpdateChargingPower(bool grounded)
    {
        if (!grounded)
        {
            CancelPreparationAndWaitForLanding();
            return;
        }

        if (inputReader.WasReleased)
        {
            lockedPower = powerGauge.LockCurrentPower();
            ExecuteJump();
            return;
        }

        powerGauge.TickGauge(Time.deltaTime);
    }

    private void UpdateJumping(bool grounded)
    {
        if (!hasLeftGround)
        {
            if (!grounded || Time.time - jumpStartedAt >= minimumJumpingTime)
            {
                hasLeftGround = true;
            }

            return;
        }

        bool slowEnoughToLand = jumpMotor.VerticalVelocity <= landingVerticalSpeedThreshold;
        if (grounded && slowEnoughToLand)
        {
            EnterIdle();
        }
    }

    private void BeginPowerGauge()
    {
        currentState = PlayerJumpState.ChargingPower;
        powerGauge.BeginGauge();
        playerVisual.SetState(currentState);
    }

    private void BeginAiming()
    {
        currentState = PlayerJumpState.Aiming;
        lockedPower = 0f;
        lockedJumpAngle = 0f;
        lockedJumpDirection = Vector2.up;
        hasLeftGround = false;

        powerGauge.Hide();
        angleAim.BeginAim();
        playerVisual.SetState(currentState);
    }

    private void ExecuteJump()
    {
        Vector2 direction = lockedJumpDirection.sqrMagnitude > 0.0001f
            ? lockedJumpDirection.normalized
            : Vector2.up;

        lastJumpAngle = lockedJumpAngle;
        lastJumpVector = jumpMotor.Jump(lockedPower, direction);

        powerGauge.Hide();
        angleAim.Hide();

        currentState = PlayerJumpState.Jumping;
        jumpStartedAt = Time.time;
        hasLeftGround = false;

        playerVisual.OnJump(direction);
        playerVisual.SetState(currentState);
    }

    private void CancelPreparationAndWaitForLanding()
    {
        powerGauge.Hide();
        angleAim.Hide();

        currentState = PlayerJumpState.Jumping;
        hasLeftGround = true;
        playerVisual.SetState(currentState);
    }

    private void EnterIdle()
    {
        currentState = PlayerJumpState.Idle;
        lockedPower = 0f;
        lockedJumpAngle = 0f;
        lockedJumpDirection = Vector2.up;
        hasLeftGround = false;

        powerGauge.Hide();
        angleAim.Hide();
        playerVisual.SetState(currentState);
    }

    private void CacheReferences()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<JumpInputReader>();
        }

        if (powerGauge == null)
        {
            powerGauge = GetComponent<JumpPowerGauge>();
        }

        if (angleAim == null)
        {
            angleAim = GetComponent<JumpAngleAim>();
        }

        if (jumpMotor == null)
        {
            jumpMotor = GetComponent<PlayerJumpMotor>();
        }

        if (groundChecker == null)
        {
            groundChecker = GetComponent<GroundChecker>();
        }

        if (playerVisual == null)
        {
            playerVisual = GetComponent<PlayerVisual>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<BoxCollider2D>();
        }
    }

    private void ApplyJumpTuning()
    {
        if (jumpTuning == null)
        {
            jumpTuning = new JumpTuningConfig();
        }

        jumpTuning.Validate();

        float playerSize = jumpTuning.PlayerSquareSize;
        if (bodyCollider != null)
        {
            bodyCollider.size = Vector2.one * playerSize;
        }

        if (playerVisual != null)
        {
            playerVisual.SetBodySize(playerSize);
        }

        if (angleAim != null)
        {
            angleAim.SetTuningConfig(jumpTuning);
        }

        if (powerGauge != null)
        {
            powerGauge.SetTuningConfig(jumpTuning);
        }
    }

    private void OnValidate()
    {
        minimumJumpingTime = Mathf.Max(0f, minimumJumpingTime);
        landingVerticalSpeedThreshold = Mathf.Max(0f, landingVerticalSpeedThreshold);
        CacheReferences();
        ApplyJumpTuning();
    }
}
