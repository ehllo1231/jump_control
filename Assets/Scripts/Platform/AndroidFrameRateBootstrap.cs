using UnityEngine;

public static class AndroidFrameRateBootstrap
{
    private const int TargetFrameRate = 120;

#if UNITY_ANDROID
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureFrameRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
#endif
}
