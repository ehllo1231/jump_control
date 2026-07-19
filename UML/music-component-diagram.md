# Music Output Module - Component Diagram

이 문서는 게임의 배경음악 송출 구조를 도메인 지식이 없어도 이해할 수 있도록 추상화한 컴포넌트 다이어그램입니다.

## 핵심 요약

- `BackgroundMusicBootstrap`은 씬에 음악 재생기가 없을 때 자동으로 배경음악 재생기를 준비합니다.
- `BackgroundMusicLoop`는 실제 재생 흐름을 관리합니다. 음악을 켜고, 끝부분에서 작게 줄이고, 다시 반복합니다.
- `GameMusic`은 음악 ON/OFF 설정을 저장하고 변경 이벤트를 보냅니다.
- `GameSettingsMenu`는 사용자가 음악 설정을 바꾸는 화면 진입점입니다.
- Unity의 `AudioSource`가 최종적으로 스피커로 나갈 소리를 재생합니다.

```mermaid
flowchart LR
    subgraph UserSide["사용자 영역"]
        User["사용자"]
        SettingsMenu["GameSettingsMenu<br/>설정 메뉴"]
    end

    subgraph GameLogic["게임 음악 제어 영역"]
        GameMusic["GameMusic<br/>음악 설정 저장소"]
        MusicLoop["BackgroundMusicLoop<br/>배경음악 재생 관리자"]
        Bootstrap["BackgroundMusicBootstrap<br/>런타임 자동 준비"]
    end

    subgraph UnityRuntime["Unity 런타임 영역"]
        PlayerPrefs["PlayerPrefs<br/>설정 영구 저장"]
        Resources["Resources/Music/first_castle<br/>음악 파일"]
        AudioSource["AudioSource<br/>Unity 재생 장치"]
        Lifecycle["Application Pause/Focus<br/>앱 중단·복귀 신호"]
        Speaker["기기 스피커"]
    end

    subgraph EditorTools["Unity 에디터 보조 도구"]
        SceneSetup["BackgroundMusicSceneSetup<br/>기존 씬에 음악 배치"]
        SceneBuilder["MVPSceneBuilder<br/>테스트 씬 생성 시 음악 배치"]
    end

    User -->|"MUSIC 토글 조작"| SettingsMenu
    SettingsMenu -->|"ON/OFF 값 변경"| GameMusic
    GameMusic -->|"저장"| PlayerPrefs
    GameMusic -->|"EnabledChanged 이벤트"| MusicLoop

    Bootstrap -->|"음악 파일 로드"| Resources
    Bootstrap -->|"재생 관리자 생성/설정"| MusicLoop
    SceneSetup -.->|"에디터에서 배치"| MusicLoop
    SceneBuilder -.->|"씬 생성 시 배치"| MusicLoop

    MusicLoop -->|"클립, 볼륨, 음소거, 일시정지 제어"| AudioSource
    Resources -->|"AudioClip 제공"| MusicLoop
    Lifecycle -->|"Pause/Resume 요청"| MusicLoop
    AudioSource -->|"음악 출력"| Speaker
```

## 관련 코드

- `Assets/Scripts/Audio/BackgroundMusicLoop.cs`
- `Assets/Scripts/Audio/GameMusic.cs`
- `Assets/Scripts/Platform/GameSettingsMenu.cs`
- `Assets/Editor/BackgroundMusicSceneSetup.cs`
- `Assets/Editor/MVPSceneBuilder.cs`
- `Assets/Resources/Music/first_castle.mp3`
