# UC-003-10: Platform 회전 조정

- 상태: Implemented
- 마지막 갱신일: 2026-06-22

## 목적

맵 제작자가 사각형 및 삼각형 Platform을 회전해 경사 발판과 다양한 지형 구성을 만든다.

## 액터

- 맵 제작자
- 개발자
- 테스터

## 사전 조건

- Unity Editor에서 맵 씬이 열려 있어야 한다.
- 씬에 `Platform2D`가 있는 Platform이 있어야 한다.

## 트리거

- 사용자가 Platform Inspector 또는 Map Builder에서 Platform의 회전값을 변경한다.

## 기본 흐름

1. 사용자가 사각형 또는 삼각형 Platform을 선택한다.
2. 시스템은 현재 Platform의 2D 회전값을 `Rotation Z`로 표시한다.
3. 사용자가 `Rotation Z` 값을 입력하거나 빠른 회전 버튼을 누른다.
4. 시스템은 Platform Transform의 Z 회전을 변경한다.
5. 시스템은 사각형의 `BoxCollider2D` 또는 삼각형의 `PolygonCollider2D`가 회전된 Platform Transform을 따라가게 한다.
6. 사용자가 Map Builder에서 새 Platform을 생성하면 시스템은 `New Platform Rotation` 값을 새 Platform에 적용한다.

## 대안 및 예외 흐름

- 3a. 사용자가 Reset 버튼을 누르면 시스템은 Platform의 Z 회전을 `0`으로 설정한다.
- 6a. Play Mode 중 생성한 Platform은 게임 정지 후 Edit Mode에 복원될 때 회전값을 유지한다.

## 인수 조건

- [ ] 사각형 Platform의 `Rotation Z` 값을 Inspector에서 변경할 수 있다.
- [ ] 삼각형 Platform의 `Rotation Z` 값을 Inspector에서 변경할 수 있다.
- [ ] Map Builder에서 새 Platform의 기본 회전값을 지정할 수 있다.
- [ ] 회전 후 시각 요소와 충돌체가 같은 Transform 회전을 따른다.
- [ ] Play Mode 중 생성한 Platform은 Edit Mode 복귀 후에도 회전값을 유지한다.

## 구현 메모

- `Platform2D`에 `RotationDegrees`와 `SetRotationDegrees`를 추가해 2D Platform의 Z 회전을 한 경로로 적용한다.
- `Platform2DEditor`에 `Rotation Z` 숫자 입력과 `-15`, `Reset`, `+15` 빠른 회전 버튼을 추가했다.
- `MapBuilderWindow`에 `New Platform Rotation` 입력을 추가해 새로 생성하는 사각형 및 삼각형 Platform에 기본 회전값을 적용한다.
- `MapBuilderWindow`의 선택 Platform 영역에서도 `Rotation Z`와 빠른 회전 버튼으로 선택한 Platform을 회전할 수 있다.
- 회전은 Platform Transform에 적용되므로 사각형 `BoxCollider2D`, 삼각형 `PolygonCollider2D`, 삼각형 자식 시각 오브젝트가 같은 회전을 따른다.
- Play Mode 생성 Platform 스냅샷은 기존처럼 Transform rotation을 저장하므로 회전된 Platform도 Edit Mode 복귀 후 유지된다.
- 2026-06-22 회전 조정 기능 추가 후 Unity C# 컴파일러로 런타임 스크립트와 Editor 스크립트 컴파일을 확인했다. 오류와 경고 출력은 없었다.

## 미결 질문

- 없음.
