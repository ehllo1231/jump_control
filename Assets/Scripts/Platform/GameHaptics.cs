using UnityEngine;

public static class GameHaptics
{
    public const string EnabledPlayerPrefsKey = "JumpTiming.Haptics.Enabled";

    private const int EnabledDefault = 1;
    private const float ChargePulseInterval = 0.18f;
    private const long FirstPressDurationMs = 22L;
    private const int FirstPressAmplitude = 120;
    private const long ChargePulseDurationMs = 10L;
    private const int ChargePulseAmplitude = 35;
    private const long MinJumpDurationMs = 35L;
    private const long MaxJumpDurationMs = 95L;
    private const int MinJumpAmplitude = 85;
    private const int MaxJumpAmplitude = 255;
    private const long LandingDurationMs = 42L;
    private const int LandingAmplitude = 135;
    private const long MinWallBounceDurationMs = 28L;
    private const long MaxWallBounceDurationMs = 58L;
    private const int MinWallBounceAmplitude = 145;
    private const int MaxWallBounceAmplitude = 230;

    private static bool chargingFeedbackActive;
    private static float chargePulseTimer;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(EnabledPlayerPrefsKey, EnabledDefault) == 1;
        set
        {
            PlayerPrefs.SetInt(EnabledPlayerPrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();

            if (!value)
            {
                StopChargingFeedback();
                CancelNativeVibration();
            }
        }
    }

    public static void PlayFirstPress()
    {
        Vibrate(FirstPressDurationMs, FirstPressAmplitude);
    }

    public static void StartChargingFeedback()
    {
        chargingFeedbackActive = true;
        chargePulseTimer = ChargePulseInterval;
        TickChargingFeedback(0f);
    }

    public static void TickChargingFeedback(float deltaTime)
    {
        if (!chargingFeedbackActive)
        {
            return;
        }

        chargePulseTimer += Mathf.Max(0f, deltaTime);
        if (chargePulseTimer < ChargePulseInterval)
        {
            return;
        }

        chargePulseTimer = 0f;
        Vibrate(ChargePulseDurationMs, ChargePulseAmplitude);
    }

    public static void StopChargingFeedback()
    {
        chargingFeedbackActive = false;
        chargePulseTimer = 0f;
    }

    public static void PlayJumpExecuted(float normalizedPower)
    {
        float power = Mathf.Clamp01(normalizedPower);
        long durationMs = Mathf.RoundToInt(Mathf.Lerp(MinJumpDurationMs, MaxJumpDurationMs, power));
        int amplitude = Mathf.RoundToInt(Mathf.Lerp(MinJumpAmplitude, MaxJumpAmplitude, power));
        Vibrate(durationMs, amplitude);
    }

    public static void PlayLanding()
    {
        Vibrate(LandingDurationMs, LandingAmplitude);
    }

    public static void PlayWallBounce(float normalizedImpact)
    {
        float impact = Mathf.Clamp01(normalizedImpact);
        long durationMs = Mathf.RoundToInt(Mathf.Lerp(MinWallBounceDurationMs, MaxWallBounceDurationMs, impact));
        int amplitude = Mathf.RoundToInt(Mathf.Lerp(MinWallBounceAmplitude, MaxWallBounceAmplitude, impact));
        Vibrate(durationMs, amplitude);
    }

    private static void Vibrate(long durationMs, int amplitude)
    {
        if (!Enabled)
        {
            return;
        }

        durationMs = Mathf.Max(1, (int)durationMs);
        amplitude = Mathf.Clamp(amplitude, 1, 255);
        VibrateNative(durationMs, amplitude);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject vibrator;
    private static bool nativeUnavailable;
    private static int sdkInt = -1;

    private static void VibrateNative(long durationMs, int amplitude)
    {
        if (nativeUnavailable)
        {
            return;
        }

        try
        {
            AndroidJavaObject currentVibrator = GetVibrator();
            if (currentVibrator == null || !HasVibrator(currentVibrator))
            {
                return;
            }

            if (GetSdkInt() >= 26)
            {
                using (AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot",
                    durationMs,
                    amplitude))
                {
                    currentVibrator.Call("vibrate", effect);
                }
            }
            else
            {
                currentVibrator.Call("vibrate", durationMs);
            }
        }
        catch (System.Exception exception)
        {
            nativeUnavailable = true;
            Debug.LogWarning($"Android vibration is unavailable: {exception.Message}");
        }
    }

    private static void CancelNativeVibration()
    {
        if (nativeUnavailable)
        {
            return;
        }

        try
        {
            AndroidJavaObject currentVibrator = GetVibrator();
            currentVibrator?.Call("cancel");
        }
        catch (System.Exception)
        {
            nativeUnavailable = true;
        }
    }

    private static AndroidJavaObject GetVibrator()
    {
        if (vibrator != null)
        {
            return vibrator;
        }

        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
        using (AndroidJavaClass contextClass = new AndroidJavaClass("android.content.Context"))
        {
            string vibratorService = contextClass.GetStatic<string>("VIBRATOR_SERVICE");
            vibrator = context.Call<AndroidJavaObject>("getSystemService", vibratorService);
        }

        return vibrator;
    }

    private static int GetSdkInt()
    {
        if (sdkInt >= 0)
        {
            return sdkInt;
        }

        using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            sdkInt = version.GetStatic<int>("SDK_INT");
        }

        return sdkInt;
    }

    private static bool HasVibrator(AndroidJavaObject currentVibrator)
    {
        try
        {
            return currentVibrator.Call<bool>("hasVibrator");
        }
        catch (System.Exception)
        {
            return true;
        }
    }
#else
    private static void VibrateNative(long durationMs, int amplitude)
    {
    }

    private static void CancelNativeVibration()
    {
    }
#endif
}
