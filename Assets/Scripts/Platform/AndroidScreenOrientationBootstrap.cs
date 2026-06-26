using UnityEngine;

public static class AndroidScreenOrientationBootstrap
{
#if UNITY_ANDROID
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceLandscapeOrientation()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
    }
#endif
}
