# UC-003-07: 기본 Platform 색상 일관성 유지

- 상태: Implemented
- 마지막 갱신일: 2026-07-17

## 목적

맵 제작자가 기본 맵과 새로 생성되는 Platform을 같은 색상 기준으로 확인한다.

## 액터

- 맵 제작자
- 개발자

## 사전 조건

- 기본 Platform 프리팹 또는 MVP 맵 생성 코드가 사용된다.

## 트리거

- 시스템이 기본 Platform을 생성하거나 기존 MVP 맵 Platform을 표시한다.

## 기본 흐름

1. 시스템은 기본 Platform 색상을 `RGB(0.22, 0.24, 0.27)`로 사용한다.
2. 시스템은 MVP 씬의 기존 Platform 색상 override도 같은 색상으로 유지한다.
3. 시스템은 삼각형 Platform도 사각 Platform과 같은 색상 기준으로 표시한다.
4. 맵 제작자는 Scene View에서 Platform을 일관된 색상으로 확인한다.

## 대안 및 예외 흐름

- 없음.

## 인수 조건

- [ ] 기본 맵의 모든 발판은 동일한 플랫폼 색상을 사용한다.
- [ ] `MVPSceneBuilder`가 생성하는 Platform은 같은 색상 기준을 사용한다.
- [ ] 삼각형 Platform도 사각 Platform과 같은 색상으로 표시된다.

## 구현 메모

- 기본 발판 색은 `RGB(0.22, 0.24, 0.27)`로 통일했다.
- `MVPSceneBuilder`와 `MVPJumpScene`의 기존 발판 색 override도 같은 색을 사용한다.
- 삼각형 Platform은 `Visual/Triangle Visual`의 MeshRenderer가 `Visual`의 SpriteRenderer 색상과 텍스처를 직접 받도록 `MaterialPropertyBlock`을 설정해 사각 Platform과 같은 색상 기준으로 표시한다.
- 2026-06-22 삼각형 MeshRenderer 투명 표시 수정 후 Unity C# 컴파일러로 런타임 스크립트와 Editor 스크립트 컴파일을 확인했다. 오류와 경고 출력은 없었다.
- 2026-06-22 삼각형 시각 메시를 자식 오브젝트로 분리한 뒤 Unity C# 컴파일러로 런타임 스크립트와 Editor 스크립트 컴파일을 확인했다. 오류와 경고 출력은 없었다.

## 미결 질문

- 없음.
