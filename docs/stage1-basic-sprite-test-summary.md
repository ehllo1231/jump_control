# Stage1 기본 스프라이트 디자인 테스트 요약

## 적용 위치

- 루트: `Stage1/ForegroundDecor/StageArt/BasicSpriteTest`
- 하단 던전: 월드 `Y=-3–55`, SpriteRenderer 50개
- 중단 던전: 월드 `Y=55–110`, SpriteRenderer 66개
- 상단 던전: 월드 `Y=110–159.5`, SpriteRenderer 46개
- 제외 구역: 맨 위 큰 방, 월드 `Y>=159.5`

겹치거나 포함된 BoxCollider 사각형은 그대로 복제하지 않고 80개 연속 벽 면으로 정리했다. 큰 삼각 경사 2곳은 Collider 안에 들어가는 수평 벽 단 21개로 구성했다. 작은 뿔처럼 안전한 사각 아트를 넣기 어려운 PolygonCollider는 CollisionGuides 상태로 남겼다.

## 사용 스프라이트

- 기본 벽돌: `sprite_01`, `sprite_03`, `sprite_06`, `sprite_08`
- 금 간 벽돌: `sprite_11`
- 발판: `sprite_41`, `sprite_43`, `sprite_45`, `sprite_47`
- 짧은 벽/끝단: `sprite_59`, `sprite_60`, `sprite_63`, `sprite_65`, `sprite_66`
- 코너: `sprite_70`, `sprite_72`, `sprite_76`
- 기둥: `sprite_84`, `sprite_86`, `sprite_88`, `sprite_90`

문양·창문 계열 `21–40`과 경사 대형 장식 `92–97`은 사용하지 않았다.

## 표시와 검증

- 기본 면은 Default Sorting Layer / Order in Layer `1`, 표면 장식은 `2`다.
- Player는 같은 Sorting Layer / Order `10`이므로 모든 테스트 아트보다 앞에 표시된다.
- 새 SpriteRenderer 162개의 전체 Bounds를 실제 Collider 내부 표본으로 검사했다.
- 테스트 계층에는 Collider2D와 `Platform2D`가 없다.
- 적용 전후 Collision과 CollisionGuides 스냅샷은 동일했다.
- 씬 재로드 후 Collider 171개, Guide Renderer 144개, Test Renderer 162개를 다시 확인했다.

## 롤백

`StageArt/BasicSpriteTest` 루트 하나를 삭제하면 테스트 디자인 전체가 제거된다. 또는 Unity 메뉴 `Tools > Jump Timing > Stage1 Basic Sprite Test > Remove Test Art`를 사용한다.

PNG 임포트 설정은 Unity에서 Sprite로 사용할 수 있게 바뀐 상태로 유지된다. 이는 아트 에셋의 사용 준비 설정이며 게임 플레이나 Collider에는 영향을 주지 않는다.
