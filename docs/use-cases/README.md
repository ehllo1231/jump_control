# 유스케이스

프로젝트 유스케이스를 구현 코드와 분리하여 관리한다.

ID는 같은 기능군끼리 같은 세 자리 그룹 번호를 공유한다. 예를 들어 `UC-003-01`부터 `UC-003-08`까지는 맵 제작 도구 관련 세부 유스케이스다.

| ID | 제목 | 상태 | 문서 | 갱신일 |
| --- | --- | --- | --- | --- |
| UC-001 | 사용자 점프 | Implemented | [문서](./UC-001-player-jump.md) | 2026-06-27 |
| UC-002 | 실시간 점프 튜닝 기능군 | Superseded | [문서](./UC-002-live-jump-tuning.md) | 2026-06-24 |
| UC-002-01 | 점프 튜닝 대상 선택 | Implemented | [문서](./UC-002-01-select-jump-tuning-target.md) | 2026-06-21 |
| UC-002-02 | 플레이어 크기 튜닝 | Implemented | [문서](./UC-002-02-tune-player-size.md) | 2026-06-27 |
| UC-002-03 | 점프 방향 튜닝 | Implemented | [문서](./UC-002-03-tune-jump-direction.md) | 2026-07-17 |
| UC-002-04 | 점프 파워 튜닝 | Implemented | [문서](./UC-002-04-tune-jump-power.md) | 2026-06-21 |
| UC-002-05 | Play Mode 튜닝 값 유지 | Implemented | [문서](./UC-002-05-persist-play-mode-tuning.md) | 2026-06-21 |
| UC-002-06 | 코드 수정 중 튜닝 값 보존 | Implemented | [문서](./UC-002-06-preserve-manual-tuning-values.md) | 2026-06-21 |
| UC-002-07 | 플레이어 시각 요소와 충돌체 정렬 유지 | Implemented | [문서](./UC-002-07-keep-player-visual-and-collider-aligned.md) | 2026-06-27 |
| UC-002-08 | 벽 충돌 탄성 및 세로 속도 튜닝 | Implemented | [문서](./UC-002-08-tune-wall-bounce-elasticity.md) | 2026-06-22 |
| UC-002-09 | 디버그 커스텀 점프 실행 | Implemented | [문서](./UC-002-09-run-custom-debug-jump.md) | 2026-06-24 |
| UC-002-10 | 디버그 점프 위치 히스토리 이동 | Implemented | [문서](./UC-002-10-return-to-previous-jump-position.md) | 2026-06-26 |
| UC-002-11 | 디버그 피격 판정 표시 | Implemented | [문서](./UC-002-11-show-debug-hitbox.md) | 2026-06-27 |
| UC-002-12 | 플레이어 이미지 파일 자동 반영 | Implemented | [문서](./UC-002-12-auto-update-player-sprite.md) | 2026-06-27 |
| UC-003 | 2D 점프 맵 제작 툴 기능군 | Superseded | [문서](./UC-003-map-authoring-tool.md) | 2026-06-21 |
| UC-003-01 | Platform 생성 및 크기 조정 | Implemented | [문서](./UC-003-01-create-and-size-platform.md) | 2026-06-21 |
| UC-003-02 | 기존 오브젝트를 Platform으로 변환 | Implemented | [문서](./UC-003-02-convert-selected-platform.md) | 2026-06-22 |
| UC-003-03 | 선택한 두 Platform 거리 표시 | Implemented | [문서](./UC-003-03-measure-selected-platforms.md) | 2026-06-21 |
| UC-003-04 | 선택 Platform에서 플레이 테스트 시작 | Implemented | [문서](./UC-003-04-start-playtest-from-platform.md) | 2026-06-21 |
| UC-003-05 | Play Mode 생성 Platform 유지 | Implemented | [문서](./UC-003-05-persist-play-mode-platforms.md) | 2026-06-22 |
| UC-003-06 | 점프 도달 후보 표시 | Implemented | [문서](./UC-003-06-show-jump-reachability.md) | 2026-06-26 |
| UC-003-07 | 기본 Platform 색상 일관성 유지 | Implemented | [문서](./UC-003-07-keep-platform-color-consistent.md) | 2026-06-22 |
| UC-003-08 | 코드 수정 중 사용자 맵 구성 보존 | Implemented | [문서](./UC-003-08-preserve-user-map-edits.md) | 2026-06-23 |
| UC-003-09 | 삼각형 Platform 생성 | Implemented | [문서](./UC-003-09-create-triangle-platform.md) | 2026-06-25 |
| UC-003-10 | Platform 회전 조정 | Implemented | [문서](./UC-003-10-rotate-platform.md) | 2026-06-22 |
| UC-003-11 | 직각 삼각형 Platform 생성 및 편집 | Implemented | [문서](./UC-003-11-create-right-triangle-platform.md) | 2026-06-25 |
| UC-004-01 | 배경음악 일시정지 지점 재개 | Implemented | [문서](./UC-004-01-resume-background-music.md) | 2026-06-26 |
| UC-004-02 | Android 빌드 배경음악 재생 | Implemented | [문서](./UC-004-02-play-background-music-on-android.md) | 2026-06-27 |
| UC-005-01 | Android 앱 가로 실행 | Implemented | [문서](./UC-005-01-launch-android-in-landscape.md) | 2026-06-27 |
| UC-005-02 | Android 120 FPS 및 터치 반응성 | Implemented | [문서](./UC-005-02-run-android-at-120-fps.md) | 2026-06-27 |
| UC-006-01 | 재시작 시 플레이어 위치 복원 | Implemented | [문서](./UC-006-01-resume-player-position-after-restart.md) | 2026-06-27 |
| UC-007-01 | 플레이테스트 이동 및 이벤트 로그 기록 | Implemented | [문서](./UC-007-01-record-playtest-logs.md) | 2026-06-27 |
| UC-007-02 | 플레이테스트 로그 Scene View 시각화 | Implemented | [문서](./UC-007-02-visualize-playtest-logs.md) | 2026-06-27 |
| UC-007-03 | 플레이테스트 로그 뷰어 툴 | Implemented | [문서](./UC-007-03-view-playtest-logs-in-tool.md) | 2026-06-27 |
| UC-010-01 | 스테이지 전체를 세로 고화질 이미지로 저장 | Verified | [문서](./UC-010-01-capture-stage-as-portrait-image.md) | 2026-07-17 |
