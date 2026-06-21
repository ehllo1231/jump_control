# UC-003-06: 점프 도달 후보 표시

- 상태: Implemented
- 마지막 갱신일: 2026-06-21

## 목적

맵 제작자가 Scene View에서 Player 현재 위치 기준으로 점프 도달 후보 궤적과 착지 가능한 Platform 후보를 확인한다.

## 액터

- 맵 제작자
- 개발자
- 테스터

## 사전 조건

- Unity Editor에서 맵 씬이 열려 있어야 한다.
- 씬에 `PlayerController`가 있어야 한다.
- 씬에 `Platform2D`가 있어야 한다.

## 트리거

- 사용자가 Map Builder의 `Show In Scene View` 토글 또는 `Tools > Jump Timing > Show Jump Reachability` 메뉴를 켠다.

## 기본 흐름

1. 시스템은 Player 현재 위치를 찾는다.
2. 시스템은 현재 `JumpTuningConfig`의 각도 범위와 점프 세기 범위를 읽는다.
3. 시스템은 Rigidbody2D mass, gravityScale과 `PlayerJumpMotor`의 impulse 배율을 읽는다.
4. 시스템은 Scene View에 샘플 점프 궤적을 표시한다.
5. 시스템은 하강 중 Platform 윗면과 만나는 궤적을 찾는다.
6. 시스템은 Player 폭이 들어갈 수 있는 Platform 착지 구간과 후보 지점을 강조한다.

## 대안 및 예외 흐름

- 1a. PlayerController를 찾지 못하면 시스템은 점프 도달 후보 궤적을 표시하지 않는다.
- 5a. 샘플 궤적이 Platform 윗면과 만나지 않으면 착지 후보를 강조하지 않는다.

## 인수 조건

- [ ] Scene View에서 Player 현재 위치 기준 점프 도달 후보 궤적을 확인할 수 있다.
- [ ] 현재 점프 튜닝의 각도 범위와 점프 세기 범위가 도달 후보 궤적에 반영된다.
- [ ] 샘플 궤적이 하강 중 Platform 윗면과 만나는 경우 해당 Platform이 도달 후보로 강조된다.
- [ ] Map Builder 창과 Tools 메뉴에서 표시를 켜고 끌 수 있다.

## 구현 메모

- `JumpReachabilityOverlay`가 Scene View에서 샘플 점프 궤적을 표시한다.
- 궤적 계산은 Player 현재 위치, `JumpTuningConfig`, `PlayerJumpMotor`, Rigidbody2D mass와 gravityScale을 기준으로 한다.
- 샘플 궤적이 하강 중 Platform 윗면과 만나고 Player 폭이 들어갈 수 있으면 착지 가능 구간과 후보 지점을 강조한다.
- 이 표시는 샘플 기반 제작 보조 기능이며 벽이나 천장 같은 중간 장애물 충돌까지 완전 시뮬레이션하지는 않는다.
- Unity 내장 Roslyn 컴파일러로 전체 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류는 없고 기존 런타임 직렬화 필드 관련 경고 2건만 발생했으며 Editor 스크립트 경고는 발생하지 않았다.

## 미결 질문

- 없음.
