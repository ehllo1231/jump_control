# UC-002-11: 디버그 피격 판정 표시

- 상태: Implemented
- 마지막 갱신일: 2026-06-27

## 목적

개발자 또는 테스터가 디버그 모드에서 주인공의 실제 피격 판정 범위를 눈으로 확인한다.

## 액터

- 개발자
- 테스터

## 사전 조건

- `Jump Tuning` 창에 유효한 `PlayerController` 대상이 선택되어 있어야 한다.
- Player 오브젝트에 `BoxCollider2D`와 `PlayerVisual`이 있어야 한다.

## 트리거

- 사용자가 `Jump Tuning` 창에서 디버그 모드를 켠다.

## 기본 흐름

1. 사용자가 디버그 모드를 켠다.
2. 시스템은 Player의 실제 `BoxCollider2D` 범위를 얇은 연두색 테두리로 표시한다.
3. 사용자가 Player 물리 크기를 변경하면 시스템은 변경된 `BoxCollider2D` 범위에 맞춰 테두리를 갱신한다.
4. 사용자가 디버그 모드를 끄면 시스템은 피격 판정 테두리를 숨긴다.

## 대안 및 예외 흐름

- 2a. Player에 유효한 `BoxCollider2D`가 없으면 시스템은 피격 판정 테두리를 표시하지 않는다.

## 인수 조건

- [ ] 디버그 모드가 켜지면 Player의 실제 `BoxCollider2D` 범위가 얇은 연두색 테두리로 표시된다.
- [ ] 피격 판정 테두리는 캐릭터 이미지 크기가 아니라 실제 충돌체 크기를 기준으로 표시된다.
- [ ] Player 물리 크기를 변경하면 피격 판정 테두리도 같은 크기로 갱신된다.
- [ ] 디버그 모드가 꺼지면 피격 판정 테두리가 숨겨진다.

## 구현 메모

- `JumpTuningConfig.DebugModeEnabled` 값을 `PlayerController.ApplyJumpTuning`에서 `PlayerVisual`로 전달한다.
- `PlayerVisual`은 디버그 모드가 켜져 있을 때 `LineRenderer`로 `BoxCollider2D.size`와 `BoxCollider2D.offset` 기준의 얇은 연두색 테두리를 표시한다.
- 피격 판정 테두리는 캐릭터 이미지 스프라이트의 크기, 시각 배율, 시각 Y 오프셋과 관계없이 실제 충돌체 기준으로 갱신된다.
- 디버그 모드가 꺼지면 테두리 오브젝트를 비활성화한다.
- Unity Roslyn 응답 파일 기반으로 런타임 및 Editor 스크립트 컴파일을 확인했다.
- 실제 Play Mode에서 디버그 모드 토글과 테두리 표시를 눈으로 확인하는 수동 검증은 수행하지 않았으므로 상태는 `Verified`가 아닌 `Implemented`로 유지한다.

## 미결 질문

- 없음.
