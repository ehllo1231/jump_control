using System.Globalization;
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
    private const float CustomJumpButtonWidth = 112f;
    private const float CustomJumpButtonHeight = 30f;
    private const float CustomJumpWindowWidth = 220f;
    private const float CustomJumpWindowHeight = 126f;
    private const int CustomJumpWindowId = 240624;
    private static readonly Vector2 CustomJumpButtonWorldOffset = new Vector2(1.1f, 0.55f);

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
    private bool customJumpWindowOpen;
    private Rect customJumpWindowRect;
    private string customJumpAngleText = "0";
    private string customJumpGaugePercentText = "100";
    private float customJumpAngle;
    private float customJumpGaugeNormalized = 1f;

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
        if (UpdateDebugCustomJump(grounded))
        {
            return;
        }

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

    private void OnGUI()
    {
        if (!Application.isPlaying || !IsDebugModeEnabled())
        {
            return;
        }

        Rect buttonRect;
        if (!TryGetCustomJumpButtonRect(out buttonRect))
        {
            return;
        }

        bool canStartCustomJump = CanStartDebugCustomJump();
        bool previousEnabled = GUI.enabled;
        GUI.enabled = canStartCustomJump;
        if (GUI.Button(buttonRect, "Custom Jump"))
        {
            OpenDebugCustomJumpWindow(buttonRect);
        }

        GUI.enabled = previousEnabled;

        if (customJumpWindowOpen)
        {
            customJumpWindowRect = GUI.Window(
                CustomJumpWindowId,
                customJumpWindowRect,
                DrawCustomJumpWindow,
                "Custom Jump");
            customJumpWindowRect = ClampToScreen(customJumpWindowRect);
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

    private bool UpdateDebugCustomJump(bool grounded)
    {
        if (!IsDebugModeEnabled())
        {
            if (customJumpWindowOpen)
            {
                CancelDebugCustomJump();
            }

            return false;
        }

        if (!customJumpWindowOpen)
        {
            return false;
        }

        if (!grounded || currentState != PlayerJumpState.Idle)
        {
            CancelDebugCustomJump();
            return false;
        }

        RefreshDebugCustomJumpPreview();
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ExecuteDebugCustomJump();
        }

        return true;
    }

    private void OpenDebugCustomJumpWindow(Rect buttonRect)
    {
        if (!CanStartDebugCustomJump())
        {
            return;
        }

        customJumpWindowOpen = true;
        customJumpWindowRect = ClampToScreen(new Rect(
            buttonRect.x,
            buttonRect.y + buttonRect.height + 4f,
            CustomJumpWindowWidth,
            CustomJumpWindowHeight));

        RefreshDebugCustomJumpPreview();
    }

    private void DrawCustomJumpWindow(int windowId)
    {
        GUILayout.Label("Angle");
        customJumpAngleText = GUILayout.TextField(customJumpAngleText);
        GUILayout.Label("Gauge %");
        customJumpGaugePercentText = GUILayout.TextField(customJumpGaugePercentText);

        RefreshDebugCustomJumpPreview();

        if (GUILayout.Button("Cancel"))
        {
            CancelDebugCustomJump();
        }

        GUI.DragWindow(new Rect(0f, 0f, CustomJumpWindowWidth, 20f));
    }

    private void RefreshDebugCustomJumpPreview()
    {
        if (TryParseFloat(customJumpAngleText, out float parsedAngle))
        {
            customJumpAngle = parsedAngle;
        }

        if (TryParseFloat(customJumpGaugePercentText, out float parsedGaugePercent))
        {
            customJumpGaugeNormalized = Mathf.Clamp01(parsedGaugePercent / 100f);
        }

        angleAim.ShowAngle(customJumpAngle);
        powerGauge.ShowLockedValue(customJumpGaugeNormalized);
    }

    private void ExecuteDebugCustomJump()
    {
        lockedJumpAngle = customJumpAngle;
        lockedJumpDirection = GetDirectionFromAngle(customJumpAngle);
        lockedPower = powerGauge.ShowLockedValue(customJumpGaugeNormalized);

        customJumpWindowOpen = false;
        ExecuteJump();
    }

    private void CancelDebugCustomJump()
    {
        customJumpWindowOpen = false;
        angleAim.Hide();
        powerGauge.Hide();

        if (currentState == PlayerJumpState.Idle)
        {
            playerVisual.SetState(currentState);
        }
    }

    private bool CanStartDebugCustomJump()
    {
        return IsDebugModeEnabled()
            && !customJumpWindowOpen
            && currentState == PlayerJumpState.Idle
            && groundChecker != null
            && groundChecker.CheckGroundedNow();
    }

    private bool IsDebugModeEnabled()
    {
        return jumpTuning != null && jumpTuning.DebugModeEnabled;
    }

    private bool TryGetCustomJumpButtonRect(out Rect buttonRect)
    {
        buttonRect = default;

        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        }

        if (camera == null)
        {
            return false;
        }

        Vector3 worldPosition = transform.position + (Vector3)CustomJumpButtonWorldOffset;
        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z < 0f)
        {
            return false;
        }

        buttonRect = ClampToScreen(new Rect(
            screenPosition.x - CustomJumpButtonWidth * 0.5f,
            Screen.height - screenPosition.y - CustomJumpButtonHeight * 0.5f,
            CustomJumpButtonWidth,
            CustomJumpButtonHeight));
        return true;
    }

    private static Rect ClampToScreen(Rect rect)
    {
        float maxX = Mathf.Max(0f, Screen.width - rect.width);
        float maxY = Mathf.Max(0f, Screen.height - rect.height);
        rect.x = Mathf.Clamp(rect.x, 0f, maxX);
        rect.y = Mathf.Clamp(rect.y, 0f, maxY);
        return rect;
    }

    private static Vector2 GetDirectionFromAngle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)).normalized;
    }

    private static bool TryParseFloat(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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

        if (jumpMotor != null)
        {
            jumpMotor.SetWallBounceElasticity(jumpTuning.WallBounceElasticity);
            jumpMotor.SetWallBounceVerticalVelocityMode(jumpTuning.WallBounceVerticalVelocityMode);
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
