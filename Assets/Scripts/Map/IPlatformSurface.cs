using UnityEngine;

/// <summary>
/// 거리 측정과 향후 점프 궤적 미리보기가 사용할 발판 표면 데이터 계약입니다.
/// </summary>
public interface IPlatformSurface
{
    Vector2 Size { get; }
    Vector2 TopCenter { get; }
    Bounds WorldBounds { get; }
}
