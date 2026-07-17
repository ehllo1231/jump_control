# UC-003-01: Platform 생성 및 크기 조정

- 상태: Implemented
- 마지막 갱신일: 2026-07-17

## 목적

맵 제작자가 Map Builder에서 새 Platform을 생성하고 Scene View와 Inspector로 위치와 크기를 조정한다.

## 액터

- 맵 제작자
- 개발자

## 사전 조건

- Unity Editor에서 맵 씬이 열려 있어야 한다.
- `Assets/Prefabs/Platform.prefab`을 사용할 수 있어야 한다.

## 트리거

- 사용자가 `Tools > Jump Timing > Map Builder`를 열고 `Create Platform` 버튼을 누른다.

## 기본 흐름

1. 시스템은 활성 씬과 Platform 프리팹을 확인한다.
2. 시스템은 Scene View 중심 또는 선택된 Platform 위에 새 Platform을 생성한다.
3. 시스템은 생성된 Platform을 선택한다.
4. 사용자는 Scene View 이동 도구로 Platform 위치를 조정한다.
5. 사용자는 Inspector에서 Width와 Height를 수정한다.
6. 시스템은 `Visual` 자식의 SpriteRenderer와 Platform 루트의 BoxCollider2D 크기를 같은 Width와 Height로 갱신한다.
7. 사용자가 SpriteRenderer 또는 Scene View Rect Tool로 보이는 Platform 도형 크기를 직접 바꾼다.
8. 시스템은 변경된 도형 크기를 Platform 크기와 BoxCollider2D 크기에 반영한다.

## 대안 및 예외 흐름

- 1a. Platform 프리팹을 찾지 못하면 시스템은 생성하지 않고 해결 방법을 안내한다.
- 1b. 열린 활성 씬이 없으면 시스템은 생성하지 않고 안내한다.
- 2a. Scene View가 없으면 시스템은 월드 원점에 Platform을 생성한다.
- 5a. Width 또는 Height가 `0.1`보다 작으면 시스템은 `0.1`로 제한한다.
- 6a. 크기 적용은 Unity Editor의 `OnValidate` 제한에 걸리지 않아야 하며, 생성 중 콘솔 오류 없이 동기화되어야 한다.
- 8a. 보이는 도형 크기가 `0.1`보다 작으면 시스템은 `0.1`로 제한한다.

## 인수 조건

- [ ] Map Builder에서 Platform을 버튼으로 생성할 수 있다.
- [ ] 생성된 Platform을 Scene View 이동 도구로 자유롭게 이동할 수 있다.
- [ ] Platform 선택 시 Inspector에서 Width와 Height를 수정할 수 있다.
- [ ] Width와 Height 변경 시 SpriteRenderer와 BoxCollider2D 크기가 함께 변경된다.
- [ ] SpriteRenderer 또는 Scene View Rect Tool로 보이는 도형 크기를 바꿔도 BoxCollider2D 크기가 함께 변경된다.
- [ ] Platform 생성 및 크기 변경 시 Sprite tiling 또는 `OnValidate` 관련 오류가 발생하지 않는다.
- [ ] 생성된 Platform은 루트에 `Platform2D`와 BoxCollider2D, 직접 자식 `Visual`에 SpriteRenderer를 가진다.

## 구현 메모

- `MapBuilderWindow.CreatePlatform`이 Platform 프리팹을 활성 씬에 생성한다.
- `Platform2D`가 Width, Height, TopCenter, WorldBounds를 관리하고 SpriteRenderer와 BoxCollider2D 크기를 동기화한다.
- `Platform2D.VisualRenderer`가 직접 자식 `Visual`의 SpriteRenderer를 제공하고 루트 Collider와 크기 동기화를 유지한다.
- `Platform2D.LateUpdate`가 SpriteRenderer의 보이는 크기 변경을 감지해 Width, Height, BoxCollider2D.size와 BoxCollider2D.offset을 다시 맞춘다.
- Scene View Rect Tool 또는 SpriteRenderer size를 직접 수정한 경우에도 보이는 Platform 도형과 충돌체 크기가 같게 유지된다.
- `Platform2DEditor`가 Width와 Height 입력 필드를 제공한다.
- `MVPWhiteSquare` 스프라이트를 Full Rect mesh로 설정했다.
- `Platform2D.OnValidate`의 크기 적용을 Editor delay call로 미뤄 생성 및 실행 중 SpriteRenderer 크기 변경 오류가 발생하지 않게 했다.
- Unity 내장 Roslyn 컴파일러로 전체 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류는 없고 기존 런타임 직렬화 필드 관련 경고 2건만 발생했으며 Editor 스크립트 경고는 발생하지 않았다.

## 미결 질문

- 없음.
