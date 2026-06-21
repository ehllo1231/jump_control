# UC-002-01: 점프 튜닝 대상 선택

- 상태: Implemented
- 마지막 갱신일: 2026-06-21

## 목적

개발자 또는 테스터가 `Jump Tuning` 창에서 어떤 Player의 점프 설정을 편집할지 확인하고 선택한다.

## 액터

- 개발자
- 테스터

## 사전 조건

- Unity Editor에서 프로젝트가 열려 있어야 한다.
- 씬 Player 또는 `Assets/Prefabs/Player.prefab` 중 하나 이상을 사용할 수 있어야 한다.

## 트리거

- 개발자 또는 테스터가 `Tools > Jump Timing > Jump Tuning`을 연다.

## 기본 흐름

1. 시스템은 선택된 오브젝트에 `PlayerController`가 있는지 확인한다.
2. 없으면 현재 씬의 `PlayerController`를 찾는다.
3. 없으면 `Assets/Prefabs/Player.prefab`을 찾는다.
4. 시스템은 찾은 대상을 튜닝 창에 표시한다.
5. 사용자는 표시된 대상의 튜닝 값을 편집할 수 있다.

## 대안 및 예외 흐름

- 1a. 선택된 오브젝트의 부모나 자식에 `PlayerController`가 있으면 해당 Player를 대상으로 사용한다.
- 3a. 씬 Player와 Player 프리팹을 모두 찾지 못하면 시스템은 유효한 대상이 없다는 안내와 대상 찾기 버튼을 표시한다.

## 인수 조건

- [ ] `Tools > Jump Timing > Jump Tuning` 메뉴에서 전용 튜닝 창을 열 수 있다.
- [ ] 선택된 Player, 씬 Player, Player 프리팹 순서로 튜닝 대상이 자동 선택된다.
- [ ] 선택된 튜닝 대상이 창에 표시된다.
- [ ] 유효한 대상이 없으면 안내 메시지가 표시된다.

## 구현 메모

- `JumpTuningWindow`가 선택된 Player, 현재 씬 Player, Player 프리팹 순서로 대상을 자동 탐색한다.
- 대상 전환 버튼으로 씬 Player 또는 Player 프리팹을 다시 찾을 수 있다.
- Unity 내장 Roslyn 컴파일러로 전체 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류는 없고 기존 런타임 직렬화 필드 관련 경고 2건만 발생했다.

## 미결 질문

- 없음.
