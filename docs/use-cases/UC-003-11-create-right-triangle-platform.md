# UC-003-11: 직각 삼각형 Platform 생성 및 편집

- 상태: Implemented
- 마지막 갱신일: 2026-06-25

## 목적

맵 제작자가 사각형 벽돌과 같은 단일 Platform 오브젝트 구조로 직각 삼각형 벽돌을 생성하고 Scene View에서 위치와 크기를 조정한다.

## 액터

- 맵 제작자
- 개발자
- 테스터

## 사전 조건

- Unity Editor에서 맵 씬이 열려 있어야 한다.
- `Tools > Jump Timing > Map Builder` 창을 사용할 수 있어야 한다.

## 트리거

- 사용자가 Map Builder 또는 Inspector에서 Platform 도형을 직각 삼각형으로 선택한다.

## 기본 흐름

1. 사용자가 Platform 도형 목록에서 직각 삼각형 방향을 선택한다.
2. 사용자가 새 Platform의 Width와 Height를 입력한다.
3. 사용자가 생성 버튼을 누른다.
4. 시스템은 선택한 직각 삼각형 방향에 맞춰 부모 Platform 하나에 `SpriteRenderer`와 `PolygonCollider2D`를 가진 Platform을 생성한다.
5. 시스템은 직각 모서리, 가로변, 세로변, 빗변을 가진 꼭지점 좌표를 적용한다.
6. 사용자는 Scene View에서 보이는 직각 삼각형을 클릭해 부모 Platform을 선택한다.
7. 사용자는 선택된 직각 삼각형 Platform을 Scene View에서 드래그해 위치를 변경한다.
8. 사용자는 Scene View 외곽 크기 핸들을 드래그해 Width와 Height를 조정한다.
9. 시스템은 편집 후에도 삼각형의 직각 구조를 유지하고 부모 `SpriteRenderer`, `PolygonCollider2D`, Inspector Width와 Height를 동기화한다.

## 대안 및 예외 흐름

- 1a. 사용자가 기존 사각형 또는 일반 삼각형 도형을 선택하면 시스템은 해당 도형의 기존 생성 및 편집 흐름을 유지한다.
- 2a. Width 또는 Height가 최소 크기보다 작으면 시스템은 최소 크기로 보정한다.
- 8a. 전체 크기 조정 결과 Width 또는 Height가 최소 크기보다 작으면 시스템은 최소 크기로 보정한다.

## 인수 조건

- [ ] Map Builder에서 직각 삼각형 Platform을 생성할 수 있다.
- [ ] 직각 삼각형 Platform은 사각 Platform처럼 자식 오브젝트 없이 부모 Platform GameObject 하나로 구성된다.
- [ ] 직각 삼각형 Platform은 선택한 방향에 맞는 직각 모서리와 빗변을 가진다.
- [ ] 직각 삼각형 Platform은 사각 Platform과 같은 색상 기준으로 표시된다.
- [ ] 직각 삼각형 Platform은 눈에 보이는 부모 `SpriteRenderer` 외형과 같은 꼭지점 좌표를 쓰는 `PolygonCollider2D`로 충돌을 제공한다.
- [ ] Scene View에서 보이는 직각 삼각형을 클릭하면 부모 `Platform2D` GameObject가 선택된다.
- [ ] Scene View에서 선택된 직각 삼각형 Platform을 드래그해 위치를 변경할 수 있다.
- [ ] Scene View에서 직각 삼각형 외곽 크기 핸들을 드래그해 Width와 Height를 조정할 수 있다.
- [ ] 직각 삼각형은 별도 빗변 기울기 핸들이나 자유 꼭지점 편집 핸들을 표시하지 않는다.
- [ ] 편집 후 부모 `SpriteRenderer`, `PolygonCollider2D`, Inspector Width와 Height가 같은 외곽 크기를 반영한다.
- [ ] 기존 사각 Platform과 일반 삼각형 Platform의 생성 및 편집 동작은 계속 동작한다.
- [ ] Play Mode에서 생성한 직각 삼각형 Platform은 Edit Mode 복귀 후에도 도형, 크기와 꼭지점 모양이 유지된다.

## 구현 메모

- `PlatformShape2D`에 `Right Triangle Bottom Left`, `Right Triangle Bottom Right`, `Right Triangle Top Right`, `Right Triangle Top Left`를 추가했다.
- 직각 삼각형은 0번 꼭지점을 직각 모서리, 1번 꼭지점을 가로변 끝, 2번 꼭지점을 세로변 끝으로 관리한다.
- `Platform2D`는 직각 삼각형 꼭지점과 전체 크기 조정을 직각 삼각형 방향에 맞는 외곽 경계로 보정한다.
- 직각 삼각형은 빗변 중간 핸들 없이 사각 Platform과 같은 이동 및 외곽 크기 조정 핸들만 제공하도록 수정한다.
- 직각 삼각형은 `Triangle Visual` 자식을 생성하지 않고 부모 `SpriteRenderer`에 절차 생성한 직각 삼각형 스프라이트를 적용한다.
- 직각 삼각형으로 전환된 기존 오브젝트에 `Triangle Visual` 자식이 남아 있으면 `Platform2D`가 해당 자식을 제거한다.
- 직각 삼각형의 부모 `SpriteRenderer.size` 변경을 감지해 Width, Height와 부모 `PolygonCollider2D` 경로를 동기화한다.
- 직각 삼각형은 Scene View 커스텀 노란 핸들을 표시하지 않고, 사각 Platform처럼 Unity 기본 Transform/Rect Tool 편집 흐름을 따른다.
- 직각 삼각형 스프라이트는 2048픽셀 절차 생성 텍스처와 슈퍼샘플링 알파를 사용해 빗변 계단현상을 줄인다.
- `MapBuilderWindow`는 직각 삼각형 도형 선택 시 생성 버튼을 `Create Right Triangle Brick`으로 표시한다.
- Play Mode 생성 Platform 스냅샷은 기존 삼각형 꼭지점 저장 구조를 재사용하므로 직각 삼각형 도형과 꼭지점도 Edit Mode 복귀 후 유지된다.
- 2026-06-25 Unity 내장 Roslyn 컴파일러로 런타임 스크립트 컴파일을 확인했다. 오류 출력은 없었다.
- 2026-06-25 새 런타임 참조를 반영한 임시 Editor response 파일로 Unity 내장 Roslyn Editor 스크립트 컴파일을 확인했다. 오류 출력은 없었다.
- 2026-06-25 직각 삼각형을 부모 단일 오브젝트 구조로 바꾸고 빗변 기울기 핸들을 제거한 뒤 Unity 내장 Roslyn 컴파일러로 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류 출력은 없었다.
- 2026-06-25 직각 삼각형 Scene View 커스텀 핸들을 제거하고 2048픽셀 슈퍼샘플링 스프라이트로 빗변 표시를 개선한 뒤 Unity 내장 Roslyn 컴파일러로 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류 출력은 없었다.

## 미결 질문

- 없음.
