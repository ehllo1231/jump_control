using System;
using UnityEngine;

/// <summary>
/// Unity Inspector에서 실시간으로 조정하는 점프 튜닝 값입니다.
/// PlayerController가 한 인스턴스를 보관하고 방향 및 게이지 컴포넌트에 공유합니다.
/// </summary>
[Serializable]
public sealed class JumpTuningConfig
{
    private const float DefaultPlayerSquareSize = 0.72f;
    private const float MinimumPlayerSquareSize = 0.1f;
    private const float DefaultMinAngle = -62f;
    private const float DefaultMaxAngle = 62f;
    private const float DefaultSweepSpeed = 145f;
    private const float DefaultChargeDuration = 1.15f;
    private const float MinimumChargeDuration = 0.05f;
    private const float DefaultMinimumJumpPower = 7f;
    private const float DefaultMaximumJumpPower = 16f;

    [Header("Player Body")]
    [SerializeField, Min(MinimumPlayerSquareSize)] private float playerSquareSize = DefaultPlayerSquareSize;

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

    public float PlayerSquareSize => Mathf.Max(MinimumPlayerSquareSize, playerSquareSize);
    public float MinDirectionAngle => Mathf.Min(minDirectionAngle, maxDirectionAngle);
    public float MaxDirectionAngle => Mathf.Max(minDirectionAngle, maxDirectionAngle);
    public float DirectionSweepSpeed => Mathf.Max(0f, directionSweepSpeed);
    public float DirectionStartNormalized => Mathf.Clamp01(directionStartNormalized);
    public float GaugeChargeDuration => Mathf.Max(MinimumChargeDuration, gaugeChargeDuration);
    public float MinimumJumpPower => Mathf.Min(minimumJumpPower, maximumJumpPower);
    public float MaximumJumpPower => Mathf.Max(minimumJumpPower, maximumJumpPower);

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
}
