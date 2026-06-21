# UC-003-05: Play Mode 생성 Platform 유지

- 상태: Implemented
- 마지막 갱신일: 2026-06-21

## 목적

맵 제작자가 Play Mode 중 Map Builder로 만든 Platform을 게임 정지 후에도 유지한다.

## 액터

- 맵 제작자
- 개발자

## 사전 조건

- Unity Editor가 Play Mode여야 한다.
- Map Builder 창이 열려 있어야 한다.
- `Assets/Prefabs/Platform.prefab`을 사용할 수 있어야 한다.

## 트리거

- 사용자가 Play Mode 중 Map Builder의 `Create Platform` 버튼을 누른다.

## 기본 흐름

1. 시스템은 Play Mode 중 생성된 Platform을 추적한다.
2. 사용자가 Play Mode를 종료한다.
3. 시스템은 종료 직전에 생성된 Platform의 위치, 회전, 크기, 색상 스냅샷을 저장한다.
4. 시스템은 Edit Mode 복귀 후 열린 씬에 같은 Platform을 다시 생성한다.
5. 사용자가 다시 Play Mode를 실행해도 생성한 Platform이 남아 있다.

## 대안 및 예외 흐름

- 4a. 원래 씬을 찾지 못하면 시스템은 활성 씬에 Platform을 다시 생성한다.
- 4b. Platform 프리팹을 찾지 못하면 시스템은 경고를 표시하고 적용을 중단한다.

## 인수 조건

- [ ] Play Mode 중 Map Builder로 생성한 Platform은 게임을 정지하고 다시 실행해도 사라지지 않는다.
- [ ] 유지된 Platform은 위치, 회전, 크기, 색상을 보존한다.
- [ ] 유지된 Platform은 Edit Mode 씬 변경으로 기록된다.

## 구현 메모

- `MapBuilderWindow.CreatePlatform`이 Play Mode 생성 Platform을 `MapBuilderPlayModePersistence`에 등록한다.
- `MapBuilderPlayModePersistence`가 Play Mode 종료 직전에 스냅샷을 저장하고 Edit Mode 복귀 후 열린 씬에 다시 생성한다.
- 이 기능은 Unity의 기본 Play Mode 임시 변경 동작을 보완하는 안전장치다.
- Unity 내장 Roslyn 컴파일러로 전체 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류는 없고 기존 런타임 직렬화 필드 관련 경고 2건만 발생했으며 Editor 스크립트 경고는 발생하지 않았다.

## 미결 질문

- 없음.
