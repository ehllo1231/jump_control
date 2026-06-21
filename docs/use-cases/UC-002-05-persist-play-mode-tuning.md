# UC-002-05: Play Mode 튜닝 값 유지

- 상태: Implemented
- 마지막 갱신일: 2026-06-21

## 목적

개발자 또는 테스터가 Play Mode 중 `Jump Tuning` 창에서 변경한 값을 게임 정지 후에도 유지한다.

## 액터

- 개발자
- 테스터

## 사전 조건

- Unity Editor가 Play Mode여야 한다.
- `Jump Tuning` 창에 유효한 `PlayerController` 대상이 선택되어 있어야 한다.

## 트리거

- 사용자가 Play Mode 중 `Jump Tuning` 창에서 값을 변경한다.

## 기본 흐름

1. 사용자가 Play Mode 중 튜닝 값을 변경한다.
2. 시스템은 변경된 `JumpTuningConfig` 스냅샷을 저장한다.
3. 사용자가 Play Mode를 종료한다.
4. 시스템은 Edit Mode 복귀 후 같은 Player에 저장한 튜닝 값을 다시 적용한다.
5. 사용자가 다시 Play Mode를 실행하면 변경한 튜닝 값이 유지되어 있다.

## 대안 및 예외 흐름

- 4a. 같은 씬 Player를 찾지 못하면 시스템은 현재 씬 Player, Player 프리팹 순서로 마지막 튜닝 값을 적용한다.

## 인수 조건

- [ ] Play Mode 중 `Jump Tuning` 창에서 변경한 값은 게임을 정지하고 다시 실행해도 유지된다.
- [ ] 같은 씬 Player를 찾지 못하면 fallback 대상에 마지막 튜닝 값이 적용된다.

## 구현 메모

- `JumpTuningPlayModePersistence`가 `JumpTuningConfig`를 `SessionState`에 JSON 스냅샷으로 보관한다.
- Edit Mode 복귀 후 같은 `GlobalObjectId`의 `PlayerController`에 스냅샷을 다시 적용한다.
- 같은 씬 Player를 찾지 못하면 현재 씬 Player, Player 프리팹 순서로 마지막 튜닝 값을 적용한다.
- Unity 내장 Roslyn 컴파일러로 전체 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류는 없고 기존 런타임 직렬화 필드 관련 경고 2건만 발생했다.
- Play Mode에서 Tools 창 값을 직접 변경하는 수동 검증은 수행하지 않았으므로 상태는 `Verified`가 아닌 `Implemented`로 유지한다.

## 미결 질문

- 없음.
