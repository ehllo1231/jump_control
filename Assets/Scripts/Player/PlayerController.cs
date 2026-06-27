using System.Collections.Generic;
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
    private const float DebugJumpHistorySamePositionToleranceSqr = 0.000001f;
    private const int CustomJumpWindowId = 240624;
    private const string SavedPlayerPositionExistsKey = "JumpTiming.PlayerPosition.Exists";
    private const string SavedPlayerPositionXKey = "JumpTiming.PlayerPosition.X";
    private const string SavedPlayerPositionYKey = "JumpTiming.PlayerPosition.Y";
    private static readonly Vector2 CustomJumpButtonWorldOffset = new Vector2(1.1f, 0.55f);

    [Header("References")]
    [SerializeField] private JumpInputReader inputReader;
    [SerializeField] private JumpPowerGauge powerGauge;
    [SerializeField] private JumpAngleAim angleAim;
    [SerializeField] private PlayerJumpMotor jumpMotor;
    [SerializeField] private GroundChecker groundChecker;
    [SerializeField] private PlayerVisual playerVisual;
    [SerializeField] private BoxCollider2D bodyCollider;
    [SerializeField] private Rigidbody2D body;

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

    private readonly List<Vector2> debugJumpPositionHistory = new List<Vector2>();
    private float jumpStartedAt;
    private bool hasLeftGround;
    private int debugJumpHistoryCursor = -1;
    private bool debugHistoryCurrentPositionCaptured;
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
    public event System.Action<PlayerController> JumpExecuted;

    public void ApplyJumpTuningNow()
    {
        CacheReferences();
        ApplyJumpTuning();
    }

    private void Awake()
    {
        CacheReferences();
        ApplyJumpTuning();
        RestoreSavedPlayerPosition();
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
        if (UpdateDebugJumpHistoryNavigation())
        {
            return;
        }

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

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveCurrentPlayerPosition();
        }
    }

    private void OnApplicationQuit()
    {
        SaveCurrentPlayerPosition();
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
        SaveDebugJumpReturnPosition();
        lastJumpVector = jumpMotor.Jump(lockedPower, direction);

        powerGauge.Hide();
        angleAim.Hide();

        currentState = PlayerJumpState.Jumping;
        jumpStartedAt = Time.time;
        hasLeftGround = false;

        playerVisual.OnJump(direction);
        playerVisual.SetState(currentState);
        JumpExecuted?.Invoke(this);
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

    private bool UpdateDebugJumpHistoryNavigation()
    {
        if (!IsDebugModeEnabled())
        {
            return false;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            StepDebugJumpHistory(-1);
            return true;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            StepDebugJumpHistory(1);
            return true;
        }

        return false;
    }

    private void StepDebugJumpHistory(int direction)
    {
        if (debugJumpPositionHistory.Count == 0)
        {
            return;
        }

        if (!debugHistoryCurrentPositionCaptured)
        {
            debugJumpPositionHistory.Add(GetCurrentPosition());
            debugJumpHistoryCursor = debugJumpPositionHistory.Count - 1;
            debugHistoryCurrentPositionCaptured = true;
        }

        int nextIndex = Mathf.Clamp(
            debugJumpHistoryCursor + direction,
            0,
            debugJumpPositionHistory.Count - 1);
        if (nextIndex == debugJumpHistoryCursor)
        {
            return;
        }

        debugJumpHistoryCursor = nextIndex;
        MoveToDebugJumpHistoryPosition(debugJumpPositionHistory[debugJumpHistoryCursor]);
    }

    private void SaveDebugJumpReturnPosition()
    {
        Vector2 currentPosition = GetCurrentPosition();
        if (debugHistoryCurrentPositionCaptured)
        {
            if (debugJumpHistoryCursor < debugJumpPositionHistory.Count - 1)
            {
                debugJumpPositionHistory.RemoveRange(
                    debugJumpHistoryCursor + 1,
                    debugJumpPositionHistory.Count - debugJumpHistoryCursor - 1);
            }

            if (debugJumpHistoryCursor >= 0
                && debugJumpHistoryCursor < debugJumpPositionHistory.Count
                && IsSameDebugJumpHistoryPosition(debugJumpPositionHistory[debugJumpHistoryCursor], currentPosition))
            {
                debugHistoryCurrentPositionCaptured = false;
                return;
            }
        }

        debugJumpPositionHistory.Add(currentPosition);
        debugJumpHistoryCursor = debugJumpPositionHistory.Count - 1;
        debugHistoryCurrentPositionCaptured = false;
    }

    private static bool IsSameDebugJumpHistoryPosition(Vector2 a, Vector2 b)
    {
        return (a - b).sqrMagnitude <= DebugJumpHistorySamePositionToleranceSqr;
    }

    private Vector2 GetCurrentPosition()
    {
        return body != null ? body.position : (Vector2)transform.position;
    }

    private void SaveCurrentPlayerPosition()
    {
        Vector2 currentPosition = GetCurrentPosition();
        if (!IsValidSavedPlayerPosition(currentPosition))
        {
            return;
        }

        PlayerPrefs.SetInt(SavedPlayerPositionExistsKey, 1);
        PlayerPrefs.SetFloat(SavedPlayerPositionXKey, currentPosition.x);
        PlayerPrefs.SetFloat(SavedPlayerPositionYKey, currentPosition.y);
        PlayerPrefs.Save();
    }

    private void RestoreSavedPlayerPosition()
    {
        if (!TryLoadSavedPlayerPosition(out Vector2 savedPosition))
        {
            return;
        }

        if (body != null)
        {
            body.position = savedPosition;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        else
        {
            Vector3 currentPosition = transform.position;
            transform.position = new Vector3(savedPosition.x, savedPosition.y, currentPosition.z);
        }

        Physics2D.SyncTransforms();
    }

    private static bool TryLoadSavedPlayerPosition(out Vector2 savedPosition)
    {
        savedPosition = default;
        if (PlayerPrefs.GetInt(SavedPlayerPositionExistsKey, 0) != 1)
        {
            return false;
        }

        savedPosition = new Vector2(
            PlayerPrefs.GetFloat(SavedPlayerPositionXKey),
            PlayerPrefs.GetFloat(SavedPlayerPositionYKey));
        return IsValidSavedPlayerPosition(savedPosition);
    }

    private static bool IsValidSavedPlayerPosition(Vector2 position)
    {
        return IsFinite(position.x) && IsFinite(position.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void MoveToDebugJumpHistoryPosition(Vector2 position)
    {
        customJumpWindowOpen = false;
        powerGauge.Hide();
        angleAim.Hide();

        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        else
        {
            Vector3 currentPosition = transform.position;
            transform.position = new Vector3(position.x, position.y, currentPosition.z);
        }

        Physics2D.SyncTransforms();
        if (groundChecker != null && groundChecker.CheckGroundedNow())
        {
            EnterIdle();
        }
        else
        {
            CancelPreparationAndWaitForLanding();
        }
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

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
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
