using UnityEngine;

/// <summary>
/// Platform 간 측정 결과입니다. 향후 궤적 미리보기에서도 동일한 표면 좌표를 재사용할 수 있습니다.
/// </summary>
public readonly struct PlatformMeasurement
{
    public PlatformMeasurement(Vector2 start, Vector2 end)
    {
        Start = start;
        End = end;
        Delta = end - start;
        Distance = Delta.magnitude;
    }

    public Vector2 Start { get; }
    public Vector2 End { get; }
    public Vector2 Delta { get; }
    public float Distance { get; }
}

public static class PlatformMeasurementUtility
{
    public static PlatformMeasurement Measure(IPlatformSurface from, IPlatformSurface to)
    {
        return new PlatformMeasurement(from.TopCenter, to.TopCenter);
    }
}
