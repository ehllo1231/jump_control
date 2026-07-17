# UC-003-14: Stage1 기본 스프라이트 디자인 테스트 적용

- 상태: Superseded
- 마지막 갱신일: 2026-07-17

## 목적

아트 디자이너가 Stage1의 실제 충돌 구조를 변경하지 않고, 독립된 `StageArt` 계층에서 기본 성벽 스프라이트 조합을 시험한다.

> 이 디자인 테스트 결과는 사용자 피드백에 따라 전체 롤백되었다. Stage1은 [UC-003-13](./UC-003-13-separate-stage1-art-from-collision.md)의 Collision/StageArt 분리 상태로 돌아갔다.

## 액터

- 아트 디자이너
- 맵 제작자
- 개발자

## 사전 조건

- `Assets/Scenes/MVPJumpScene.unity`에 `Stage1/Collision`과 `Stage1/ForegroundDecor/StageArt`, `CollisionGuides`가 존재한다.

## 트리거

- 아트 디자이너가 Stage1 맨 위 큰 방을 제외한 구역에 기본 성벽 디자인을 시험 적용한다.

## 기본 흐름

1. 시스템은 `CollisionGuides`를 참고해 `StageArt` 아래에 테스트 아트를 배치한다.
2. 사용자는 적용된 디자인을 검토한다.
3. 사용자가 결과를 승인하지 않으면 시스템은 테스트 전용 루트와 관련 도구를 제거한다.
4. 시스템은 테스트에서 변경한 에셋 임포트 설정을 원래 값으로 복구한다.
5. 시스템은 씬을 다시 열어 Collision과 CollisionGuides가 유지되고 테스트 아트가 제거됐는지 확인한다.

## 대안 및 예외 흐름

- 3a. 테스트 전용 루트가 이미 없으면 중복 삭제하지 않는다.
- 4a. 임포트 설정이 원래 값이면 재변경하지 않는다.

## 인수 조건

- [x] `Stage1/ForegroundDecor/StageArt/BasicSpriteTest`가 존재하지 않는다.
- [x] `StageArt`에 테스트 SpriteRenderer가 남아 있지 않다.
- [x] 기존 Collision Collider 171개가 유지된다.
- [x] 기존 CollisionGuides Renderer 144개가 유지된다.
- [x] 이번 테스트에서 변경한 24개 PNG의 임포트 설정이 원래 값으로 복구된다.
- [x] 테스트 배치 도구와 결과 요약 문서가 제거된다.

## 구현 메모

- `BasicSpriteTest` 루트를 삭제하고 씬을 저장했다.
- 테스트에 사용한 24개 PNG를 `Texture Type: Default`, `Sprite Mode: 0`, `MipMap: On`, `Sprite Mesh: Tight`, `Alpha Is Transparency: Off`로 복구했다.
- 씬 재로드 후 `StageArt` 자식 0개, Collision Collider 171개, CollisionGuides Renderer 144개를 확인했다.
- 기존 Collision, Collider와 CollisionGuides에는 롤백 과정에서 변경을 가하지 않았다.
- 테스트 배치 도구와 테스트 결과 요약 문서를 제거했다.

## 미결 질문

- 없음.
