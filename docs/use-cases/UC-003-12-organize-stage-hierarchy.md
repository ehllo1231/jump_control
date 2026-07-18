# UC-003-12: Stage1 구조물 계층 정리

- 상태: Superseded
- 마지막 갱신일: 2026-07-17

## 목적

맵 제작자가 Stage1의 충돌 구조물, 장식, 조명을 역할별 계층에서 쉽게 찾을 수 있게 정리하면서 기존 게임 플레이와 충돌 판정을 그대로 유지한다.

> Stage1의 Platform별 Visual 구조는 [UC-003-13](./UC-003-13-separate-stage1-art-from-collision.md)의 독립 아트 계층으로 대체되었다.

## 액터

- 맵 제작자
- 개발자

## 사전 조건

- `Assets/Scenes/MVPJumpScene.unity`에 플레이어가 상호작용하는 기존 구조물과 2D Collider가 배치되어 있다.

## 트리거

- 맵 제작자 또는 개발자가 Stage1 씬 계층을 역할별로 정리한다.

## 기본 흐름

1. 시스템은 씬에 `Stage1` 루트 계층을 준비한다.
2. 시스템은 `Stage1` 바로 아래에 `Collision`, `BackgroundDecor`, `ForegroundDecor`, `Lighting` 계층을 준비한다.
3. 시스템은 플레이어를 제외한 기존 충돌 구조물을 `Collision` 아래로 이동한다.
4. 시스템은 각 충돌 구조물 바로 아래에 `Visual` 자식 오브젝트를 하나만 유지한다.
5. Platform 구조물은 루트에 `Platform2D`와 2D Collider를 유지하고, `SpriteRenderer`를 `Visual` 자식에 둔다.
6. 일반 삼각형 Platform의 `Triangle Visual`은 `Visual` 아래에서 관리한다.
7. 맵 제작자가 Platform 루트를 이동하면 시각 요소와 충돌 요소가 같은 Transform을 따라 함께 이동한다.
8. 시스템은 기존 배경 장식을 `BackgroundDecor` 아래에서 관리할 수 있게 정리한다.
9. 시스템은 계층 변경 전후 구조물의 월드 Transform, SpriteRenderer 설정, 모든 2D Collider 설정 및 월드 위치를 동일하게 유지한다.

## 대안 및 예외 흐름

- 1a. `Stage1` 계층이 이미 있으면 시스템은 기존 계층을 재사용한다.
- 2a. 필요한 역할 계층이 이미 있으면 시스템은 중복 생성하지 않고 기존 계층을 재사용한다.
- 4a. 구조물에 `Visual` 자식이 이미 있으면 시스템은 중복 생성하지 않는다.
- 5a. 기존 Platform 루트에 `SpriteRenderer`가 있으면 같은 컴포넌트 식별자와 직렬화 값을 유지한 채 `Visual`로 이전한다.

## 인수 조건

- [x] `Stage1` 바로 아래에 `Collision`, `BackgroundDecor`, `ForegroundDecor`, `Lighting`이 각각 하나씩 존재한다.
- [x] 플레이어를 제외하고 2D Collider를 포함하는 모든 기존 구조물은 `Collision`의 직접 자식이다.
- [x] 각 충돌 구조물에는 `Visual` 직접 자식이 하나씩 존재한다.
- [x] 모든 Platform 루트에는 `Platform2D`와 기존 2D Collider가 유지되고 `SpriteRenderer`는 존재하지 않는다.
- [x] 모든 Platform의 직접 자식 `Visual`에는 기존 `SpriteRenderer`가 존재한다.
- [x] 일반 삼각형 Platform의 `Triangle Visual`은 해당 Platform의 `Visual` 아래에 존재한다.
- [x] Platform 루트를 이동하면 `Visual`과 Collider가 함께 이동한다.
- [x] Platform 프리팹으로 새 구조물을 생성해도 같은 계층 구조를 사용한다.
- [x] 기존 구조물의 월드 위치, 회전, 스케일은 계층 정리 전후 동일하다.
- [x] 기존 2D Collider의 개수, 타입, 활성 상태, Trigger 설정, 직렬화 데이터와 월드 Bounds는 계층 정리 전후 동일하다.
- [x] 기존 Platform의 스프라이트, 색상, 크기, 활성 상태, 머티리얼과 정렬 설정은 계층 정리 전후 동일하다.
- [x] 플레이어와 카메라의 계층 및 Transform은 변경되지 않는다.

## 구현 메모

- `Assets/Scenes/MVPJumpScene.unity`에 `Stage1`과 네 역할 계층을 생성했다.
- 2D Collider를 포함하는 기존 구조물 106개를 `Collision` 직접 자식으로 정리하고, 각 구조물에 빈 `Visual` 직접 자식을 하나씩 추가했다.
- 기존 `Demon Castle Interior` 배경 장식을 `BackgroundDecor` 아래로 이동했다.
- Unity Editor 검증에서 전체 Collider 172개의 타입, 직렬화 데이터, 월드 Transform, 월드 Bounds가 변경 전후 동일함을 확인했다.
- 같은 검증에서 Player와 Main Camera의 부모 및 월드 Transform이 변경되지 않았음을 확인했다.
- 씬 파일에 직접 직렬화된 Collider 블록 55개의 정렬 SHA-256이 변경 전후 동일함을 추가로 확인했다.
- Platform 렌더러 분리 작업은 기존 프리팹 `SpriteRenderer`의 fileID를 유지해 씬 인스턴스 오버라이드를 보존하는 방식으로 진행한다.
- Platform 프리팹의 루트에는 BoxCollider2D를 유지하고 직접 자식 `Visual`로 기존 SpriteRenderer를 이전했다.
- 현재 씬의 Platform 100개와 루트 Collider 125개를 검사하고, 중복된 빈 `Visual` 50개를 제거했다.
- Unity Editor 마이그레이션 검증에서 Platform 루트 Transform, SpriteRenderer 직렬화 값과 월드 Bounds, 모든 루트 Collider 직렬화 값과 월드 Bounds가 작업 전후 동일함을 확인했다.
- Unity 6000.3.1f1 스크립트 컴파일을 완료했으며 C# 오류가 발생하지 않았다.

## 미결 질문

- 없음.
