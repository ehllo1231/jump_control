using System.Globalization;
using UnityEngine;

public partial class PlayerController
{
    private const float CustomJumpButtonWidth = 112f;
    private const float CustomJumpButtonHeight = 30f;
    private const float CustomJumpWindowWidth = 300f;
    private const float CustomJumpWindowHeight = 360f;
    private const float CustomJumpPadSize = 168f;
    private const float CustomJumpPadHandleSize = 12f;
    private const int CustomJumpWindowId = 240624;
    private const int CustomJumpPadControlId = 240625;
    private static readonly Vector2 CustomJumpButtonWorldOffset = new Vector2(1.1f, 0.55f);

    private bool customJumpWindowOpen;
    private Rect customJumpWindowRect;
    private string customJumpAngleText = "0";
    private string customJumpGaugePercentText = "100";
    private float customJumpAngle;
    private float customJumpGaugeNormalized = 1f;
    private bool customJumpArmed;
    private bool customJumpPadDragging;

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
}
