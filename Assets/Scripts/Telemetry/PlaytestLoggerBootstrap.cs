using UnityEngine;

public static class PlaytestLoggerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddLoggerToPlayer()
    {
#if UNITY_EDITOR
        if (!PlaytestLogger.AutoLoggingEnabled)
        {
            return;
        }

        PlayerController player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (player == null || player.GetComponent<PlaytestLogger>() != null)
        {
            return;
        }

        player.gameObject.AddComponent<PlaytestLogger>();
#endif
    }
}
