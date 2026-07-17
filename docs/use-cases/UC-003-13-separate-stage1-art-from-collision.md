# UC-003-13: Stage1 아트와 충돌 구조 분리

- 상태: Verified
- 마지막 갱신일: 2026-07-17

## 목적

맵 제작자가 여러 Platform으로 조립한 Stage1 충돌 구조와 최종 스프라이트 디자인을 독립적으로 배치하여, 하나의 아트가 여러 Collider를 자연스럽게 덮도록 제작한다.

## 액터

- 맵 제작자
- 아트 디자이너
- 개발자

## 사전 조건

- `Assets/Scenes/MVPJumpScene.unity`에 `Stage1/Collision`, `BackgroundDecor`, `ForegroundDecor`, `Lighting` 계층이 존재한다.
- 기존 Stage1 Platform의 Transform과 Collider가 현재 게임 플레이를 구성한다.

## 트리거

- 맵 제작자가 Platform 단위 외형 대신 구역과 구조물 단위의 Stage1 스프라이트 디자인을 적용한다.

## 기본 흐름

1. 시스템은 `Stage1/Collision`을 게임 플레이 충돌 전용 계층으로 유지한다.
2. 시스템은 `Stage1/ForegroundDecor` 아래에 최종 아트를 위한 `StageArt`와 기존 외형 참고용 `CollisionGuides`를 준비한다.
3. 시스템은 현재 보이는 Platform 외형을 Collider와 부모 관계가 없는 `CollisionGuides`에 복제해 마이그레이션 직전 화면을 유지한다.
4. 시스템은 Stage1의 모든 Platform을 충돌 전용 모드로 바꾸어 `Collision` 아래 Renderer를 표시하지 않는다.
5. 아트 디자이너는 `StageArt` 아래에서 Collider 개수와 무관하게 하나 이상의 스프라이트를 구역 단위로 배치한다.
6. 맵 제작자는 최종 아트가 적용된 구역의 `CollisionGuides`를 숨기고 Scene View의 Collider 표시와 플레이 테스트로 시각 경계와 실제 충돌 경계를 확인한다.
7. 시스템은 분리 전후 기존 Platform Transform과 모든 Collider 설정 및 월드 Bounds를 동일하게 유지한다.

## 대안 및 예외 흐름

- 2a. `StageArt` 또는 `CollisionGuides`가 이미 있으면 시스템은 중복 생성하지 않고 기존 계층을 재사용한다.
- 3a. 일반 삼각형의 절차 생성 Mesh 또는 직각 삼각형의 절차 생성 Sprite는 독립 아트 계층에서도 재로드 후 유지되는 에셋으로 복제한다.
- 4a. Stage1 밖에서 사용하는 Platform 프리팹과 Map Builder 생성 Platform은 기존처럼 `Visual`을 표시한다.
- 5a. 배경 전용 아트는 `BackgroundDecor`, 플레이어보다 앞에서 보여야 하는 장식은 `ForegroundDecor`의 별도 구역에 배치한다.
- 6a. 아직 최종 아트가 없는 구역은 `CollisionGuides`를 켜 둬 기존 임시 외형으로 플레이할 수 있다.

## 인수 조건

- [x] `Stage1/ForegroundDecor` 아래에 `StageArt`와 `CollisionGuides`가 각각 하나씩 존재한다.
- [x] Stage1의 활성 Renderer는 `Collision`이 아니라 장식 계층 아래에 존재한다.
- [x] `CollisionGuides`는 기존 Stage1 Platform 외형과 같은 월드 위치, 회전, 크기, 색상과 정렬 상태로 표시된다.
- [x] Stage1의 모든 Platform은 충돌 전용 모드이고, Stage1 밖의 Platform 기본값은 시각 요소 표시 상태다.
- [x] 기존 Platform Transform과 Collider 개수, 타입, 직렬화 값 및 월드 Bounds가 분리 전후 동일하다.
- [x] 일반 삼각형과 직각 삼각형 참고 외형도 씬을 다시 불러온 뒤 유지된다.
- [x] `StageArt`에 배치한 스프라이트에는 Collider나 `Platform2D`가 자동으로 추가되지 않는다.
- [x] Stage1 아트 제작 주의사항 문서가 제공된다.

## 구현 메모

- 작업 지침은 `docs/stage1-art-authoring-guide.md`에 기록한다.
- `Platform2D`에 기본값이 활성화된 `renderVisuals` 설정을 추가하고 Stage1 인스턴스 100개에만 비활성 값을 저장했다.
- `Stage1/Collision` 아래 기존 Renderer 146개의 외형을 독립 `CollisionGuides` 계층으로 복제했다.
- 절차 생성 직각 삼각형 Sprite 2개와 일반 삼각형 Mesh 2개를 `Assets/Art/Stage1/CollisionGuides`에 영구 에셋으로 저장했다.
- 마이그레이션 중 `Collision` 전체 Collider 171개의 직렬화 값과 월드 Bounds, 직접 구조물 Transform이 변경되지 않음을 검사했다.
- 씬을 다시 연 뒤 Platform 100개가 충돌 전용이고, Collider 171개와 Guide Renderer 146개가 유지되며, 가이드의 Sprite와 Mesh가 모두 영구 에셋을 참조함을 확인했다.
- Preview Scene에서 새 Platform은 기존처럼 Visual 표시가 활성화되는 기본값임을 확인했다.
- Unity 6000.3.1f1 스크립트 컴파일을 완료했으며 최종 컴파일 오류가 발생하지 않았다.

## 미결 질문

- 없음.
