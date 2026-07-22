# UC-012-01: 점프 플레이 프레임 안정성 유지

- 상태: Implemented
- 마지막 갱신일: 2026-07-22

## 목적

플레이어가 점프하고 카메라가 이동하는 동안 정적 맵 편집 동기화, 중복 물리 검사, 시각 정렬과 플레이테스트 로그 저장으로 인한 메인 스레드 부하를 줄여 게임을 매끄럽게 실행한다.

## 액터

- 플레이어
- 테스터
- 개발자

## 사전 조건

- 플레이 가능한 씬에 Player, 추적 Camera와 하나 이상의 Platform이 있어야 한다.
- Unity Editor 플레이테스트에서는 플레이테스트 로그 기록이 활성화될 수 있다.

## 트리거

- 플레이어가 점프를 실행한다.
- 점프 중 Player와 Camera가 매 프레임 이동한다.

## 기본 흐름

1. 시스템은 프레임마다 Player의 지면 접촉을 한 번 검사한다.
2. 시스템은 정적 Platform의 시각 요소와 Collider를 Play Mode 진입 시 적용하고, 플레이 중 매 프레임 다시 생성하지 않는다.
3. 시스템은 Camera와 Player 시각 요소를 픽셀 격자에 맞추되 값이 바뀔 때만 Transform과 Camera 속성을 갱신한다.
4. 시스템은 점프·착지 플레이테스트 로그를 메모리 버퍼에 기록하고 일정 간격 또는 세션 종료 시 파일에 반영한다.
5. 플레이어는 점프 입력, 물리 판정, 픽셀 정렬과 로그 데이터가 유지된 상태로 게임을 플레이한다.

## 대안 및 예외 흐름

- 2a. Edit Mode에서 맵 제작자가 SpriteRenderer 또는 Rect Tool로 Platform 크기를 직접 변경하면 시스템은 기존처럼 표시 크기와 Collider를 자동 동기화한다.
- 2b. Play Mode에서 시스템이 `Platform2D`의 공개 크기·도형 변경 API를 호출하면 변경 사항을 즉시 한 번 적용한다.
- 3a. Camera 또는 Player의 목표 위치가 이전 프레임과 같으면 동일한 값을 다시 쓰지 않는다.
- 4a. 로그 기록 중 오류가 발생하면 시스템은 기록만 중단하고 플레이 입력과 물리 동작은 계속 처리한다.
- 4b. Play Mode 또는 애플리케이션이 종료되면 시스템은 남은 로그 버퍼를 저장한 뒤 파일을 닫는다.

## 인수 조건

- [x] Play Mode의 `Platform2D.LateUpdate`는 Collider 경로 또는 삼각형 Mesh를 재생성하지 않는다.
- [x] Edit Mode에서 Platform 표시 크기를 직접 변경하면 Collider 크기가 계속 자동 동기화된다.
- [x] Player의 일반 프레임 지면 검사는 중복 호출되지 않는다.
- [x] Player 시각 픽셀 정렬은 한 프레임에 기본 위치와 스냅 위치를 연속으로 쓰지 않는다.
- [x] Camera의 픽셀 투영 속성과 Transform은 계산 결과가 바뀔 때만 기록된다.
- [x] 점프·착지 이벤트 처리 경로에서 로그 파일을 즉시 Flush하지 않는다.
- [x] 점프 입력, 착지 판정, 픽셀 정렬 및 플레이테스트 로그 레코드 형식은 유지된다.

## 구현 메모

- 구현 전 분석에서 `MVPJumpScene`에 `Platform2D` 52개가 있으며, 삼각형 Platform의 런타임 `LateUpdate`가 `PolygonCollider2D.SetPath`와 Mesh 갱신을 반복하는 것을 확인했다.
- `PlaytestLogger`는 점프, 착지, 낙하 및 경로 단절 이벤트에서 `StreamWriter.Flush()`를 즉시 호출하고 있었다.
- `Platform2D`는 Play Mode에서 편집용 `LateUpdate` 동기화를 건너뛰고, `SetSize`, `SetShape`, `SetTriangleVertex` 등 공개 API가 호출될 때만 명시적으로 다시 적용한다.
- `GroundChecker`의 자체 `Update` 검사를 제거하고 `PlayerController.Update`가 요청하는 한 번의 검사 결과를 사용한다. 접촉 필터는 매 검사마다 만들지 않고 `Awake`와 `OnValidate`에서 구성한다.
- `PlayerVisual`은 기본 로컬 위치를 먼저 기록한 뒤 월드 위치를 다시 스냅하던 흐름을 하나의 픽셀 정렬된 로컬 목표 위치 계산으로 합쳤다. Collider 및 디버그 외곽선 동기화는 런타임 매 프레임 경로에서 제외했다.
- `SimpleCameraFollow`는 Orthographic 속성과 Transform 위치가 실제로 달라질 때만 Unity 객체에 기록한다.
- `PlaytestLogger`는 64KB 쓰기 버퍼를 사용하고 최대 2초 또는 64개 레코드 단위로 저장한다. 점프·착지 이벤트에서는 즉시 Flush하지 않으며 세션 종료 시 남은 레코드를 모두 저장한다.
- Unity 내장 Roslyn 응답 파일을 사용해 Editor 및 Android Development Player 런타임 스크립트 컴파일을 통과했다.
- Unity 6000.3.1f1 임시 프로젝트의 Play Mode 자동 검증에서 Platform 런타임 동기화 중단과 명시적 갱신, GroundChecker 중복 Update 제거, 이동 중 Player 픽셀 정렬과 Collider 보존, 로그 버퍼 및 세션 종료 저장을 확인했다.

## 미결 질문

- 실제 목표 기기별 프레임 시간과 GC Alloc 수치는 Unity Profiler 실기 측정 후 추가한다.
