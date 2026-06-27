# UC-002-02: 플레이어 크기 튜닝

- 상태: Implemented
- 마지막 갱신일: 2026-06-27

## 목적

개발자 또는 테스터가 `Jump Tuning` 창에서 주인공 물리 크기와 시각 크기 배율을 조정한다.

## 액터

- 개발자
- 테스터

## 사전 조건

- `Jump Tuning` 창에 유효한 `PlayerController` 대상이 선택되어 있어야 한다.

## 트리거

- 사용자가 `Player Body > Player Square Size` 또는 `Player Visual Scale` 값을 변경한다.

## 기본 흐름

1. 사용자가 주인공 물리 크기 값을 입력한다.
2. 시스템은 값을 `0.1` 이상으로 제한한다.
3. 시스템은 Player의 `BoxCollider2D.size`를 같은 크기로 변경한다.
4. 사용자는 필요하면 주인공 시각 크기 배율을 입력한다.
5. 시스템은 시각 크기 배율을 `0.1` 이상으로 제한한다.
6. 시스템은 Player 시각 요소의 크기를 `Player Square Size * Player Visual Scale`로 변경한다.
7. 시스템은 시각 요소의 하단이 Player 충돌체 하단에 맞춰지도록 표시 위치를 조정한다.
8. 시스템은 지면 판정이 변경된 충돌체 하단과 폭을 기준으로 동작하게 한다.

## 대안 및 예외 흐름

- 2a. 사용자가 `0.1`보다 작은 값을 입력하면 시스템은 `0.1`로 보정한다.
- 5a. 시각 크기 배율이 `1`보다 크거나 작아도 시스템은 `BoxCollider2D.size`를 바꾸지 않는다.

## 인수 조건

- [ ] `Jump Tuning` 창에서 주인공 물리 크기를 변경할 수 있다.
- [ ] `Jump Tuning` 창에서 주인공 시각 크기 배율을 변경할 수 있다.
- [ ] 물리 크기 변경 시 `BoxCollider2D` 크기가 변경된다.
- [ ] 시각 크기 배율 변경 시 스프라이트 크기만 변경되고 `BoxCollider2D` 크기는 유지된다.
- [ ] 시각 크기 배율을 키워도 Player 스프라이트 하단은 충돌체 하단 기준에 맞춰지고 위쪽으로 커진다.
- [ ] 크기 변경 후 지면 판정 영역은 새 충돌체 하단과 폭을 따른다.
- [ ] 기본 주인공 크기는 `0.72`이고 최솟값은 `0.1`이다.
- [ ] 기본 시각 크기 배율은 `1.8`이고 최솟값은 `0.1`이다.

## 구현 메모

- `JumpTuningConfig.PlayerSquareSize`가 플레이어 크기 설정을 보관한다.
- `JumpTuningConfig.PlayerVisualScale`이 플레이어 시각 크기 배율 설정을 보관한다.
- `Jump Tuning` 창의 `Player Body` 섹션에서 `Player Square Size`와 `Player Visual Scale`을 함께 수정할 수 있다.
- `PlayerController.ApplyJumpTuning`이 `BoxCollider2D.size`는 물리 크기로, `PlayerVisual.SetBodySize`는 물리 크기와 시각 배율을 곱한 크기로 갱신한다.
- `PlayerVisual`은 스프라이트의 로컬 하단을 기준으로 위치를 보정해 시각 배율을 키워도 발이 아래로 파고들지 않고 위쪽으로 커지게 한다.
- `GroundChecker`는 고정된 GroundCheck 위치 대신 변경된 콜라이더의 실제 하단과 폭을 기준으로 지면을 검사한다.
- Unity 내장 Roslyn 컴파일러로 전체 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류는 없고 기존 런타임 직렬화 필드 관련 경고 2건만 발생했다.

## 미결 질문

- 없음.
