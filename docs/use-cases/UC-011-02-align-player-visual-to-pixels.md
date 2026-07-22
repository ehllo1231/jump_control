# UC-011-02: 플레이어 시각 요소 픽셀 정렬

- 상태: Verified
- 마지막 갱신일: 2026-07-22

## 목적

플레이어가 이동하거나 점프할 때 물리 판정을 변경하지 않고 주인공 스프라이트만 화면 픽셀 격자에 정렬하여 픽셀 흔들림과 흐릿한 이동을 줄인다.

## 액터

- 플레이어
- 개발자

## 사전 조건

- Player 루트에 `Rigidbody2D`, `BoxCollider2D`, `PlayerVisual`이 있어야 한다.
- 표시용 `SpriteRenderer`는 Player 루트와 분리된 `Visual` 자식에 있어야 한다.
- 활성 Orthographic Camera가 플레이어를 렌더링해야 한다.

## 트리거

- 플레이어가 게임을 실행하고 Player가 이동하거나 점프한다.

## 기본 흐름

1. 시스템은 Rigidbody2D와 Player 루트를 기존 물리 계산 결과에 따라 이동시킨다.
2. 시스템은 현재 물리 크기, 시각 배율, 시각 오프셋과 방향으로 `Visual`의 기본 위치와 크기를 계산한다.
3. 시스템은 활성 카메라의 화면 픽셀 한 칸에 해당하는 월드 간격을 구한다.
4. 시스템은 `Visual`의 월드 X, Y 위치만 픽셀 간격의 배수로 반올림한다.
5. 시스템은 Player 루트, Rigidbody2D, BoxCollider2D와 시각 요소의 Z 위치를 변경하지 않는다.
6. 플레이어는 물리 판정이 유지된 픽셀 정렬 주인공으로 게임을 플레이한다.

## 대안 및 예외 흐름

- 3a. 활성 카메라가 없거나 Orthographic Camera가 아니면 시스템은 픽셀 위치 보정을 건너뛰고 기본 시각 정렬만 유지한다.
- 3b. 활성 카메라에 `SimpleCameraFollow`가 있으면 시스템은 카메라가 계산한 픽셀 격자 간격을 그대로 사용한다.
- 4a. 플레이어 시각 픽셀 스냅 옵션이 꺼져 있으면 시스템은 `Visual` 위치를 반올림하지 않는다.
- 4b. Edit Mode에서는 저장된 프리팹 및 씬 배치를 변경하지 않도록 런타임 픽셀 위치 보정을 적용하지 않는다.

## 인수 조건

- [x] `1920 × 1080`의 기본 카메라 설정에서 `Visual`의 월드 X, Y 위치는 `1/80` 월드 단위의 배수다.
- [x] 픽셀 정렬 전후 Player 루트와 Rigidbody2D 위치가 변경되지 않는다.
- [x] 픽셀 정렬 전후 BoxCollider2D의 offset과 size가 변경되지 않는다.
- [x] 픽셀 정렬은 `Visual`의 Z 위치, 시각 크기, 좌우반전과 사용자 X/Y 오프셋을 보존한다.
- [x] 실행 중 해상도가 바뀌면 카메라의 새 픽셀 격자 간격으로 `Visual`이 다시 정렬된다.
- [x] 카메라가 없거나 픽셀 스냅이 비활성화되면 기존 시각 정렬 결과가 유지된다.
- [x] 주인공 PNG는 Point 필터, mipmap 비활성화, 비등방성 필터 0, Clamp, 무압축, Full Rect 설정으로 가져온다.

## 구현 메모

- `PlayerVisual`은 기본 시각 정렬을 마친 뒤 Play Mode의 LateUpdate에서 `Visual` 자식 위치만 픽셀 격자에 맞춘다.
- `SimpleCameraFollow`가 연결된 카메라에서는 카메라의 `WorldUnitsPerScreenPixel`을 사용하고, 일반 Orthographic Camera에서는 카메라 높이와 픽셀 높이로 격자 간격을 계산한다.
- `PlayerVisual`의 실행 순서를 200으로 지정해 기본 실행 순서의 카메라 LateUpdate가 픽셀 배율과 위치를 먼저 갱신하도록 했다.
- Player 프리팹의 `Pixel Snap Visual`을 기본 활성화했다. Rigidbody2D의 Interpolate 설정과 Player 루트 및 Collider는 변경하지 않았다.
- `PlayerSpriteAutoImporter`가 Point, mipmap 비활성화, Clamp, 무압축, Full Rect와 함께 비등방성 필터를 0으로 설정한다.
- Unity 6000.3.1f1의 현재 프로젝트 컴파일 응답 파일로 런타임 및 에디터 어셈블리 컴파일을 통과했다.
- Unity 6000.3.1f1 임시 프로젝트에서 주인공 PNG 임포트 설정과 1080p `1/80` 격자 정렬을 자동 검증했다. 픽셀 스냅 전후 Player 루트, Rigidbody2D, Collider, 시각 스케일과 Z 값이 같은 것도 함께 확인했다.

## 미결 질문

- 없음.
