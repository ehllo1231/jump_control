# UC-003-16: Stage1 분할 디자인 이미지 적용

- 상태: Verified
- 마지막 갱신일: 2026-07-17

## 목적

아트 디자이너가 Stage1 전체 캡처를 세로로 나눈 디자인 이미지를 실제 충돌 구조와 독립된 시각 요소로 시험 적용한다.

## 액터

- 아트 디자이너
- 맵 제작자
- 개발자

## 사전 조건

- `Assets/Scenes/MVPJumpScene.unity`에 `Stage1/Collision`과 `Stage1/ForegroundDecor/StageArt`가 존재한다.
- 전체 Stage1 캡처를 5등분한 첫 번째 구조 참고 이미지 `structure1.png`와 디자인 이미지 `design1.png`가 `Assets/Art/Design/stage1`에 존재한다.

## 트리거

- 아트 디자이너가 5등분된 Stage1의 첫 번째 구간 디자인을 게임에서 확인하려 한다.

## 기본 흐름

1. 시스템은 `structure1.png`와 `design1.png`의 공통 구조 윤곽을 기준으로 디자인 이미지의 표시 범위를 계산한다.
2. 시스템은 `design1.png`를 Sprite 에셋으로 가져온다.
3. 시스템은 `Stage1/ForegroundDecor/StageArt` 아래에 첫 번째 구간 전용 테스트 루트를 생성한다.
4. 시스템은 디자인 SpriteRenderer를 첫 번째 캡처 구간의 월드 범위에 맞춰 배치한다.
5. 시스템은 디자인을 플레이어보다 뒤에 표시하고, 기존 회색 `CollisionGuides`와 선택적으로 비교할 수 있게 한다.
6. 맵 제작자는 Scene View 또는 Play Mode에서 디자인과 실제 충돌 경계의 대응을 확인한다.

## 대안 및 예외 흐름

- 1a. 구조 이미지와 디자인 이미지의 해상도가 다르면 시스템은 각 이미지의 전체 가로·세로 비율을 기준으로 비례 정렬한다.
- 3a. 같은 이름의 테스트 루트가 이미 있으면 중복 생성하지 않고 해당 루트의 디자인 오브젝트를 갱신한다.
- 5a. 디자인 검토가 끝나지 않은 구역은 `CollisionGuides`를 유지해 실제 충돌 구조를 계속 비교할 수 있다.
- 6a. 결과를 되돌릴 때는 첫 번째 구간 전용 테스트 루트만 제거하여 Collision과 다른 StageArt를 보존한다.

## 인수 조건

- [x] 새 디자인은 `Stage1/ForegroundDecor/StageArt` 아래에만 생성된다.
- [x] 디자인 오브젝트에는 Collider와 `Platform2D`가 없다.
- [x] 기존 `Stage1/Collision`의 Transform과 Collider 직렬화 값은 변경되지 않는다.
- [x] 5등분된 첫 번째 구간의 구조 윤곽과 디자인 윤곽이 월드 공간에서 대응한다.
- [x] 디자인은 플레이어보다 뒤에 렌더링된다.
- [x] 테스트 전용 루트를 비활성화하거나 제거하면 디자인만 쉽게 롤백할 수 있다.

## 구현 메모

- `structure1.png`의 중앙 세로벽, 좌우 장벽, 바닥 픽셀 경계를 현재 `CollisionGuides`의 월드 Bounds와 대응하여 적용 범위를 `x -31.485..29.925`, `y -6.974..38.835`로 계산했다.
- `design1.png`를 100 PPU, Full Rect, Mipmap 비활성, Bilinear, Clamp, 비압축 단일 Sprite로 가져왔다.
- `Stage1/ForegroundDecor/StageArt/DesignTest_Section_01_of_05/Design1`에 SpriteRenderer 하나를 배치했다. Collider와 `Platform2D`는 추가하지 않았다.
- 디자인의 어두운 합성 배경만 투명하게 만드는 `JumpTiming/Stage Design Background Key` 셰이더와 조절 가능한 머티리얼을 추가했다.
- 디자인의 Order in Layer를 5로 설정하여 Order 10인 플레이어보다 뒤에 표시했다.
- `Tools/Jump Timing/Stage1 Design/Apply Section 1 Test`와 `Rollback Section 1 Test` 메뉴로 같은 배치를 다시 적용하거나 테스트 루트만 제거할 수 있게 했다.
- 별도 임시 프로젝트의 Unity 6000.3.1f1에서 스크립트 및 셰이더 임포트와 컴파일을 통과했다.
- Unity가 생성한 2160×1611 프리뷰에서 석재 벽·발판이 구조 윤곽에 대응하고 검은 디자인 배경이 기존 배경과 플레이어를 가리지 않음을 확인했다.
- 적용 도구가 작업 전후 `Stage1/Collision` 전체 Transform 및 Collider 직렬화 상태를 비교했으며 변경이 없음을 확인했다.

## 미결 질문

- 이후 구간도 같은 합성 이미지 방식으로 적용할지는 첫 번째 구간의 게임 내 검토 결과에 따라 결정한다.
