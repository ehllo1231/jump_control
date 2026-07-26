# UC-003-17: Stage1 재디자인을 위한 맵 초기화

- 상태: Verified
- 마지막 갱신일: 2026-07-26

## 목적

맵 제작자가 기존 Stage1 작업을 복구 가능한 상태로 보존하면서, 바닥 하나와 역할별 계층만 남긴 빈 맵에서 새로운 디자인을 시작한다.

## 액터

- 맵 제작자
- 아트 디자이너
- 개발자

## 사전 조건

- `Assets/Scenes/MVPJumpScene.unity`에 기존 Stage1 충돌 구조, 배경, 가이드와 디자인 파츠가 존재한다.
- 현재 씬을 복구할 수 있는 백업 씬과 Git 기준점이 준비되어 있어야 한다.

## 트리거

- 맵 제작자가 기존 Stage1 디자인을 폐기하고 새로운 플레이어 비율과 아트 해상도에 맞춘 맵 제작을 시작한다.

## 기본 흐름

1. 시스템은 현재 Stage1 씬을 별도 백업 씬과 Git 기준점으로 보존한다.
2. 시스템은 `Stage1/Collision`에서 `Floor`를 제외한 모든 충돌 구조물을 제거한다.
3. 시스템은 Stage1 밖에 남아 있는 맵 구조물과 루트 디자인 스프라이트를 제거한다.
4. 시스템은 `BackgroundDecor`와 `StageArt`를 비운다.
5. 시스템은 `CollisionGuides`에서 `Floor`에 대응하는 가이드만 유지한다.
6. 시스템은 `Stage1/Collision`, `BackgroundDecor`, `ForegroundDecor/StageArt`, `ForegroundDecor/CollisionGuides`, `Lighting` 계층을 유지한다.
7. 시스템은 기존 Stage1 전용 아트, 그레이박스, 배경, 생성 가이드 에셋과 재적용 도구를 제거한다.
8. 맵 제작자는 빈 Stage1에서 새로운 맵 디자인을 시작한다.

## 대안 및 예외 흐름

- 1a. 백업 씬 또는 Git 기준점을 만들 수 없으면 시스템은 초기화를 시작하지 않는다.
- 2a. `Floor`를 하나만 정확히 식별할 수 없으면 시스템은 씬을 저장하지 않고 오류를 보고한다.
- 5a. `Floor` 가이드를 하나만 정확히 식별할 수 없으면 시스템은 씬을 저장하지 않고 오류를 보고한다.
- 6a. 필수 역할 계층이 없거나 중복되어 있으면 시스템은 씬을 저장하지 않고 오류를 보고한다.
- 7a. Player, Camera, 공용 Platform·Obstacle 프리팹과 런타임 시스템은 Stage1 전용 결과물로 간주하지 않고 보존한다.

## 인수 조건

- [x] `Stage1/Collision`의 직접 자식은 `Floor` 하나뿐이다.
- [x] `BackgroundDecor`와 `StageArt`는 비어 있다.
- [x] `CollisionGuides`에는 `Floor` 가이드 하나만 존재한다.
- [x] `Stage1` 밖에 `Platform2D` 맵 구조물과 루트 디자인 Sprite가 남아 있지 않다.
- [x] `Stage1`, `Collision`, `BackgroundDecor`, `ForegroundDecor`, `StageArt`, `CollisionGuides`, `Lighting` 계층이 유지된다.
- [x] Player, Main Camera, Gameplay와 Floor의 Transform 및 핵심 직렬화 값이 변경되지 않는다.
- [x] 메인 씬의 `.meta` GUID가 변경되지 않는다.
- [x] 기존 Stage1 전용 아트 에셋과 재적용 메뉴가 제거된다.
- [x] Unity Editor 재컴파일과 씬 재로드 후 Missing 참조 또는 컴파일 오류가 없다.
- [x] Play Mode에서 Player가 남겨 둔 Floor에 정상적으로 착지한다.

## 구현 메모

- 초기화 전 기준점 커밋은 `2cc8197`이며, 현재 씬 사본은 `Assets/_Recovery/MapSceneBackups/MVPJumpScene-20260726-before-stage1-redesign.unity`에 보존한다.
- Unity Editor 초기화 도구가 Collision 구조물 105개, BackgroundDecor 자식 1개, StageArt 자식 1개, CollisionGuide 105개와 Stage1 밖 맵 오브젝트 4개를 제거했다.
- 초기화 후 씬 루트는 Player, Main Camera, Gameplay, Stage1 네 개이며, Prefab Instance는 Player와 `Collision/Floor` 두 개만 남아 있다.
- 기준점 커밋과 직렬화 비교에서 Player 프리팹 오버라이드, Main Camera 컴포넌트, Gameplay Transform, Floor 프리팹 오버라이드와 Floor 가이드 하위 트리가 동일함을 확인했다. 씬 GUID `a5d4c5f5fd739664baf099a466db7845`도 유지됐다.
- 이전 배경·그레이박스·디자인 파츠·생성 가이드·전용 머티리얼과 셰이더를 제거하고 해당 Unity 폴더 경로는 유지했다. 이전 디자인을 다시 생성하는 세 메뉴와 임시 초기화 도구도 최종 결과에서 제거했다.
- Unity 6000.3.1f1에서 최종 Editor 어셈블리 재컴파일을 통과했다. Play Mode 검증에서는 Dynamic Rigidbody Player를 Floor 위에서 낙하시켜 `GroundChecker`가 착지를 판정함을 확인했다.

## 미결 질문

- 없음.
