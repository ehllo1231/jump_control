using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Player의 현재 위치와 점프 튜닝값으로 Scene View에 도달 후보 궤적을 표시합니다.
/// </summary>
[InitializeOnLoad]
public static class JumpReachabilityOverlay
{
    private const string EnabledKey = "JumpTiming.JumpReachabilityOverlay.Enabled";
    private const string MenuPath = "Tools/Jump Timing/Show Jump Reachability";
    private const int AngleSampleCount = 13;
    private const int PowerSampleCount = 4;
    private const int TrajectoryStepCount = 56;
    private const float MinimumPreviewSeconds = 0.75f;
    private const float MaximumPreviewSeconds = 4.5f;
    private const float CandidateTopInset = 0.02f;

    private static readonly Color lowPowerColor = new Color(0.2f, 0.62f, 1f, 0.18f);
    private static readonly Color highPowerColor = new Color(1f, 0.78f, 0.18f, 0.72f);
    private static readonly Color candidateColor = new Color(0.2f, 1f, 0.55f, 0.95f);
    private static readonly Color startColor = new Color(1f, 1f, 1f, 0.95f);

    private static GUIStyle labelStyle;
    private static GUIStyle compactLabelStyle;

    public static bool IsEnabled => EditorPrefs.GetBool(EnabledKey, true);

    static JumpReachabilityOverlay()
    {
        SceneView.duringSceneGui += DrawReachability;
        Selection.selectionChanged += SceneView.RepaintAll;
        EditorApplication.playModeStateChanged += _ => SceneView.RepaintAll();
        EditorApplication.update += RepaintWhilePlaying;
    }

    public static void SetEnabled(bool enabled)
    {
        EditorPrefs.SetBool(EnabledKey, enabled);
        Menu.SetChecked(MenuPath, enabled);
        SceneView.RepaintAll();
    }

    [MenuItem(MenuPath, false, 31)]
    private static void ToggleReachability()
    {
        SetEnabled(!IsEnabled);
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggleReachability()
    {
        Menu.SetChecked(MenuPath, IsEnabled);
        return true;
    }

    private static void RepaintWhilePlaying()
    {
        if (Application.isPlaying && IsEnabled)
        {
            SceneView.RepaintAll();
        }
    }

    private static void DrawReachability(SceneView sceneView)
    {
        if (!IsEnabled)
        {
            return;
        }

        PlayerController player = FindPlayerController();
        if (player == null || player.JumpTuning == null)
        {
            return;
        }

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        PlayerJumpMotor jumpMotor = player.GetComponent<PlayerJumpMotor>();
        JumpTuningConfig tuning = player.JumpTuning;

        Vector2 start = GetStartPosition(player, body);
        Vector2 gravity = GetGravity(body);
        Vector2 playerHalfSize = GetPlayerHalfSize(player, tuning);
        float playerHalfWidth = playerHalfSize.x;
        float playerHalfHeight = playerHalfSize.y;
        List<Platform2D> platforms = FindScenePlatforms();
        Dictionary<Platform2D, ReachCandidate> candidates = new Dictionary<Platform2D, ReachCandidate>();

        DrawStartMarker(start, tuning);

        Color previousColor = Handles.color;
        for (int powerIndex = 0; powerIndex < PowerSampleCount; powerIndex++)
        {
            float normalizedPower = PowerSampleCount == 1 ? 1f : powerIndex / (float)(PowerSampleCount - 1);
            float power = tuning.EvaluateJumpPower(normalizedPower);
            Color trajectoryColor = Color.Lerp(lowPowerColor, highPowerColor, normalizedPower);
            float lineWidth = Mathf.Lerp(1.2f, 2.4f, normalizedPower);

            for (int angleIndex = 0; angleIndex < AngleSampleCount; angleIndex++)
            {
                float normalizedAngle = AngleSampleCount == 1 ? 0.5f : angleIndex / (float)(AngleSampleCount - 1);
                float angle = Mathf.Lerp(tuning.MinDirectionAngle, tuning.MaxDirectionAngle, normalizedAngle);
                Vector2 launchVelocity = CalculateLaunchVelocity(angle, power, body, jumpMotor);
                Vector3[] points = SampleTrajectory(start, launchVelocity, gravity);

                Handles.color = trajectoryColor;
                Handles.DrawAAPolyLine(lineWidth, points);
                CollectReachableCandidates(
                    points,
                    platforms,
                    playerHalfWidth,
                    playerHalfHeight,
                    normalizedPower,
                    angle,
                    candidates);
            }
        }

        DrawReachableCandidates(candidates, playerHalfWidth);
        Handles.color = previousColor;
    }

    private static PlayerController FindPlayerController()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject != null)
        {
            PlayerController selectedPlayer = selectedObject.GetComponentInParent<PlayerController>();
            if (selectedPlayer == null)
            {
                selectedPlayer = selectedObject.GetComponentInChildren<PlayerController>(true);
            }

            if (selectedPlayer != null)
            {
                return selectedPlayer;
            }
        }

        return Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    }

    private static List<Platform2D> FindScenePlatforms()
    {
        Platform2D[] allPlatforms = Resources.FindObjectsOfTypeAll<Platform2D>();
        List<Platform2D> scenePlatforms = new List<Platform2D>(allPlatforms.Length);

        foreach (Platform2D platform in allPlatforms)
        {
            if (platform == null
                || EditorUtility.IsPersistent(platform)
                || !platform.gameObject.scene.IsValid()
                || !platform.gameObject.activeInHierarchy)
            {
                continue;
            }

            scenePlatforms.Add(platform);
        }

        return scenePlatforms;
    }

    private static Vector2 GetStartPosition(PlayerController player, Rigidbody2D body)
    {
        if (Application.isPlaying && body != null)
        {
            return body.position;
        }

        return player.transform.position;
    }

    private static Vector2 GetGravity(Rigidbody2D body)
    {
        float gravityScale = body != null ? body.gravityScale : 1f;
        Vector2 gravity = Physics2D.gravity * gravityScale;
        return gravity.sqrMagnitude > 0.0001f ? gravity : new Vector2(0f, -9.81f);
    }

    private static Vector2 GetPlayerHalfSize(PlayerController player, JumpTuningConfig tuning)
    {
        BoxCollider2D collider = player.GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            Bounds bounds = collider.bounds;
            return new Vector2(Mathf.Max(0.01f, bounds.extents.x), Mathf.Max(0.01f, bounds.extents.y));
        }

        float halfSize = tuning.PlayerSquareSize * 0.5f;
        return new Vector2(halfSize, halfSize);
    }

    private static Vector2 CalculateLaunchVelocity(
        float angle,
        float power,
        Rigidbody2D body,
        PlayerJumpMotor jumpMotor)
    {
        Vector2 baseVelocity = Vector2.zero;
        if (Application.isPlaying && body != null)
        {
            baseVelocity = body.linearVelocity;
        }

        if (jumpMotor != null && jumpMotor.ClearVelocityBeforeJump)
        {
            baseVelocity = new Vector2(jumpMotor.KeepHorizontalVelocityOnJump ? baseVelocity.x : 0f, 0f);
        }

        float radians = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)).normalized;
        float impulseMultiplier = jumpMotor != null ? jumpMotor.ImpulseMultiplier : 1f;
        float mass = body != null ? Mathf.Max(0.0001f, body.mass) : 1f;
        return baseVelocity + direction * power * impulseMultiplier / mass;
    }

    private static Vector3[] SampleTrajectory(Vector2 start, Vector2 launchVelocity, Vector2 gravity)
    {
        float previewSeconds = CalculatePreviewSeconds(launchVelocity, gravity);
        Vector3[] points = new Vector3[TrajectoryStepCount + 1];

        for (int i = 0; i < points.Length; i++)
        {
            float t = previewSeconds * i / TrajectoryStepCount;
            Vector2 position = start + launchVelocity * t + 0.5f * gravity * t * t;
            points[i] = new Vector3(position.x, position.y, 0f);
        }

        return points;
    }

    private static float CalculatePreviewSeconds(Vector2 launchVelocity, Vector2 gravity)
    {
        float verticalGravity = Mathf.Abs(gravity.y);
        if (verticalGravity < 0.0001f)
        {
            return 2f;
        }

        float airborneSeconds = Mathf.Max(0f, launchVelocity.y) * 2f / verticalGravity;
        return Mathf.Clamp(airborneSeconds + 0.6f, MinimumPreviewSeconds, MaximumPreviewSeconds);
    }

    private static void CollectReachableCandidates(
        Vector3[] points,
        List<Platform2D> platforms,
        float playerHalfWidth,
        float playerHalfHeight,
        float normalizedPower,
        float angle,
        Dictionary<Platform2D, ReachCandidate> candidates)
    {
        for (int i = 1; i < points.Length; i++)
        {
            Vector2 previous = points[i - 1];
            Vector2 current = points[i];
            if (current.y > previous.y)
            {
                continue;
            }

            foreach (Platform2D platform in platforms)
            {
                if (candidates.ContainsKey(platform))
                {
                    continue;
                }

                if (TryFindLandingPoint(previous, current, platform, playerHalfWidth, playerHalfHeight, out Vector2 landingPoint))
                {
                    candidates.Add(platform, new ReachCandidate(landingPoint, normalizedPower, angle));
                }
            }
        }
    }

    private static bool TryFindLandingPoint(
        Vector2 previous,
        Vector2 current,
        Platform2D platform,
        float playerHalfWidth,
        float playerHalfHeight,
        out Vector2 landingPoint)
    {
        Bounds bounds = platform.WorldBounds;
        float centerLandingY = bounds.max.y + playerHalfHeight;

        landingPoint = default;
        if (previous.y < centerLandingY || current.y > centerLandingY)
        {
            return false;
        }

        float yDelta = current.y - previous.y;
        if (Mathf.Abs(yDelta) < 0.0001f)
        {
            return false;
        }

        float ratio = (centerLandingY - previous.y) / yDelta;
        if (ratio < 0f || ratio > 1f)
        {
            return false;
        }

        float x = Mathf.Lerp(previous.x, current.x, ratio);
        float minStandX = bounds.min.x + playerHalfWidth;
        float maxStandX = bounds.max.x - playerHalfWidth;
        if (minStandX > maxStandX || x < minStandX || x > maxStandX)
        {
            return false;
        }

        landingPoint = new Vector2(x, centerLandingY);
        return true;
    }

    private static void DrawStartMarker(Vector2 start, JumpTuningConfig tuning)
    {
        float handleSize = HandleUtility.GetHandleSize(start) * 0.08f;
        Handles.color = startColor;
        Handles.DrawWireDisc(start, Vector3.forward, handleSize);

        string label =
            $"Jump Reach\nAngle {tuning.MinDirectionAngle:0.#}°~{tuning.MaxDirectionAngle:0.#}°\nPower {tuning.MinimumJumpPower:0.#}~{tuning.MaximumJumpPower:0.#}";
        Handles.Label(start + Vector2.up * handleSize * 2.4f, label, GetLabelStyle());
    }

    private static void DrawReachableCandidates(Dictionary<Platform2D, ReachCandidate> candidates, float playerHalfWidth)
    {
        foreach (KeyValuePair<Platform2D, ReachCandidate> pair in candidates)
        {
            Platform2D platform = pair.Key;
            ReachCandidate candidate = pair.Value;
            if (platform == null)
            {
                continue;
            }

            Bounds bounds = platform.WorldBounds;
            float minStandX = bounds.min.x + playerHalfWidth;
            float maxStandX = bounds.max.x - playerHalfWidth;
            float topY = bounds.max.y + CandidateTopInset;
            Vector3 topLeft = new Vector3(minStandX, topY, 0f);
            Vector3 topRight = new Vector3(maxStandX, topY, 0f);
            float handleSize = HandleUtility.GetHandleSize(candidate.Point) * 0.08f;

            Handles.color = candidateColor;
            Handles.DrawAAPolyLine(5f, topLeft, topRight);
            Handles.DrawSolidDisc(candidate.Point, Vector3.forward, handleSize);

            string label = $"Reachable\n{candidate.Angle:0.#}° / {candidate.NormalizedPower:P0}";
            Handles.Label(candidate.Point + Vector2.up * handleSize * 2.2f, label, GetCompactLabelStyle());
        }
    }

    private static GUIStyle GetLabelStyle()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        return labelStyle;
    }

    private static GUIStyle GetCompactLabelStyle()
    {
        if (compactLabelStyle == null)
        {
            compactLabelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 10
            };
        }

        return compactLabelStyle;
    }

    private readonly struct ReachCandidate
    {
        public ReachCandidate(Vector2 point, float normalizedPower, float angle)
        {
            Point = point;
            NormalizedPower = normalizedPower;
            Angle = angle;
        }

        public Vector2 Point { get; }
        public float NormalizedPower { get; }
        public float Angle { get; }
    }
}
