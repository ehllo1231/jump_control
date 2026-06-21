# UC-003-04: 선택 Platform에서 플레이 테스트 시작

- 상태: Implemented
- 마지막 갱신일: 2026-06-21

## 목적

맵 제작자가 선택한 Platform 위에 Player를 배치하고 즉시 플레이 테스트를 시작한다.

## 액터

- 맵 제작자
- 테스터
- 개발자

## 사전 조건

- Unity Editor에서 맵 씬이 열려 있어야 한다.
- 씬에 `PlayerController`와 `Platform2D`가 있어야 한다.

## 트리거

- 사용자가 Platform 하나를 선택하고 Map Builder에서 `Start Test From Selected Platform` 버튼을 누른다.

## 기본 흐름

1. 시스템은 선택된 Platform을 확인한다.
2. 시스템은 Player를 찾는다.
3. 시스템은 Player를 선택한 Platform 위에 배치한다.
4. 시스템은 Rigidbody2D 속도를 초기화한다.
5. Edit Mode이면 시스템은 Play Mode에 진입한다.

## 대안 및 예외 흐름

- 1a. 선택한 오브젝트가 Platform이 아니면 테스트 시작 버튼은 비활성화된다.
- 2a. PlayerController를 찾지 못하면 시스템은 Play Mode를 시작하지 않고 안내한다.
- 5a. 이미 Play Mode이면 시스템은 Play Mode를 다시 시작하지 않고 Player 위치와 속도를 즉시 초기화한다.

## 인수 조건

- [ ] 선택한 Platform 위에서 Player를 시작시키는 테스트 버튼을 사용할 수 있다.
- [ ] Edit Mode에서는 Play Mode 진입 후 Player가 선택 발판 위로 이동한다.
- [ ] Play Mode에서는 즉시 Player가 선택 발판 위로 이동한다.
- [ ] Player 재배치 시 Rigidbody2D 속도가 초기화된다.

## 구현 메모

- `MapPlaytestLauncher`가 Edit Mode에서는 Play Mode 진입 후, Play Mode에서는 즉시 Player를 선택 발판 위에 배치한다.
- `MapPlaytestLauncher`가 Rigidbody2D 속도를 초기화한다.
- Unity 내장 Roslyn 컴파일러로 전체 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류는 없고 기존 런타임 직렬화 필드 관련 경고 2건만 발생했다.
- Unity Scene View에서 전체 제작 흐름을 직접 조작하는 수동 검증은 수행하지 않았으므로 상태는 `Verified`가 아닌 `Implemented`로 유지한다.

## 미결 질문

- 없음.
