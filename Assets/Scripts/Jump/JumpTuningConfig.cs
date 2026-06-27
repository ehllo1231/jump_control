using System;
using UnityEngine;

public enum WallBounceVerticalVelocityMode
{
    [InspectorName("Preserve Pre-Collision Y Velocity")]
    PreservePreCollisionVelocity = 0,
    [InspectorName("Use Current Collision Y Velocity")]
    UseCurrentCollisionVelocity = 1
}

/// <summary>
/// Unity Inspector에서 실시간으로 조정하는 점프 튜닝 값입니다.
/// PlayerController가 한 인스턴스를 보관하고 방향 및 게이지 컴포넌트에 공유합니다.
/// </summary>
[Serializable]
public sealed class JumpTuningConfig
{
    private const float DefaultPlayerSquareSize = 0.72f;
    private const float MinimumPlayerSquareSize = 0.1f;
    private const float DefaultPlayerVisualScale = 1.8f;
    private const float MinimumPlayerVisualScale = 0.1f;
    private const float DefaultMinAngle = -62f;
    private const float DefaultMaxAngle = 62f;
    private const float DefaultSweepSpeed = 145f;
    private const float DefaultChargeDuration = 1.15f;
    private const float MinimumChargeDuration = 0.05f;
    private const float DefaultMinimumJumpPower = 7f;
    private const float DefaultMaximumJumpPower = 16f;
    private const float DefaultWallBounceElasticity = 0.18f;
    private const float MaximumWallBounceElasticity = 2f;
    private const WallBounceVerticalVelocityMode DefaultWallBounceVerticalVelocityMode =
        WallBounceVerticalVelocityMode.PreservePreCollisionVelocity;

    [Header("Player Body")]
    [SerializeField, Min(MinimumPlayerSquareSize)] private float playerSquareSize = DefaultPlayerSquareSize;
    [Tooltip("충돌체 크기는 유지하고 Player 스프라이트만 키우거나 줄이는 배율입니다.")]
    [SerializeField, Min(MinimumPlayerVisualScale)] private float playerVisualScale = DefaultPlayerVisualScale;

    [Header("Collision")]
    [Tooltip("벽 충돌 시 반대 방향으로 되돌리는 속도 비율입니다. 바닥 착지에는 적용하지 않습니다.")]
    [SerializeField, Range(0f, MaximumWallBounceElasticity)] private float wallBounceElasticity = DefaultWallBounceElasticity;
    [Tooltip("벽 충돌 후 세로 속도를 충돌 직전 값으로 보존할지, 기존처럼 충돌 처리 후 현재 값을 사용할지 선택합니다.")]
    [SerializeField] private WallBounceVerticalVelocityMode wallBounceVerticalVelocityMode = DefaultWallBounceVerticalVelocityMode;

    [Header("Direction")]
    [SerializeField] private float minDirectionAngle = DefaultMinAngle;
    [SerializeField] private float maxDirectionAngle = DefaultMaxAngle;
    [SerializeField, Min(0f)] private float directionSweepSpeed = DefaultSweepSpeed;
    [SerializeField, Range(0f, 1f)] private float directionStartNormalized = 0.5f;

    [Header("Power Gauge")]
    [Tooltip("게이지가 최소에서 최대까지 도달하는 데 걸리는 시간(초)")]
    [SerializeField, Min(MinimumChargeDuration)] private float gaugeChargeDuration = DefaultChargeDuration;

    [SerializeField, Min(0f)] private float minimumJumpPower = DefaultMinimumJumpPower;
    [SerializeField, Min(0f)] private float maximumJumpPower = DefaultMaximumJumpPower;

    [Tooltip("X: 정규화 게이지 값(0~1), Y: 최소·최대 점프 세기 사이의 보간 비율(0~1)")]
    [SerializeField] private AnimationCurve gaugePowerResponse = CreateDefaultPowerResponse();

    [Header("Debug Mode")]
    [SerializeField] private bool debugModeEnabled;

    public float PlayerSquareSize => Mathf.Max(MinimumPlayerSquareSize, playerSquareSize);
    public float PlayerVisualScale => Mathf.Max(MinimumPlayerVisualScale, playerVisualScale);
    public float MinDirectionAngle => Mathf.Min(minDirectionAngle, maxDirectionAngle);
    public float MaxDirectionAngle => Mathf.Max(minDirectionAngle, maxDirectionAngle);
    public float DirectionSweepSpeed => Mathf.Max(0f, directionSweepSpeed);
    public float DirectionStartNormalized => Mathf.Clamp01(directionStartNormalized);
    public float GaugeChargeDuration => Mathf.Max(MinimumChargeDuration, gaugeChargeDuration);
    public float MinimumJumpPower => Mathf.Min(minimumJumpPower, maximumJumpPower);
    public float MaximumJumpPower => Mathf.Max(minimumJumpPower, maximumJumpPower);
    public float WallBounceElasticity => Mathf.Clamp(wallBounceElasticity, 0f, MaximumWallBounceElasticity);
    public WallBounceVerticalVelocityMode WallBounceVerticalVelocityMode =>
        NormalizeWallBounceVerticalVelocityMode(wallBounceVerticalVelocityMode);
    public bool DebugModeEnabled => debugModeEnabled;

    public float EvaluateJumpPower(float normalizedGaugeValue)
    {
        float normalizedValue = Mathf.Clamp01(normalizedGaugeValue);
        float response = HasUsablePowerResponse()
            ? Mathf.Clamp01(gaugePowerResponse.Evaluate(normalizedValue))
            : normalizedValue;
        return Mathf.Lerp(MinimumJumpPower, MaximumJumpPower, response);
    }

    public void Validate()
    {
        playerSquareSize = Mathf.Max(MinimumPlayerSquareSize, playerSquareSize);
        playerVisualScale = Mathf.Max(MinimumPlayerVisualScale, playerVisualScale);
        wallBounceElasticity = Mathf.Clamp(wallBounceElasticity, 0f, MaximumWallBounceElasticity);
        wallBounceVerticalVelocityMode = NormalizeWallBounceVerticalVelocityMode(wallBounceVerticalVelocityMode);
        directionSweepSpeed = Mathf.Max(0f, directionSweepSpeed);
        directionStartNormalized = Mathf.Clamp01(directionStartNormalized);
        gaugeChargeDuration = Mathf.Max(MinimumChargeDuration, gaugeChargeDuration);
        minimumJumpPower = Mathf.Max(0f, minimumJumpPower);
        maximumJumpPower = Mathf.Max(0f, maximumJumpPower);

        if (!HasUsablePowerResponse())
        {
            gaugePowerResponse = CreateDefaultPowerResponse();
        }
    }

    private bool HasUsablePowerResponse()
    {
        return gaugePowerResponse != null && gaugePowerResponse.length > 0;
    }

    private static AnimationCurve CreateDefaultPowerResponse()
    {
        return AnimationCurve.Linear(0f, 0f, 1f, 1f);
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
