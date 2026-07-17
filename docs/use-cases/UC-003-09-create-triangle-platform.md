# UC-003-09: 삼각형 Platform 생성

- 상태: Implemented
- 마지막 갱신일: 2026-07-17

## 목적

맵 제작자가 Map Builder에서 사각형뿐 아니라 삼각형 벽돌을 생성해 경사 충돌, 벽 반동, 장식적인 지형 구성을 만든다.

## 액터

- 맵 제작자
- 개발자
- 테스터

## 사전 조건

- Unity Editor에서 맵 씬이 열려 있어야 한다.
- `Tools > Jump Timing > Map Builder` 창을 사용할 수 있어야 한다.

## 트리거

- 사용자가 Map Builder에서 새 Platform 도형을 삼각형으로 선택하고 생성한다.

## 기본 흐름

1. 사용자가 Map Builder에서 새 Platform 도형을 선택한다.
2. 사용자가 새 Platform의 Width와 Height를 입력한다.
3. 사용자가 생성 버튼을 누른다.
4. 시스템은 선택한 도형이 삼각형이면 `Visual/Triangle Visual`의 삼각형 메시와 Platform 루트의 `PolygonCollider2D`를 가진 Platform을 생성한다.
5. 시스템은 삼각형 시각 메시와 `PolygonCollider2D`가 같은 꼭지점 좌표를 사용하도록 갱신한다.
6. 사용자는 Scene View에서 보이는 삼각형을 클릭해 부모 Platform을 선택한다.
7. 사용자는 Scene View에서 선택된 삼각형 Platform을 드래그해 위치를 변경한다.
8. 사용자는 Scene View에서 삼각형 꼭지점을 드래그해 원하는 삼각형 모양으로 변경한다.
9. 사용자는 Scene View에서 삼각형 외곽 크기 핸들을 드래그해 전체 Width와 Height를 조정한다.
10. 시스템은 꼭지점 또는 전체 크기 변경 결과를 Platform에 저장하고 Width, Height를 삼각형 외곽 크기에 맞춰 갱신한다.
11. 시스템은 선택한 도형이 사각형이면 기존처럼 SpriteRenderer와 `BoxCollider2D`를 가진 Platform을 생성한다.
12. 생성된 Platform은 Scene View에서 이동할 수 있고 Inspector에서 도형, Width, Height와 삼각형 꼭지점을 수정할 수 있다.
13. 사용자가 씬을 저장하고 Unity를 다시 열거나 게임을 실행해도 생성한 삼각형 Platform이 사라지지 않는다.

## 대안 및 예외 흐름

- 1a. 사용자가 기존 사각형 도형을 선택하면 시스템은 기존 사각 Platform 생성 흐름을 유지한다.
- 2a. Width 또는 Height가 최소 크기보다 작으면 시스템은 최소 크기로 보정한다.
- 4a. 삼각형 Platform의 방향을 바꾸면 시스템은 메시와 `PolygonCollider2D` 경로를 새 방향 프리셋에 맞게 갱신한다.
- 4b. `Visual`에 `SpriteRenderer`가 있어도 시스템은 삼각형 메시 렌더러를 `Visual/Triangle Visual`에 생성해 Unity Renderer 컴포넌트 충돌을 피한다.
- 6a. Scene View 클릭으로 `Triangle Visual` 자식 오브젝트가 선택되면 시스템은 선택 대상을 부모 Platform으로 전환한다.
- 8a. 사용자가 삼각형을 거의 일직선이나 0 크기에 가깝게 만들면 시스템은 유효한 삼각형 면적이 유지되는 범위에서만 꼭지점 변경을 적용한다.
- 9a. 전체 크기 조정 결과 Width 또는 Height가 최소 크기보다 작으면 시스템은 최소 크기로 보정한다.

## 인수 조건

- [ ] Map Builder에서 삼각형 Platform을 생성할 수 있다.
- [ ] 삼각형 Platform은 눈에 보이는 삼각형 형태로 표시된다.
- [ ] 삼각형 Platform은 사각 Platform과 같은 색상 기준으로 표시된다.
- [ ] 삼각형 Platform은 눈에 보이는 메시와 완전히 같은 꼭지점 좌표를 쓰는 `PolygonCollider2D`로 충돌을 제공한다.
- [ ] Scene View에서 보이는 삼각형을 클릭하면 부모 `Platform2D` GameObject가 선택된다.
- [ ] Scene View에서 선택된 삼각형 Platform을 드래그해 위치를 변경할 수 있다.
- [ ] Scene View에서 삼각형 꼭지점을 드래그해 임의의 삼각형 모양으로 변경할 수 있다.
- [ ] Scene View에서 삼각형 외곽 크기 핸들을 드래그해 전체 Width와 Height를 조정할 수 있다.
- [ ] 꼭지점 드래그 후 Inspector의 Width와 Height는 변경된 삼각형 외곽 크기를 반영한다.
- [ ] 전체 크기 조정 후 삼각형 메시, `PolygonCollider2D`, Inspector Width와 Height가 같은 외곽 크기를 반영한다.
- [ ] 기존 사각 Platform 생성과 크기 조정은 계속 동작한다.
- [ ] Inspector에서 Platform 도형, Width, Height와 삼각형 꼭지점을 수정할 수 있다.
- [ ] Play Mode에서 생성한 삼각형 Platform은 Edit Mode 복귀 후에도 도형, 크기와 꼭지점 모양이 유지된다.
- [ ] 저장된 씬을 다시 열거나 게임을 실행해도 삼각형 Platform이 유지된다.
- [ ] 삼각형 Platform 복구 시 `Triangle Visual` 자식 오브젝트와 `PolygonCollider2D` 데이터가 함께 복원된다.
- [ ] `Triangle Visual`은 Platform의 직접 자식 `Visual` 아래에 존재한다.

## 구현 메모

- `Platform2D`에 `PlatformShape2D`를 추가해 `Rectangle`, `Triangle Up`, `Triangle Right`, `Triangle Down`, `Triangle Left`를 선택할 수 있게 했다.
- 사각형 Platform은 `Visual`의 SpriteRenderer와 루트의 `BoxCollider2D`를 사용한다.
- 삼각형 Platform은 세 꼭지점 좌표를 직렬화하고, `Visual/Triangle Visual`의 MeshRenderer/MeshFilter와 루트 `PolygonCollider2D`가 같은 꼭지점 배열을 사용한다.
- Unity는 같은 GameObject에 `SpriteRenderer`와 `MeshRenderer`를 함께 둘 수 없으므로, 삼각형 MeshRenderer/MeshFilter는 `Visual/Triangle Visual`에 생성한다.
- 삼각형 MeshRenderer는 SpriteRenderer의 텍스처와 색상을 `MaterialPropertyBlock`으로 명시적으로 받아 사각 Platform과 같은 색상 기준으로 표시된다.
- `Platform2DEditor`에서 Shape, Width, Height와 삼각형 꼭지점 좌표를 편집할 수 있다.
- Scene View에서 삼각형 꼭지점 핸들을 드래그해 임의의 삼각형 모양으로 변경할 수 있다.
- 삼각형 꼭지점 변경 후 Width와 Height는 실제 꼭지점 외곽 크기에 맞춰 갱신된다.
- 삼각형 꼭지점이 거의 일직선이거나 최소 크기보다 작아지는 변경은 적용하지 않는다.
- `PlatformSelectionRedirector`가 Scene View 클릭 등으로 `Triangle Visual` 자식이 선택되면 선택 대상을 부모 `Platform2D` GameObject로 바꾼다.
- `Platform2DEditor`가 선택된 삼각형 Platform의 외곽 중심 이동 핸들과 외곽 크기 조정 핸들을 Scene View에 표시한다.
- 삼각형 전체 크기 조정 핸들은 기존 꼭지점의 외곽 내 상대 위치를 유지한 채 새 외곽 크기로 스케일하고, 메시와 `PolygonCollider2D`를 같은 꼭지점으로 다시 적용한다.
- Map Builder에 `New Platform Shape` 선택 필드를 추가하고, 삼각형 선택 시 `Create Triangle Brick` 버튼으로 표시되게 했다.
- Play Mode 생성 Platform 스냅샷에 도형, 크기와 삼각형 꼭지점 정보를 포함해 삼각형 Platform도 Edit Mode 복귀 후 같은 모양으로 유지되게 했다.
- 점프 도달 후보 표시는 삼각형 도형 변경을 캐시에 반영하고, 실제 꼭지점 기준으로 수평 윗변이 있는 삼각형만 착지 후보로 취급한다.
- 2026-06-22 꼭지점 편집 기능 추가 후 Unity C# 컴파일러로 런타임 스크립트와 Editor 스크립트 컴파일을 확인했다. 오류와 경고 출력은 없었다.
- 2026-06-22 삼각형 MeshRenderer 투명 표시 수정 후 Unity C# 컴파일러로 런타임 스크립트와 Editor 스크립트 컴파일을 확인했다. 오류와 경고 출력은 없었다.
- 2026-06-22 삼각형 시각 메시를 자식 오브젝트로 분리한 뒤 Unity C# 컴파일러로 런타임 스크립트와 Editor 스크립트 컴파일을 확인했다. 오류와 경고 출력은 없었다.
- 2026-06-23 삼각형 구조물이 사라진 문제 보고에 따라 백업 씬 복구와 씬 덮어쓰기 방지 보강을 진행한다.
- 2026-06-23 복구된 `Assets/Scenes/MVPJumpScene.unity`에서 삼각형 Platform 1개(`shape: 3`, width `25.29694`, height `9.50899`), `PolygonCollider2D`, `Triangle Visual` 자식, `MeshFilter`, `MeshRenderer` 참조를 확인했다.
- 2026-06-23 Unity 6000.3.1f1 배치 모드에서 스크립트 컴파일을 확인했다. 오류 출력은 없었다.
- 2026-06-25 삼각형 Scene View 선택 리다이렉트, 이동 핸들, 전체 크기 조정 핸들을 추가한 뒤 Unity 내장 Roslyn 컴파일러로 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류 출력은 없었다.
- 2026-07-17 일반 삼각형의 `Triangle Visual`을 `Visual` 아래로 이동하고 Unity Editor에서 기존 Collider와 렌더러 값이 유지됨을 확인했다.

## 미결 질문

- 없음.
