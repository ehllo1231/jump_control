# UC-002-07: 플레이어 시각 요소와 충돌체 정렬 유지

- 상태: Implemented
- 마지막 갱신일: 2026-06-27

## 목적

개발자 또는 맵 제작자가 Player 시각 요소를 사각형 또는 캐릭터 이미지로 교체해도 Player의 충돌체와 점프 판정 기준이 안정적으로 유지된다.

## 액터

- 개발자
- 맵 제작자
- 테스터

## 사전 조건

- Player 오브젝트에 `PlayerVisual`, `BoxCollider2D`, `Rigidbody2D`가 있어야 한다.
- Player 시각 요소가 자식 `Visual` 오브젝트로 존재할 수 있다.
- 캐릭터 이미지 스프라이트를 사용할 때도 점프 로직과 충돌체는 Player 루트에 남아 있어야 한다.

## 트리거

- 사용자가 Scene View에서 Player 시각 요소 또는 Player 오브젝트를 조정한다.

## 기본 흐름

1. 사용자가 Scene View에서 Player의 보이는 시각 요소 위치를 옮긴다.
2. 시스템은 Player 루트 위치를 옮기는 Scene View 드래그 핸들을 표시한다.
3. 사용자가 드래그 핸들로 Player 루트 위치를 옮긴다.
4. 시스템은 시각 요소의 로컬 위치를 현재 크기, 오프셋, 방향에 맞는 정렬 위치로 되돌린다.
5. 시스템은 Player의 `BoxCollider2D` offset을 원점으로 유지한다.
6. 시스템은 Player의 물리 크기와 시각 크기 배율을 각각 적용한다.
7. 시스템은 시각 요소 크기가 충돌체보다 커지거나 작아져도 `BoxCollider2D.size`를 물리 크기로 유지한다.
8. 시스템은 시각 요소 하단을 충돌체 하단에 맞춰 시각 배율이 커져도 Player가 아래로 파고들어 보이지 않게 한다.
9. 시스템은 시각 X/Y 오프셋을 적용해 캐릭터 이미지만 좌우 또는 위아래로 이동할 수 있게 한다.
10. 시스템은 캐릭터 이미지를 좌우반전할 때 `BoxCollider2D` 윗변의 중점을 지나는 세로선을 기준축으로 사용한다.
11. 시스템은 캐릭터 이미지 스프라이트를 표시하더라도 원본 색상을 유지할 수 있게 한다.

## 대안 및 예외 흐름

- 1a. 사용자가 Player 루트 오브젝트를 움직이면 시각 요소와 충돌체는 함께 움직인다.
- 5a. 튜닝 값으로 Player 물리 크기가 변경되면 시스템은 충돌체 크기를 변경하고, 시각 요소는 물리 크기와 시각 크기 배율을 곱한 값으로 표시한다.
- 9a. 시각 X/Y 오프셋을 조정하면 시스템은 충돌체를 움직이지 않고 캐릭터 이미지만 좌우 또는 위아래로 이동한다.
- 10a. 시각 X 오프셋이 적용된 상태에서 좌우반전하면 시스템은 X 오프셋도 기준축 반대편으로 반전해 같은 축을 유지한다.
- 11a. 상태별 색상 틴트가 필요한 임시 사각형 스프라이트를 사용할 경우 시스템은 기존처럼 상태 색상을 적용할 수 있다.

## 인수 조건

- [ ] Scene View에서 보이는 Player 시각 요소를 움직여도 충돌체가 이전 위치에 남지 않는다.
- [ ] Scene View에서 Player 드래그 핸들을 끌어 Player 위치를 자유롭게 수정할 수 있다.
- [ ] Player 시각 요소의 로컬 위치는 현재 크기, 오프셋, 방향에 맞는 정렬 위치로 유지된다.
- [ ] Player의 `BoxCollider2D.offset`은 원점으로 유지된다.
- [ ] Player의 `BoxCollider2D.size`는 시각 크기 배율과 관계없이 물리 크기로 유지된다.
- [ ] Player 시각 요소 크기는 물리 크기와 시각 크기 배율을 곱한 값으로 표시된다.
- [ ] Player 시각 요소 하단은 시각 크기 배율과 관계없이 `BoxCollider2D` 하단 기준에 맞춰진다.
- [ ] Player 시각 X/Y 오프셋을 변경해도 `BoxCollider2D` 크기와 위치는 유지된다.
- [ ] 좌측 점프 등으로 Player 시각 요소가 좌우반전될 때 반전 기준축은 `BoxCollider2D` 윗변의 중점을 지나는 세로선이다.
- [ ] 좌우반전해도 `BoxCollider2D` 크기와 위치는 유지된다.
- [ ] 지면 판정은 정렬된 충돌체의 실제 하단과 폭을 기준으로 동작한다.
- [ ] 캐릭터 이미지 스프라이트를 사용하면 원본 색상이 상태 색상으로 틴트되지 않는다.

## 구현 메모

- `PlayerVisual`에 `ExecuteAlways`를 적용하여 Edit Mode와 Play Mode 모두에서 정렬을 유지한다.
- `Assets/IncomingImages/Player/stand/demonking_right.png`를 Player 표시 스프라이트로 사용하도록 `Assets/Prefabs/Player.prefab`을 갱신했다.
- 마왕 캐릭터 스프라이트의 약한 알파 여백을 잘라 `800x1103` 이미지로 정리하고, pixels per unit을 `1103`으로 설정해 세로 기준 1유닛 스프라이트로 사용한다.
- `PlayerVisual.tintByState` 옵션을 추가했고, Player prefab에서는 이 값을 꺼서 캐릭터 이미지 원본 색상을 유지한다.
- `PlayerScenePositionHandle`이 Scene View에 Player 루트 Transform을 이동하는 드래그 핸들을 표시한다.
- `PlayerVisual`은 자식 `Visual`을 기준으로 Player 루트 위치를 계속 이동시키지 않고, `Visual.localPosition`을 현재 크기, 오프셋, 방향에 맞는 값으로 직접 계산해 Scene View 드래그 중 위치가 증폭되는 문제를 방지한다.
- `Visual.localRotation`과 `BoxCollider2D.offset`은 원점/기본 회전으로 유지한다.
- `PlayerVisual`은 `Visual.localPosition`, `Visual.localRotation`, `BoxCollider2D.offset`만 정렬하고, 시각 요소 크기로 `BoxCollider2D.size`를 덮어쓰지 않는다.
- `PlayerController.ApplyJumpTuning`이 튜닝 값으로 크기를 적용할 때 `BoxCollider2D.size`는 물리 크기로 유지하고, `PlayerVisual.SetBodySize`가 시각 요소를 물리 크기와 시각 배율을 곱한 크기로 표시한다.
- `PlayerVisual`은 스프라이트의 로컬 하단을 `BoxCollider2D` 하단에 맞춰 시각 배율을 키워도 발 기준 위치가 유지되도록 한다.
- `PlayerVisual`은 하단 정렬 이후 시각 X/Y 오프셋을 더해 캐릭터 이미지만 좌우 또는 위아래로 조정한다.
- `PlayerVisual`은 좌우반전 상태를 직접 보관하고 `Visual.localScale.x`와 `Visual.localPosition.x`를 함께 계산해 `BoxCollider2D` 윗변 중점의 세로선을 기준으로 이미지를 반전한다.
- `PlayerVisual`은 디버그 모드에서 실제 `BoxCollider2D` 기준 피격 판정 테두리를 표시하므로 시각 요소와 충돌체 차이를 눈으로 확인할 수 있다.
- Unity 내장 Roslyn 컴파일러로 전체 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류는 없고 기존 런타임 직렬화 필드 관련 경고 2건만 발생했다.

## 미결 질문

- 없음.
