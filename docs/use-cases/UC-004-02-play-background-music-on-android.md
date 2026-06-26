# UC-004-02: Android 빌드 배경음악 재생

- 상태: Implemented
- 마지막 갱신일: 2026-06-27

## 목적

사용자가 Android APK로 설치한 게임에서도 에디터와 동일하게 배경음악을 들을 수 있다.

## 액터

- 사용자
- 테스터

## 사전 조건

- Android 빌드에 배경음악 클립이 포함되어 있어야 한다.
- 게임 씬 또는 런타임 초기화 경로에서 `BackgroundMusicLoop`가 생성되어야 한다.

## 트리거

- 사용자가 Android 기기에서 게임 앱을 실행한다.

## 기본 흐름

1. 시스템은 Android 빌드에 포함된 배경음악 클립을 로드한다.
2. 시스템은 `BackgroundMusicLoop`를 통해 배경음악을 재생한다.
3. 시스템은 기존 배경음악 반복 및 일시정지 지점 재개 동작을 유지한다.

## 대안 및 예외 흐름

- 1a. 배경음악 클립을 찾지 못하면 시스템은 경고를 남기고 배경음악을 재생하지 않는다.

## 인수 조건

- [ ] Android APK로 설치한 게임에서 배경음악이 재생된다.
- [ ] 에디터에서도 같은 배경음악 클립을 로드해 재생할 수 있다.
- [ ] 기존 일시정지 지점 재개와 반복 재생 동작이 유지된다.

## 구현 메모

- 배경음악 파일을 `Assets/Resources/Music/first_castle.mp3`로 이동해 Android 빌드에 포함되도록 했다.
- `BackgroundMusicBootstrap`은 빌드 런타임에서 `Resources.Load<AudioClip>("Music/first_castle")`로 배경음악을 로드한다.
- 에디터용 배경음악 설정 코드도 이동된 `Assets/Resources/Music/first_castle.mp3` 경로를 사용하도록 갱신했다.
- Unity 포함 C# 컴파일러로 Android 심볼을 켠 런타임 스크립트 컴파일과 에디터 스크립트 포함 컴파일을 확인했다. 기존 직렬화 필드 관련 경고만 출력됐고 컴파일 오류는 없었다.

## 미결 질문

- 없음.
