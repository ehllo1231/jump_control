# UC-005-02: Android 120 FPS 및 터치 반응성

- 상태: Implemented
- 마지막 갱신일: 2026-06-27

## 목적

사용자가 Android 기기에서 게임을 실행했을 때 가능한 경우 120 FPS로 동작하고, 화면 터치가 점프 입력에 빠르게 반영된다.

## 액터

- 사용자
- 테스터

## 사전 조건

- 게임이 Android 앱으로 빌드되어 설치되어 있어야 한다.
- Android 기기가 120 Hz 화면 갱신을 지원해야 120 FPS 표시가 가능하다.

## 트리거

- 사용자가 Android 기기에서 게임 앱을 실행한다.
- 사용자가 Android 기기 화면을 터치하거나 터치를 해제한다.

## 기본 흐름

1. 시스템은 Android 앱 시작 시 목표 프레임레이트를 120 FPS로 설정한다.
2. 시스템은 프레임레이트 목표가 적용될 수 있도록 vSync 기반 제한을 끈다.
3. 사용자가 화면을 터치하면 시스템은 해당 프레임의 점프 입력 누름으로 처리한다.
4. 사용자가 모든 터치를 해제하면 시스템은 해당 프레임의 점프 입력 해제로 처리한다.

## 대안 및 예외 흐름

- 1a. 기기 또는 운영체제가 120 Hz를 지원하지 않으면 실제 표시 FPS는 기기가 허용하는 값으로 제한될 수 있다.
- 1b. Android가 아닌 플랫폼에서는 Android 전용 FPS 설정을 적용하지 않는다.

## 인수 조건

- [ ] Android 런타임에서 `Application.targetFrameRate`가 120으로 설정된다.
- [ ] Android 런타임에서 `QualitySettings.vSyncCount`가 0으로 설정된다.
- [ ] 터치 시작과 종료는 기존 점프 입력 흐름에 같은 프레임의 누름/해제 신호로 전달된다.
- [ ] Android가 아닌 빌드의 FPS 설정은 변경하지 않는다.

## 구현 메모

- `AndroidFrameRateBootstrap`이 Android 런타임 시작 전 `QualitySettings.vSyncCount`를 0으로 설정하고 `Application.targetFrameRate`를 120으로 설정한다.
- `JumpInputReader`는 이미 `Input.touchCount`와 `Input.GetTouch`를 매 프레임 읽어 터치 시작/종료를 같은 프레임의 `WasPressed`/`WasReleased`로 변환하고 있었다.
- 터치 입력 코드 자체의 추가 프레임 지연은 확인되지 않았고, 낮은 FPS와 vSync 제한이 입력 샘플링 간격을 키울 수 있어 Android 시작 시 120 FPS 목표를 적용했다.
- Unity 포함 C# 컴파일러로 Android 심볼을 켠 런타임 스크립트 컴파일과 일반 런타임 스크립트 컴파일을 확인했다. 기존 직렬화 필드 관련 경고만 출력됐고 컴파일 오류는 없었다.

## 미결 질문

- 없음.
