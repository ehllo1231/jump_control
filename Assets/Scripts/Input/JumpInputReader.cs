using UnityEngine;

/// <summary>
/// 점프 버튼 입력만 읽는 어댑터입니다.
/// 나중에 모바일 버튼에서 SetExternalJumpHeld를 호출하면 점프 로직을 바꾸지 않고 입력만 교체할 수 있습니다.
/// </summary>
public class JumpInputReader : MonoBehaviour
{
    [Header("Keyboard Input")]
    [SerializeField] private bool enableKeyboardInput = true;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Header("Touch Input")]
    [SerializeField] private bool enableTouchInput = true;
#if UNITY_EDITOR
    [SerializeField] private bool enableEditorMouseTouchSimulation;
#endif

    private bool previousHeld;
    private bool externalJumpHeld;

    public bool WasPressed { get; private set; }
    public bool IsHeld { get; private set; }
    public bool WasReleased { get; private set; }

    /// <summary>
    /// PlayerController가 매 프레임 한 번 호출합니다.
    /// Unity Update 순서에 의존하지 않기 위해 입력 갱신을 명시적으로 분리했습니다.
    /// </summary>
    public void Tick()
    {
        bool keyboardHeld = enableKeyboardInput && Input.GetKey(jumpKey);
        bool touchHeld = enableTouchInput && IsTouchHeld();
        bool currentHeld = keyboardHeld || touchHeld || externalJumpHeld;

        WasPressed = currentHeld && !previousHeld;
        WasReleased = !currentHeld && previousHeld;
        IsHeld = currentHeld;

        previousHeld = currentHeld;
    }

    /// <summary>
    /// 모바일 UI 버튼이나 다른 입력 장치가 점프 버튼 상태를 주입할 때 사용합니다.
    /// </summary>
    public void SetExternalJumpHeld(bool held)
    {
        externalJumpHeld = held;
    }

    public void ClearExternalInput()
    {
        externalJumpHeld = false;
    }

    private void OnDisable()
    {
        previousHeld = false;
        externalJumpHeld = false;
        WasPressed = false;
        IsHeld = false;
        WasReleased = false;
    }

    private bool IsTouchHeld()
    {
#if UNITY_EDITOR
        if (enableEditorMouseTouchSimulation && Input.GetMouseButton(0))
        {
            return true;
        }
#endif

        for (int i = 0; i < Input.touchCount; i++)
        {
            TouchPhase phase = Input.GetTouch(i).phase;
            if (phase != TouchPhase.Ended && phase != TouchPhase.Canceled)
            {
                return true;
            }
        }

        return false;
    }
}
