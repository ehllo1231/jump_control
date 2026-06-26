# UC-005-01: Android 앱 가로 실행

- 상태: Implemented
- 마지막 갱신일: 2026-06-27

## 목적

사용자가 Android 기기에서 게임 앱을 실행했을 때 게임 화면이 세로가 아니라 가로 방향으로 표시된다.

## 액터

- 사용자
- 테스터

## 사전 조건

- 게임이 Android 앱으로 빌드되어 설치되어 있어야 한다.

## 트리거

- 사용자가 Android 기기에서 게임 앱을 실행한다.

## 기본 흐름

1. 시스템은 Android 앱 시작 시 화면 방향을 가로 방향으로 설정한다.
2. 시스템은 세로 방향 자동 회전을 허용하지 않는다.
3. 사용자는 가로 화면에서 게임을 플레이한다.

## 대안 및 예외 흐름

- 1a. Android가 아닌 플랫폼에서는 Android 전용 화면 방향 강제 로직을 실행하지 않는다.

## 인수 조건

- [ ] Android APK로 설치한 게임이 가로 방향으로 실행된다.
- [ ] Android에서 세로 방향으로 자동 회전하지 않는다.
- [ ] Android가 아닌 빌드의 화면 방향 동작은 변경하지 않는다.

## 구현 메모

- `ProjectSettings/ProjectSettings.asset`에서 Android 자동 회전 허용 방향 중 세로와 세로 반전을 비활성화했다.
- `AndroidScreenOrientationBootstrap`이 Android 런타임 시작 전 `Screen.orientation`을 `LandscapeLeft`로 설정하고 가로 방향 자동 회전만 허용한다.
- Android가 아닌 플랫폼에서는 `UNITY_ANDROID` 조건부 컴파일로 화면 방향 강제 로직을 포함하지 않는다.
- Unity 포함 C# 컴파일러로 Android 심볼을 켠 런타임 스크립트 컴파일과 에디터 스크립트 포함 컴파일을 확인했다. 기존 직렬화 필드 관련 경고만 출력됐고 컴파일 오류는 없었다.

## 미결 질문

- 없음.
