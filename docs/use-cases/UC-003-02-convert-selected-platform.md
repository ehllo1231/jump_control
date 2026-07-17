# UC-003-02: 기존 오브젝트를 Platform으로 변환

- 상태: Implemented
- 마지막 갱신일: 2026-07-17

## 목적

맵 제작자가 기존 SpriteRenderer와 BoxCollider2D가 있는 오브젝트를 `Platform2D` 기반 편집 가능 Platform으로 변환한다.

## 액터

- 맵 제작자
- 개발자

## 사전 조건

- Unity Editor에서 맵 씬이 열려 있어야 한다.
- Unity Editor가 Edit Mode여야 한다.
- 선택된 오브젝트의 루트 또는 `Visual` 자식에 SpriteRenderer가 있고, 루트에 BoxCollider2D가 있어야 한다.
- 선택된 오브젝트에 `Platform2D`가 아직 없어야 한다.

## 트리거

- 사용자가 Map Builder에서 `Convert Selected To Editable Platform` 버튼을 누른다.

## 기본 흐름

1. 시스템은 선택된 오브젝트가 변환 가능한지 확인한다.
2. 시스템은 현재 SpriteRenderer bounds 기준으로 Platform 크기를 계산한다.
3. 시스템은 오브젝트의 localScale을 `Vector3.one`으로 정리한다.
4. 시스템은 `Visual` 직접 자식을 생성하거나 재사용하고 기존 SpriteRenderer를 그 자식으로 이전한다.
5. 시스템은 루트에 `Platform2D` 컴포넌트를 추가하고 BoxCollider2D를 유지한다.
6. 시스템은 계산한 Width와 Height를 `Platform2D`에 적용한다.

## 대안 및 예외 흐름

- 1a. 선택된 오브젝트가 없거나 조건을 만족하지 않으면 변환 버튼은 비활성화된다.
- 1b. Play Mode이면 변환 버튼은 비활성화되며, 시스템은 Edit Mode에서 변환하라고 안내한다.

## 인수 조건

- [ ] 기존 SpriteRenderer와 BoxCollider2D가 있는 오브젝트를 Platform으로 변환할 수 있다.
- [ ] 변환 후 현재 외형 크기가 유지된다.
- [ ] 변환 후 Inspector에서 Width와 Height를 편집할 수 있다.
- [ ] Play Mode에서는 변환이 정식 씬 저장으로 오인되지 않도록 차단된다.
- [ ] 변환 후 루트에는 BoxCollider2D와 `Platform2D`, 직접 자식 `Visual`에는 기존 SpriteRenderer가 존재한다.

## 구현 메모

- `MapBuilderWindow.ConvertSelectedToPlatform`이 선택 오브젝트의 bounds와 부모 scale을 기준으로 로컬 Platform 크기를 계산한다.
- 변환 시 기존 SpriteRenderer 직렬화 값을 `Visual` 자식의 SpriteRenderer로 복사하고 루트 SpriteRenderer를 제거한다.
- `Undo.AddComponent<Platform2D>`로 변환 작업을 Undo 가능하게 기록한다.
- `MapBuilderWindow`는 Play Mode에서 변환 버튼을 비활성화하고, 직접 호출되더라도 안내 후 중단한다.
- 2026-06-22에 Play Mode 변환으로 Unity 백업 씬에만 변경이 남는 상황을 방지하도록 구현을 보강했다.

## 미결 질문

- 없음.
