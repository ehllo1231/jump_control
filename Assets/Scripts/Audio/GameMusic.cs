using System;
using UnityEngine;

public static class GameMusic
{
    public const string EnabledPlayerPrefsKey = "JumpTiming.Music.Enabled";

    private const int EnabledDefault = 1;

    public static event Action<bool> EnabledChanged;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(EnabledPlayerPrefsKey, EnabledDefault) == 1;
        set
        {
            if (value == Enabled)
            {
                return;
            }

            PlayerPrefs.SetInt(EnabledPlayerPrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
            EnabledChanged?.Invoke(value);
        }
    }
}
