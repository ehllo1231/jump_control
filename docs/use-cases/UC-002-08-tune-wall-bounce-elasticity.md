# UC-002-08: 벽 충돌 탄성 및 세로 속도 튜닝

- 상태: Implemented
- 마지막 갱신일: 2026-06-22

## 목적

개발자 또는 테스터가 `Jump Tuning` 창에서 Player가 벽에 부딪혔을 때의 가로 탄성과 세로 속도 처리 방식을 조정한다.

## 액터

- 개발자
- 테스터

## 사전 조건

- `Jump Tuning` 창에 유효한 `PlayerController` 대상이 선택되어 있어야 한다.
- Player에 `Rigidbody2D`와 `PlayerJumpMotor`가 있어야 한다.

## 트리거

- 사용자가 `Jump Tuning` 창에서 `Wall Bounce Elasticity` 또는 `Wall Bounce Vertical Mode` 값을 변경한다.

## 기본 흐름

1. 사용자가 벽 충돌 탄성 값과 세로 속도 처리 방식을 입력한다.
2. 시스템은 값을 `0` 이상 `2` 이하로 제한한다.
3. 시스템은 Player가 가로 방향 충돌 법선을 가진 표면에 부딪혔을 때 현재 충돌 속도와 탄성 값을 기준으로 반대 방향 가로 속도를 적용한다.
4. 시스템은 Player가 같은 벽 접촉 상태에 머물러도 짧은 간격으로 벽 반동을 재시도해 벽에 붙은 상태가 지속되지 않게 한다.
5. 시스템은 충돌 직후 가로 속도가 너무 작아 빠져나오지 못하는 경우에도 탄성 값에 비례한 최소 이탈 속도를 적용한다.
6. 세로 속도 처리 방식이 `Preserve Pre-Collision Y Velocity`이면 시스템은 벽 충돌 직전의 세로 속도를 유지한다.
7. 세로 속도 처리 방식이 `Use Current Collision Y Velocity`이면 시스템은 기존 방식처럼 충돌 처리 후 현재 세로 속도를 유지한다.
8. 시스템은 바닥이나 천장처럼 세로 방향 충돌에는 벽 탄성을 적용하지 않는다.
9. Play Mode 중 값을 변경하면 게임 정지 후에도 변경값이 유지된다.

## 대안 및 예외 흐름

- 2a. 사용자가 범위를 벗어난 값을 입력하면 시스템은 가장 가까운 유효 범위 값으로 보정한다.
- 6a. 세로 속도 처리 방식 값이 유효하지 않으면 시스템은 `Preserve Pre-Collision Y Velocity`를 사용한다.

## 인수 조건

- [ ] `Jump Tuning` 창에서 벽 충돌 탄성 값을 변경할 수 있다.
- [ ] 기본 벽 충돌 탄성은 과하게 튀지 않는 값이다.
- [ ] `Jump Tuning` 창에서 벽 충돌 후 세로 속도 처리 방식을 선택할 수 있다.
- [ ] 기본 세로 속도 처리 방식은 벽 충돌 직전의 세로 속도를 보존한다.
- [ ] 기존 세로 속도 처리 방식도 설정에서 다시 선택할 수 있다.
- [ ] 벽에 부딪히면 Player가 충돌 반대 방향으로 살짝 튀어나온다.
- [ ] 벽 접촉이 한 프레임 이상 유지되어도 Player가 벽에 붙은 상태로 멈추지 않는다.
- [ ] 충돌 직후 가로 상대속도가 작아도 Player가 벽에서 빠져나올 최소 가로 속도를 받는다.
- [ ] 바닥 착지에는 벽 탄성이 적용되지 않는다.
- [ ] Play Mode 중 변경한 탄성 값과 세로 속도 처리 방식은 게임 정지 후에도 유지된다.

## 구현 메모

- `JumpTuningConfig`에 `wallBounceElasticity`를 추가하고 기본값을 `0.18`, 허용 범위를 `0`부터 `2`까지로 제한했다.
- `JumpTuningConfig`에 `wallBounceVerticalVelocityMode`를 추가하고 기본값을 `Preserve Pre-Collision Y Velocity`로 설정했다.
- `JumpTuningWindow`의 `Collision` 섹션에서 `Wall Bounce Elasticity`와 `Wall Bounce Vertical Mode`를 편집할 수 있다.
- `PlayerController.ApplyJumpTuning`이 튜닝 값을 `PlayerJumpMotor.SetWallBounceElasticity`와 `PlayerJumpMotor.SetWallBounceVerticalVelocityMode`로 전달한다.
- `PlayerJumpMotor`가 `OnCollisionEnter2D`와 `OnCollisionStay2D`에서 가로 방향 충돌 법선만 감지해 벽 충돌 시 가로 속도를 약하게 되돌린다.
- 같은 벽 접촉에서 반복 반동이 과하게 누적되지 않도록 짧은 쿨다운을 두고, 충돌 해석 뒤 가로 속도가 거의 사라진 경우에도 탄성 값에 비례한 최소 이탈 속도를 적용한다.
- 벽 반동 직후 Player 위치를 벽 법선 방향으로 아주 조금 분리해 다음 물리 스텝에서도 같은 접촉에 붙어 있는 상황을 줄였다.
- 작은 충돌 속도를 별도로 무시하던 최소 충돌 속도 조건은 제거했다.
- `PlayerJumpMotor`가 물리 스텝 직전 속도를 캐시해 `Preserve Pre-Collision Y Velocity` 모드에서 벽 충돌 전 세로 속도를 유지한다.
- `Use Current Collision Y Velocity` 모드를 선택하면 기존처럼 충돌 처리 후 현재 세로 속도를 유지한다.
- 바닥이나 천장처럼 세로 방향 충돌에는 벽 탄성을 적용하지 않는다.
- 2026-06-22 벽 접촉 유지 방지 수정 후 Unity C# 컴파일러로 런타임 스크립트와 Editor 스크립트 컴파일을 확인했다. 오류 출력은 없었다.
- Play Mode에서 벽 충돌 체감은 직접 검증하지 않았다.

## 미결 질문

- 없음.
