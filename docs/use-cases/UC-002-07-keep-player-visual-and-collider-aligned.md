# UC-002-07: 플레이어 시각 요소와 충돌체 정렬 유지

- 상태: Implemented
- 마지막 갱신일: 2026-06-21

## 목적

개발자 또는 맵 제작자가 Scene View에서 보이는 Player 사각형을 움직이거나 크기를 맞출 때 Player의 충돌체와 시각 요소가 같은 위치와 크기를 유지한다.

## 액터

- 개발자
- 맵 제작자
- 테스터

## 사전 조건

- Player 오브젝트에 `PlayerVisual`, `BoxCollider2D`, `Rigidbody2D`가 있어야 한다.
- Player 시각 요소가 자식 `Visual` 오브젝트로 존재할 수 있다.

## 트리거

- 사용자가 Scene View에서 Player 시각 요소 또는 Player 오브젝트를 조정한다.

## 기본 흐름

1. 사용자가 Scene View에서 Player의 보이는 사각형 위치를 옮긴다.
2. 시스템은 Player 루트 위치를 옮기는 Scene View 드래그 핸들을 표시한다.
3. 사용자가 드래그 핸들로 Player 루트 위치를 옮긴다.
4. 시스템은 시각 요소의 로컬 위치를 원점으로 되돌린다.
5. 시스템은 Player의 `BoxCollider2D` offset을 원점으로 유지한다.
6. 시스템은 시각 요소 크기와 `BoxCollider2D.size`를 같은 값으로 유지한다.

## 대안 및 예외 흐름

- 1a. 사용자가 Player 루트 오브젝트를 움직이면 시각 요소와 충돌체는 함께 움직인다.
- 5a. 튜닝 값으로 Player 크기가 변경되면 시스템은 시각 요소와 충돌체 크기를 같은 값으로 적용한다.

## 인수 조건

- [ ] Scene View에서 보이는 Player 사각형을 움직여도 충돌체가 이전 위치에 남지 않는다.
- [ ] Scene View에서 Player 드래그 핸들을 끌어 Player 위치를 자유롭게 수정할 수 있다.
- [ ] Player 시각 요소의 로컬 위치는 원점으로 유지된다.
- [ ] Player의 `BoxCollider2D.offset`은 원점으로 유지된다.
- [ ] Player 시각 요소 크기와 `BoxCollider2D.size`는 같은 값으로 유지된다.
- [ ] 지면 판정은 정렬된 충돌체의 실제 하단과 폭을 기준으로 동작한다.

## 구현 메모

- `PlayerVisual`에 `ExecuteAlways`를 적용하여 Edit Mode와 Play Mode 모두에서 정렬을 유지한다.
- `PlayerScenePositionHandle`이 Scene View에 Player 루트 Transform을 이동하는 드래그 핸들을 표시한다.
- `PlayerVisual`은 자식 `Visual`을 기준으로 Player 루트 위치를 계속 이동시키지 않고, `Visual.localPosition`을 원점으로 고정해 Scene View 드래그 중 위치가 증폭되는 문제를 방지한다.
- `Visual.localRotation`과 `BoxCollider2D.offset`은 원점/기본 회전으로 유지한다.
- `Visual.localScale`을 기준으로 `BoxCollider2D.size`를 갱신하므로 직접 크기를 맞춘 경우에도 보이는 사각형과 충돌체 크기가 같아진다.
- `PlayerController.ApplyJumpTuning`이 튜닝 값으로 크기를 적용할 때도 `PlayerVisual.SetBodySize`가 시각 요소와 충돌체 크기를 함께 맞춘다.
- Unity 내장 Roslyn 컴파일러로 전체 런타임 및 Editor 스크립트 컴파일을 확인했다. 오류는 없고 기존 런타임 직렬화 필드 관련 경고 2건만 발생했다.

## 미결 질문

- 없음.
