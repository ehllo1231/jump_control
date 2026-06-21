using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택한 Platform 위에서 Player를 시작시키는 Play Mode 진입 및 배치 서비스입니다.
/// </summary>
[InitializeOnLoad]
public static class MapPlaytestLauncher
{
    private const string PendingKey = "JumpTiming.MapPlaytest.Pending";
    private const string SpawnXKey = "JumpTiming.MapPlaytest.SpawnX";
    private const string SpawnTopYKey = "JumpTiming.MapPlaytest.SpawnTopY";
    private const float SpawnClearance = 0.02f;

    static MapPlaytestLauncher()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    public static void StartFromPlatform(Platform2D platform)
    {
        if (platform == null)
        {
            return;
        }

        PlayerController player = FindPlayer();
        if (player == null)
        {
            EditorUtility.DisplayDialog(
                "Player Missing",
                "현재 씬에서 PlayerController를 찾지 못했습니다. Player가 포함된 씬을 열어주세요.",
                "OK");
            return;
        }

        Vector2 topCenter = platform.TopCenter;
        if (Application.isPlaying)
        {
            PlacePlayer(player, topCenter.x, topCenter.y);
            return;
        }

        SessionState.SetBool(PendingKey, true);
        SessionState.SetFloat(SpawnXKey, topCenter.x);
        SessionState.SetFloat(SpawnTopYKey, topCenter.y);
        EditorApplication.isPlaying = true;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
        {
            EditorApplication.delayCall += ApplyPendingSpawn;
        }
    }

    private static void ApplyPendingSpawn()
    {
        if (!SessionState.GetBool(PendingKey, false))
        {
            return;
        }

        SessionState.SetBool(PendingKey, false);
        PlayerController player = FindPlayer();
        if (player == null)
        {
            Debug.LogError("Map Playtest: Play Mode에서 PlayerController를 찾지 못했습니다.");
            return;
        }

        PlacePlayer(
            player,
            SessionState.GetFloat(SpawnXKey, 0f),
            SessionState.GetFloat(SpawnTopYKey, 0f));
    }

    private static PlayerController FindPlayer()
    {
        return Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    }

    private static void PlacePlayer(PlayerController player, float spawnX, float platformTopY)
    {
        player.ApplyJumpTuningNow();

        BoxCollider2D playerCollider = player.GetComponent<BoxCollider2D>();
        float halfHeight = playerCollider != null
            ? playerCollider.bounds.extents.y
            : 0.36f;

        Vector3 spawnPosition = player.transform.position;
        spawnPosition.x = spawnX;
        spawnPosition.y = platformTopY + halfHeight + SpawnClearance;
        player.transform.position = spawnPosition;

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = spawnPosition;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.WakeUp();
        }

        Physics2D.SyncTransforms();
        Selection.activeGameObject = player.gameObject;
    }
}
