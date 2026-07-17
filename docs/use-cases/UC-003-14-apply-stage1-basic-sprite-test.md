# UC-003-14: Stage1 기본 스프라이트 디자인 테스트 적용

- 상태: Verified
- 마지막 갱신일: 2026-07-17

## 목적

아트 디자이너가 Stage1의 실제 충돌 구조를 변경하지 않고, 독립된 `StageArt` 계층에서 기본 성벽 스프라이트 조합의 시각 품질과 방향성을 시험한다.

## 액터

- 아트 디자이너
- 맵 제작자
- 개발자

## 사전 조건

- `Assets/Scenes/MVPJumpScene.unity`에 `Stage1/Collision`과 `Stage1/ForegroundDecor/StageArt`, `CollisionGuides`가 존재한다.
- `Assets/Art/Stage1/basic_sprite`에 디자인 테스트용 스프라이트 97개가 임포트되어 있다.
- 기존 `Collision`의 Transform과 Collider가 현재 게임 플레이를 구성한다.

## 트리거

- 아트 디자이너가 Stage1 맨 위 큰 방을 제외한 구역에 기본 성벽 디자인을 시험 적용한다.

## 기본 흐름

1. 시스템은 `CollisionGuides`를 형태와 위치 참고용으로만 읽는다.
2. 시스템은 `StageArt` 아래에 디자인 테스트 전용 단일 루트를 준비한다.
3. 시스템은 기본 벽돌, 금 간 벽돌, 발판, 코너, 기둥 계열만 선택한다.
4. 시스템은 서로 겹친 회색 가이드 모양을 그대로 복제하지 않고, 구조물의 노출 면이 하나의 벽이나 발판처럼 이어지도록 스프라이트를 배치한다.
5. 시스템은 새 SpriteRenderer의 표시 영역이 실제 Collider 범위를 벗어나지 않게 배치한다.
6. 시스템은 맨 위 큰 방에 해당하는 구역을 테스트 적용 대상에서 제외한다.
7. 시스템은 새 아트를 Player보다 뒤, CollisionGuides보다 앞에 표시한다.
8. 시스템은 적용 전후 `Collision`의 계층, Transform, Collider 설정과 월드 Bounds가 동일한지 검증한다.
9. 아트 디자이너는 테스트를 되돌릴 때 테스트 전용 단일 루트만 삭제한다.

## 대안 및 예외 흐름

- 2a. 같은 이름의 테스트 루트가 이미 있으면 기존 루트만 교체하여 중복 아트를 만들지 않는다.
- 4a. 회전·곡선·삼각형처럼 선택한 스프라이트로 안전하게 덮기 어려운 충돌 영역은 억지로 채우지 않고 이번 테스트에서 제외할 수 있다.
- 5a. 스프라이트의 불투명 영역을 Collider 내부에 안전하게 맞출 수 없으면 해당 조각을 생성하지 않는다.
- 6a. 최상단 큰 방의 경계는 Collider 분포에서 확인한 명시적 월드 Y 기준으로 기록한다.
- 7a. 기존 CollisionGuides는 수정하지 않고, 정렬 순서 차이로 테스트 아트를 확인한다.

## 인수 조건

- [x] 새 오브젝트는 모두 `Stage1/ForegroundDecor/StageArt/BasicSpriteTest` 아래에만 존재한다.
- [x] 테스트 루트를 삭제하는 한 번의 작업으로 새 디자인을 제거할 수 있다.
- [x] `Collision`, 기존 Collider와 기존 Transform은 작업 전후 동일하다.
- [x] `CollisionGuides`의 오브젝트와 컴포넌트는 변경되지 않는다.
- [x] 사용 스프라이트는 기본 벽돌 `01–10`, 금 간 벽돌 `11–20`, 발판 `41–58`, 짧은 벽/끝단 `59–68`, 코너 `69–78`, 기둥 `79–91` 중에서만 선택한다.
- [x] 문양·창문·빈 벽 장식 `21–40`과 경사 대형 장식 `92–97`은 사용하지 않는다.
- [x] 맨 위 큰 방에는 새 SpriteRenderer가 생성되지 않는다.
- [x] 새 SpriteRenderer에는 Collider2D나 `Platform2D`가 없다.
- [x] 새 아트는 Default Sorting Layer, Order in Layer 1–2로 설정되어 Order 10인 Player보다 뒤에 표시된다.
- [x] 새 아트의 전체 SpriteRenderer Bounds는 하나 이상의 실제 Collider 영역 안에 있다.
- [x] 사용한 스프라이트와 적용한 구역이 문서에 기록된다.

## 구현 메모

- 테스트 루트 이름은 `BasicSpriteTest`로 고정한다.
- 기본 벽돌과 금 간 벽돌은 큰 면 채움, 발판은 수평 노출 면, 코너와 기둥은 구조가 꺾이거나 길게 이어지는 구역의 시각적 연결에 사용한다.
- 배치 도구는 적용 직전에 `Collision`과 `CollisionGuides` 상태를 스냅샷하고, 생성 직후 같은 값을 다시 비교한다.
- 상단 큰 방은 Collider 분포에서 왼쪽 벽이 시작되는 월드 `Y=159.63`을 기준으로 확인하고, 안전 여유를 둔 `Y=159.5` 이상을 제외했다.
- 가이드와 겹치는 BoxCollider 81개를 읽어 포함 도형을 제거하고 같은 두께로 이어지는 구간을 병합한 벽 면 80개를 만들었다.
- 큰 PolygonCollider 경사 2곳은 작은 사각 셀을 그대로 나열하지 않고, Collider 내부에 완전히 들어가는 수평 벽 단 21개로 재구성했다.
- `BasicSpriteTest` 아래 3개 높이 구역에 SpriteRenderer 162개를 생성했다. 새 Collider2D와 `Platform2D`는 0개다.
- 적용 직전과 직후 `Collision` 및 `CollisionGuides` 전체 Transform·컴포넌트 직렬화 값·Bounds 스냅샷이 동일함을 확인했다.
- 씬 재로드 후 Collider 171개, CollisionGuides Renderer 144개, 테스트 SpriteRenderer 162개가 유지되었다.
- 재로드 검증에서 모든 테스트 SpriteRenderer가 Default Sorting Layer/Order 1–2이고, Player Order 10보다 뒤이며, 최고 Bounds `Y=159.472`로 제외 경계를 넘지 않음을 확인했다.
- 사용 내역과 롤백 방법은 `docs/stage1-basic-sprite-test-summary.md`에 기록했다.

## 미결 질문

- 없음.
