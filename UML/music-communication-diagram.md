# Music Output Module - Communication Diagram

이 문서는 배경음악이 준비되고, 재생되고, 설정 변경과 앱 일시정지에 반응하는 메시지 흐름을 간단히 보여줍니다.

## 핵심 시나리오

- 게임이 시작되면 음악 재생기가 없을 경우 자동으로 만들어집니다.
- 음악 재생기는 음악 파일을 받아 `AudioSource`로 재생합니다.
- 사용자가 설정 메뉴에서 음악을 끄거나 켜면 저장값이 바뀌고, 재생 중인 음악은 즉시 음소거 상태만 바뀝니다.
- 앱이 백그라운드로 가면 재생 위치를 기억하고 멈췄다가, 복귀하면 같은 위치에서 이어서 재생합니다.

```mermaid
flowchart LR
    User["사용자"]
    App["Unity 앱 시작/복귀"]
    Bootstrap["BackgroundMusicBootstrap"]
    Resources["Resources<br/>음악 파일"]
    MusicLoop["BackgroundMusicLoop"]
    AudioSource["AudioSource"]
    SettingsMenu["GameSettingsMenu"]
    GameMusic["GameMusic"]
    PlayerPrefs["PlayerPrefs"]
    Speaker["스피커"]

    App -->|"1. 씬 로드 후 음악 준비 요청"| Bootstrap
    Bootstrap -->|"2. 음악 파일 로드"| Resources
    Resources -->|"3. AudioClip 반환"| Bootstrap
    Bootstrap -->|"4. 재생 관리자 생성 및 Configure"| MusicLoop
    MusicLoop -->|"5. PlayLoop 시작"| AudioSource
    AudioSource -->|"6. 배경음악 출력"| Speaker

    User -->|"7. 설정 메뉴에서 MUSIC 토글"| SettingsMenu
    SettingsMenu -->|"8. GameMusic.Enabled 변경"| GameMusic
    GameMusic -->|"9. ON/OFF 저장"| PlayerPrefs
    GameMusic -->|"10. EnabledChanged 이벤트 전달"| MusicLoop
    MusicLoop -->|"11. AudioSource.mute 전환"| AudioSource

    App -->|"12. 앱 일시정지 또는 포커스 잃음"| MusicLoop
    MusicLoop -->|"13. 현재 재생 위치 저장 후 Pause"| AudioSource
    App -->|"14. 앱 복귀 또는 포커스 획득"| MusicLoop
    MusicLoop -->|"15. 저장 위치부터 UnPause"| AudioSource
```

## 관련 코드

- `Assets/Scripts/Audio/BackgroundMusicLoop.cs`
- `Assets/Scripts/Audio/GameMusic.cs`
- `Assets/Scripts/Platform/GameSettingsMenu.cs`
- `Assets/Resources/Music/first_castle.mp3`
