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
    private const float CustomJumpWindowWidth = 300f;
    private const float CustomJumpWindowHeight = 360f;
    private const float CustomJumpPadSize = 168f;
    private const float CustomJumpPadHandleSize = 12f;
    private const float DebugJumpHistorySamePositionToleranceSqr = 0.000001f;
    private const int CustomJumpWindowId = 240624;
    private const int CustomJumpPadControlId = 240625;
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
    private bool currentJumpWasExecuted;
    private int debugJumpHistoryCursor = -1;
    private bool debugHistoryCurrentPositionCaptured;
    private bool customJumpWindowOpen;
    private Rect customJumpWindowRect;
    private string customJumpAngleText = "0";
    private string customJumpGaugePercentText = "100";
    private float customJumpAngle;
    private float customJumpGaugeNormalized = 1f;
    private bool customJumpArmed;
    private bool customJumpPadDragging;

    public PlayerJumpState CurrentState => currentState;
    public float LockedPower => lockedPower;
    public float LastJumpAngle => lastJumpAngle;
    public Vector2 LastJumpVector => lastJumpVector;
    public JumpTuningConfig JumpTuning => jumpTuning;
    public event System.Action<PlayerController> JumpExecuted;
    public event System.Action<PlayerController> Landed;
    public event System.Action<PlayerController> DebugJumpHistoryMoved;

    public void ApplyJumpTuningNow()
    {
        CacheReferences();
        ApplyJumpTuning();
    }

    private void Awake()
    {
        CacheReferences();
        ApplyJumpTuning();
        if (ShouldUsePersistentPlayerPosition())
        {
            RestoreSavedPlayerPosition();
        }
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

        if (customJumpArmed)
        {
            if (GUI.Button(buttonRect, "Cancel"))
            {
                CancelDebugCustomJump();
            }

            if (customJumpWindowOpen)
            {
                customJumpWindowOpen = false;
            }

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
            customJumpWindowRect.width = CustomJumpWindowWidth;
            customJumpWindowRect.height = CustomJumpWindowHeight;
            customJumpWindowRect = ClampToScreen(customJumpWindowRect);
        }
    }

    private void UpdateIdle(bool grounded)
    {
        if (!grounded)
        {
            currentState = PlayerJumpState.Jumping;
            hasLeftGround = true;
            currentJumpWasExecuted = false;
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
            if (currentJumpWasExecuted)
            {
                Landed?.Invoke(this);
            }

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
        currentJumpWasExecuted = true;

        playerVisual.OnJump(direction);
        playerVisual.SetState(currentState);
        JumpExecuted?.Invoke(this);
    }

    private bool UpdateDebugCustomJump(bool grounded)
    {
        if (!IsDebugModeEnabled())
        {
            if (customJumpWindowOpen || customJumpArmed)
            {
                CancelDebugCustomJump();
            }

            return false;
        }

        if ((customJumpWindowOpen || customJumpArmed) && Input.GetKeyDown(KeyCode.D))
        {
            CancelDebugCustomJump();
            return true;
        }

        if (customJumpWindowOpen)
        {
            if (!grounded || currentState != PlayerJumpState.Idle)
            {
                CancelDebugCustomJump();
                return false;
            }

            RefreshDebugCustomJumpPreview();
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ArmDebugCustomJump();
                ExecuteDebugCustomJump();
            }

            return true;
        }

        if (!customJumpArmed)
        {
            return false;
        }

        if (currentState != PlayerJumpState.Idle)
        {
            return false;
        }

        if (!grounded)
        {
            CancelPreparationAndWaitForLanding();
            return true;
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
        if (!ShouldUsePersistentPlayerPosition())
        {
            return;
        }

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

        DebugJumpHistoryMoved?.Invoke(this);
    }

    private void OpenDebugCustomJumpWindow(Rect buttonRect)
    {
        if (!CanStartDebugCustomJump())
        {
            return;
        }

        customJumpWindowOpen = true;
        customJumpPadDragging = false;
        SynchronizeCustomJumpText();
        customJumpWindowRect = ClampToScreen(new Rect(
            buttonRect.x,
            buttonRect.y + buttonRect.height + 4f,
            CustomJumpWindowWidth,
            CustomJumpWindowHeight));

        RefreshDebugCustomJumpPreview();
    }

    private void DrawCustomJumpWindow(int windowId)
    {
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        Rect padRect = GUILayoutUtility.GetRect(
            CustomJumpPadSize,
            CustomJumpPadSize,
            GUILayout.Width(CustomJumpPadSize),
            GUILayout.Height(CustomJumpPadSize));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        DrawCustomJumpPad(padRect);
        GUILayout.Space(8f);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Angle", GUILayout.Width(64f));
        customJumpAngleText = GUILayout.TextField(customJumpAngleText, GUILayout.Width(74f));
        GUILayout.Label("deg", GUILayout.Width(32f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Power", GUILayout.Width(64f));
        customJumpGaugePercentText = GUILayout.TextField(customJumpGaugePercentText, GUILayout.Width(74f));
        GUILayout.Label("%", GUILayout.Width(32f));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        RefreshDebugCustomJumpPreview();
        GUILayout.Space(8f);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply", GUILayout.Height(28f)))
        {
            ArmDebugCustomJump();
        }

        if (GUILayout.Button("Cancel", GUILayout.Height(28f)))
        {
            CancelDebugCustomJump();
        }
        GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0f, 0f, CustomJumpWindowWidth, 20f));
    }

    private void DrawCustomJumpPad(Rect padRect)
    {
        HandleCustomJumpPadInput(padRect);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.08f, 0.09f, 0.1f, 0.92f);
        GUI.Box(padRect, GUIContent.none);
        GUI.color = previousColor;

        Vector2 center = padRect.center;
        float radius = GetCustomJumpPadRadius(padRect);
        DrawGuiLine(center + Vector2.left * radius, center + Vector2.right * radius, 1f, new Color(1f, 1f, 1f, 0.22f));
        DrawGuiLine(center + Vector2.up * radius, center + Vector2.down * radius, 1f, new Color(1f, 1f, 1f, 0.22f));

        Vector2 handlePosition = GetCustomJumpPadHandlePosition(padRect);
        DrawGuiLine(center, handlePosition, 3f, new Color(0.2f, 1f, 0.55f, 0.88f));

        Rect handleRect = new Rect(
            handlePosition.x - CustomJumpPadHandleSize * 0.5f,
            handlePosition.y - CustomJumpPadHandleSize * 0.5f,
            CustomJumpPadHandleSize,
            CustomJumpPadHandleSize);
        GUI.color = new Color(0.2f, 1f, 0.55f, 1f);
        GUI.Box(handleRect, GUIContent.none);
        GUI.color = previousColor;
    }

    private void HandleCustomJumpPadInput(Rect padRect)
    {
        Event current = Event.current;
        if (current == null)
        {
            return;
        }

        int controlId = GUIUtility.GetControlID(CustomJumpPadControlId, FocusType.Passive, padRect);
        if (current.type == EventType.MouseDown && current.button == 0 && padRect.Contains(current.mousePosition))
        {
            GUIUtility.hotControl = controlId;
            customJumpPadDragging = true;
            SetCustomJumpFromPadPosition(current.mousePosition, padRect);
            current.Use();
            return;
        }

        if (current.type == EventType.MouseDrag
            && current.button == 0
            && customJumpPadDragging
            && GUIUtility.hotControl == controlId)
        {
            SetCustomJumpFromPadPosition(current.mousePosition, padRect);
            current.Use();
            return;
        }

        if (current.type == EventType.MouseUp
            && customJumpPadDragging
            && GUIUtility.hotControl == controlId)
        {
            GUIUtility.hotControl = 0;
            customJumpPadDragging = false;
            current.Use();
        }
    }

    private void SetCustomJumpFromPadPosition(Vector2 mousePosition, Rect padRect)
    {
        Vector2 center = padRect.center;
        Vector2 screenVector = mousePosition - center;
        Vector2 jumpVector = new Vector2(screenVector.x, -screenVector.y);
        float radius = GetCustomJumpPadRadius(padRect);
        float magnitude = Mathf.Clamp(jumpVector.magnitude, 0f, radius);
        float nextGauge = radius > 0f ? magnitude / radius : 0f;
        float nextAngle = magnitude > 0.001f
            ? Mathf.Atan2(jumpVector.x, jumpVector.y) * Mathf.Rad2Deg
            : 0f;

        customJumpAngle = ClampCustomJumpAngle(nextAngle);
        customJumpGaugeNormalized = Mathf.Clamp01(nextGauge);
        SynchronizeCustomJumpText();
        RefreshDebugCustomJumpPreview();
    }

    private Vector2 GetCustomJumpPadHandlePosition(Rect padRect)
    {
        Vector2 direction = GetDirectionFromAngle(ClampCustomJumpAngle(customJumpAngle));
        Vector2 screenDirection = new Vector2(direction.x, -direction.y);
        return padRect.center + screenDirection * GetCustomJumpPadRadius(padRect) * Mathf.Clamp01(customJumpGaugeNormalized);
    }

    private static float GetCustomJumpPadRadius(Rect padRect)
    {
        return Mathf.Max(1f, Mathf.Min(padRect.width, padRect.height) * 0.5f - CustomJumpPadHandleSize);
    }

    private static void DrawGuiLine(Vector2 start, Vector2 end, float width, Color color)
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        Vector2 delta = end - start;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float length = delta.magnitude;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, length, width), Texture2D.whiteTexture);
        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private void RefreshDebugCustomJumpPreview()
    {
        if (TryParseFloat(customJumpAngleText, out float parsedAngle))
        {
            customJumpAngle = ClampCustomJumpAngle(parsedAngle);
        }

        if (TryParseFloat(customJumpGaugePercentText, out float parsedGaugePercent))
        {
            customJumpGaugeNormalized = Mathf.Clamp01(parsedGaugePercent / 100f);
        }

        angleAim.ShowAngle(customJumpAngle);
        powerGauge.ShowLockedValue(customJumpGaugeNormalized);
    }

    private void ArmDebugCustomJump()
    {
        RefreshDebugCustomJumpPreview();
        SynchronizeCustomJumpText();
        customJumpWindowOpen = false;
        customJumpArmed = true;
        if (customJumpPadDragging)
        {
            GUIUtility.hotControl = 0;
        }

        customJumpPadDragging = false;
    }

    private void ExecuteDebugCustomJump()
    {
        ArmDebugCustomJump();
        lockedJumpAngle = ClampCustomJumpAngle(customJumpAngle);
        lockedJumpDirection = GetDirectionFromAngle(lockedJumpAngle);
        lockedPower = powerGauge.ShowLockedValue(customJumpGaugeNormalized);

        ExecuteJump();
    }

    private void CancelDebugCustomJump()
    {
        customJumpWindowOpen = false;
        customJumpArmed = false;
        if (customJumpPadDragging)
        {
            GUIUtility.hotControl = 0;
        }

        customJumpPadDragging = false;

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
            && !customJumpArmed
            && currentState == PlayerJumpState.Idle
            && groundChecker != null
            && groundChecker.CheckGroundedNow();
    }

    private void SynchronizeCustomJumpText()
    {
        customJumpAngleText = ClampCustomJumpAngle(customJumpAngle).ToString("0.##", CultureInfo.InvariantCulture);
        customJumpGaugePercentText = (Mathf.Clamp01(customJumpGaugeNormalized) * 100f).ToString("0.#", CultureInfo.InvariantCulture);
    }

    private float ClampCustomJumpAngle(float angle)
    {
        return jumpTuning != null
            ? Mathf.Clamp(angle, jumpTuning.MinDirectionAngle, jumpTuning.MaxDirectionAngle)
            : angle;
    }

    private bool IsDebugModeEnabled()
    {
        return jumpTuning != null && jumpTuning.DebugModeEnabled;
    }

    private bool ShouldUsePersistentPlayerPosition()
    {
        return !IsDebugModeEnabled();
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
        currentJumpWasExecuted = false;
        playerVisual.SetState(currentState);
    }

    private void EnterIdle()
    {
        currentState = PlayerJumpState.Idle;
        lockedPower = 0f;
        lockedJumpAngle = 0f;
        lockedJumpDirection = Vector2.up;
        hasLeftGround = false;
        currentJumpWasExecuted = false;

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
