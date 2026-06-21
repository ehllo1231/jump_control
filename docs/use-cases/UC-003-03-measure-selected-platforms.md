# UC-003-03: 선택한 두 Platform 거리 표시

- 상태: Implemented
- 마지막 갱신일: 2026-06-21

## 목적

맵 제작자가 Scene View에서 두 Platform 사이의 수평·수직 거리와 전체 거리를 확인한다.

## 액터

- 맵 제작자
- 개발자

## 사전 조건

- Unity Editor에서 맵 씬이 열려 있어야 한다.
- 씬에 `Platform2D`가 두 개 이상 있어야 한다.

## 트리거

- 사용자가 Scene View 또는 Hierarchy에서 Platform 두 개를 함께 선택한다.

## 기본 흐름

1. 시스템은 선택된 오브젝트에서 고유한 `Platform2D` 두 개를 찾는다.
2. 시스템은 두 Platform의 TopCenter를 기준으로 측정값을 계산한다.
3. 시스템은 Scene View에 두 점을 잇는 선을 표시한다.
4. 시스템은 `ΔX`, `ΔY`, `Distance` 값을 Scene View에 표시한다.

## 대안 및 예외 흐름

- 1a. 선택된 Platform이 정확히 두 개가 아니면 시스템은 거리 오버레이를 표시하지 않는다.

## 인수 조건

- [ ] Platform 두 개 선택 시 Scene View에 측정 선이 표시된다.
- [ ] Platform 두 개 선택 시 `ΔX`, `ΔY`, `Distance`가 표시된다.
- [ ] 선택이 두 Platform이 아니면 측정 정보가 표시되지 않는다.

## 구현 메모

- `PlatformMeasurementUtility`가 두 발판의 TopCenter 기준 `ΔX`, `ΔY`, `Distance`를 계산한다.
- `PlatformSceneOverlay`가 선택된 두 Platform을 A와 B로 표시하고 측정 선과 값을 Scene View에 렌더링한다.
- Unity 내장 Roslyn 컴파일러로 전체 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류는 없고 기존 런타임 직렬화 필드 관련 경고 2건만 발생했다.

## 미결 질문

- 없음.
