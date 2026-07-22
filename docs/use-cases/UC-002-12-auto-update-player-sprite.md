# UC-002-12: 플레이어 이미지 파일 자동 반영

- 상태: Implemented
- 마지막 갱신일: 2026-07-22

## 목적

개발자 또는 맵 제작자가 주인공 이미지 파일만 교체해도 Player 프리팹과 열린 Scene의 주인공 시각 이미지가 자동으로 갱신된다.

## 액터

- 개발자
- 맵 제작자

## 사전 조건

- Player 프리팹이 `Assets/Prefabs/Player.prefab`에 있어야 한다.
- Player 시각 요소가 `SpriteRenderer`를 통해 표시되어야 한다.
- 교체 대상 이미지는 `Assets/IncomingImages/Player/stand/demonking_right.png` 경로의 PNG 파일이어야 한다.

## 트리거

- 사용자가 `Assets/IncomingImages/Player/stand/demonking_right.png` 파일을 다른 PNG로 덮어쓴다.
- 사용자가 `Tools > Jump Timing > Refresh Player Sprite` 메뉴를 실행한다.

## 기본 흐름

1. 사용자가 교체 대상 PNG 파일을 덮어쓴다.
2. Unity가 해당 PNG를 import한다.
3. 시스템은 해당 PNG를 `Sprite` 타입, Single import, Full Rect mesh, 투명 배경, Point 필터, mipmap 및 비등방성 필터 비활성화, Clamp와 무압축 설정으로 맞춘다.
4. 시스템은 PNG 높이를 기준으로 pixels per unit 값을 설정해 스프라이트 세로 크기를 1유닛으로 맞춘다.
5. 시스템은 Player 프리팹의 주인공 `SpriteRenderer`에 새 Sprite를 연결한다.
6. 시스템은 열려 있는 Scene의 Player 시각 요소에도 새 Sprite를 연결한다.
7. 시스템은 Player의 충돌체, 점프 판정, 시각 오프셋 설정을 변경하지 않는다.

## 대안 및 예외 흐름

- 1a. 사용자가 `.meta` 파일을 삭제하거나 Unity가 새 GUID를 만들면 시스템은 파일 경로 기준으로 Player 프리팹의 Sprite 참조를 다시 연결한다.
- 2a. 교체 대상 경로에 파일이 없으면 시스템은 경고를 남기고 Player 프리팹을 변경하지 않는다.
- 3a. PNG 크기를 읽을 수 없으면 시스템은 pixels per unit 자동 갱신을 건너뛰고 import 가능한 설정만 적용한다.
- 6a. 열린 Scene에 Player가 없으면 시스템은 Player 프리팹만 갱신한다.

## 인수 조건

- [ ] `Assets/IncomingImages/Player/stand/demonking_right.png`를 다른 PNG로 덮어쓰면 Unity import 후 Player 프리팹의 주인공 Sprite가 새 이미지로 갱신된다.
- [ ] `.meta`가 유지되지 않아도 Player 프리팹의 Sprite 참조가 교체 대상 경로의 Sprite로 다시 연결된다.
- [ ] 교체된 PNG는 Sprite 타입으로 import된다.
- [ ] 교체된 PNG는 Point 필터, mipmap 및 비등방성 필터 비활성화, Clamp, 무압축과 Full Rect로 import된다.
- [ ] 교체된 PNG의 pixels per unit은 PNG 높이와 같아져 세로 기준 1유닛으로 표시된다.
- [ ] 열린 Scene의 Player 시각 Sprite도 새 이미지로 갱신된다.
- [ ] 이미지 교체 후 Player의 `BoxCollider2D` 크기와 위치는 변경되지 않는다.

## 구현 메모

- `Assets/Editor/PlayerSpriteAutoImporter.cs`가 `AssetPostprocessor`로 `Assets/IncomingImages/Player/stand/demonking_right.png` import를 감지한다.
- `OnPreprocessTexture`에서 Sprite import 설정, mipmap 비활성화, 투명 배경, Full Rect mesh, pixels per unit 자동 설정을 적용한다.
- `OnPostprocessAllAssets`에서 import 완료 후 Player 프리팹과 열린 Scene의 Player `SpriteRenderer`를 새 Sprite로 갱신한다.
- `Tools > Jump Timing > Refresh Player Sprite` 메뉴로 같은 갱신을 수동 실행할 수 있다.
- Player 시각 정렬, 스케일, X/Y 오프셋, 좌우반전 기준축은 기존 `PlayerVisual` 로직을 그대로 사용한다.
- Unity 6000.3.1f1 임시 프로젝트에서 실제 주인공 PNG를 강제 reimport하고 Point, mipmap 비활성화, 비등방성 필터 0, Clamp, 무압축과 Full Rect 설정을 자동 검증했다.

## 미결 질문

- 없음.
