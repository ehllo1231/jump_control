# UC-002-02: 플레이어 크기 튜닝

- 상태: Implemented
- 마지막 갱신일: 2026-06-21

## 목적

개발자 또는 테스터가 `Jump Tuning` 창에서 주인공 사각형 크기를 변경하고 충돌체와 지면 판정에 같은 크기를 반영한다.

## 액터

- 개발자
- 테스터

## 사전 조건

- `Jump Tuning` 창에 유효한 `PlayerController` 대상이 선택되어 있어야 한다.

## 트리거

- 사용자가 `Player Body > Player Square Size` 값을 변경한다.

## 기본 흐름

1. 사용자가 주인공 사각형 크기 값을 입력한다.
2. 시스템은 값을 `0.1` 이상으로 제한한다.
3. 시스템은 Player의 `BoxCollider2D.size`를 같은 크기로 변경한다.
4. 시스템은 Player 시각 요소의 크기를 같은 크기로 변경한다.
5. 시스템은 지면 판정이 변경된 충돌체 하단과 폭을 기준으로 동작하게 한다.

## 대안 및 예외 흐름

- 2a. 사용자가 `0.1`보다 작은 값을 입력하면 시스템은 `0.1`로 보정한다.

## 인수 조건

- [ ] `Jump Tuning` 창에서 주인공 사각형 크기를 변경할 수 있다.
- [ ] 크기 변경 시 스프라이트와 `BoxCollider2D` 크기가 함께 변경된다.
- [ ] 크기 변경 후 지면 판정 영역은 새 충돌체 하단과 폭을 따른다.
- [ ] 기본 주인공 크기는 `0.72`이고 최솟값은 `0.1`이다.

## 구현 메모

- `JumpTuningConfig.PlayerSquareSize`가 플레이어 크기 설정을 보관한다.
- `PlayerController.ApplyJumpTuning`이 `BoxCollider2D.size`와 `PlayerVisual.SetBodySize`를 갱신한다.
- `GroundChecker`는 고정된 GroundCheck 위치 대신 변경된 콜라이더의 실제 하단과 폭을 기준으로 지면을 검사한다.
- Unity 내장 Roslyn 컴파일러로 전체 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류는 없고 기존 런타임 직렬화 필드 관련 경고 2건만 발생했다.

## 미결 질문

- 없음.
