using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class PlaytestLogger : MonoBehaviour
{
    private const int WriterBufferSize = 65536;
    public const string AutoLoggingEnabledPlayerPrefsKey = "JumpTiming.PlaytestLogger.AutoLoggingEnabled";
    public const string FallYThresholdPlayerPrefsKey = "JumpTiming.PlaytestLogger.FallYThreshold";
    public const float DefaultFallYThreshold = -8f;

    [Header("Recording")]
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private bool respectGlobalAutoLoggingSetting = true;

    [Header("Movement Samples")]
    [SerializeField, Min(0.02f)] private float sampleInterval = 0.08f;
    [SerializeField, Min(0f)] private float minimumSampleDistance = 0.03f;

    [Header("File Buffer")]
    [SerializeField, Min(0.5f)] private float flushInterval = 2f;
    [SerializeField, Min(1)] private int maxBufferedRecords = 64;

    [Header("Fall Event")]
    [SerializeField] private bool useConfiguredFallYThreshold = true;
    [SerializeField] private float fallYThreshold = DefaultFallYThreshold;
    [SerializeField, Min(0f)] private float fallResetMargin = 1f;

    private PlayerController player;
    private Rigidbody2D body;
    private StreamWriter writer;
    private string logPath;
    private string sessionSceneName;
    private string sessionPlayerName;
    private float nextSampleTime;
    private float nextFlushTime;
    private int bufferedRecordCount;
    private bool hasLastSamplePosition;
    private Vector2 lastSamplePosition;
    private bool fallRecorded;
    private bool sessionOpen;

    public string LogPath => logPath;

    public static bool AutoLoggingEnabled
    {
        get => PlayerPrefs.GetInt(AutoLoggingEnabledPlayerPrefsKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(AutoLoggingEnabledPlayerPrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static float ConfiguredFallYThreshold
    {
        get => PlayerPrefs.GetFloat(FallYThresholdPlayerPrefsKey, DefaultFallYThreshold);
        set
        {
            PlayerPrefs.SetFloat(FallYThresholdPlayerPrefsKey, value);
            PlayerPrefs.Save();
        }
    }

    private void Awake()
    {
        CacheReferences();
        if (useConfiguredFallYThreshold)
        {
            fallYThreshold = ConfiguredFallYThreshold;
        }
    }

    private void OnEnable()
    {
        CacheReferences();
        if (player != null)
        {
            player.JumpExecuted += HandleJumpExecuted;
            player.Landed += HandleLanded;
            player.DebugJumpHistoryMoved += HandleDebugJumpHistoryMoved;
        }
    }

    private void Start()
    {
        if (!enableLogging || (respectGlobalAutoLoggingSetting && !AutoLoggingEnabled))
        {
            enabled = false;
            return;
        }

        StartSession();
    }

    private void LateUpdate()
    {
        if (!sessionOpen)
        {
            return;
        }

        RecordSampleIfNeeded();
        RecordFallIfNeeded();
        FlushBufferedRecordsIfNeeded();
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.JumpExecuted -= HandleJumpExecuted;
            player.Landed -= HandleLanded;
            player.DebugJumpHistoryMoved -= HandleDebugJumpHistoryMoved;
        }

        EndSession();
    }

    private void OnApplicationQuit()
    {
        EndSession();
    }

    private void StartSession()
    {
        if (sessionOpen)
        {
            return;
        }

        try
        {
            string logDirectory = PlaytestLogPaths.GetLogDirectory();
            Directory.CreateDirectory(logDirectory);

            Scene activeScene = SceneManager.GetActiveScene();
            sessionSceneName = activeScene.IsValid() ? activeScene.name : "UnknownScene";
            sessionPlayerName = player != null ? player.name : name;
            string fileName = string.Format(
                "{0}_{1}{2}",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                PlaytestLogPaths.SanitizeFileNamePart(sessionSceneName),
                PlaytestLogPaths.LogFileExtension);
            logPath = Path.Combine(logDirectory, fileName);
            writer = new StreamWriter(logPath, false, Encoding.UTF8, WriterBufferSize);
            sessionOpen = true;
            nextSampleTime = 0f;
            nextFlushTime = Time.realtimeSinceStartup + Mathf.Max(0.5f, flushInterval);
            bufferedRecordCount = 0;
            hasLastSamplePosition = false;
            fallRecorded = false;

            WriteRecord(new PlaytestLogRecord
            {
                type = PlaytestLogRecordTypes.SessionStart,
                time = Time.timeSinceLevelLoad,
                scene = sessionSceneName,
                timestamp = DateTime.Now.ToString("o"),
                player = sessionPlayerName
            });
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Playtest Logger: 로그 파일을 만들지 못했습니다. {exception.Message}");
            EndSession();
            enabled = false;
        }
    }

    private void EndSession()
    {
        if (!sessionOpen && writer == null)
        {
            return;
        }

        try
        {
            if (sessionOpen)
            {
                WriteRecord(new PlaytestLogRecord
                {
                    type = PlaytestLogRecordTypes.SessionEnd,
                    time = Time.timeSinceLevelLoad,
                    scene = sessionSceneName,
                    timestamp = DateTime.Now.ToString("o"),
                    player = sessionPlayerName
                });
            }

            writer?.Flush();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Playtest Logger: 남은 로그를 저장하지 못했습니다. {exception.Message}");
        }
        finally
        {
            sessionOpen = false;
            try
            {
                writer?.Dispose();
            }
            catch (Exception)
            {
                // Gameplay shutdown must continue even when telemetry cleanup fails.
            }

            writer = null;
            bufferedRecordCount = 0;
        }
    }

    private void RecordSampleIfNeeded()
    {
        if (Time.time < nextSampleTime)
        {
            return;
        }

        nextSampleTime = Time.time + Mathf.Max(0.02f, sampleInterval);
        Vector2 position = GetCurrentPosition();
        if (hasLastSamplePosition)
        {
            float minimumDistanceSqr = minimumSampleDistance * minimumSampleDistance;
            if ((position - lastSamplePosition).sqrMagnitude < minimumDistanceSqr)
            {
                return;
            }
        }

        hasLastSamplePosition = true;
        lastSamplePosition = position;
        WritePositionRecord(PlaytestLogRecordTypes.Sample, position);
    }

    private void RecordFallIfNeeded()
    {
        Vector2 position = GetCurrentPosition();
        float threshold = fallYThreshold;
        if (!fallRecorded && position.y <= threshold)
        {
            fallRecorded = true;
            WritePositionRecord(PlaytestLogRecordTypes.Fall, position);
            return;
        }

        if (fallRecorded && position.y >= threshold + fallResetMargin)
        {
            fallRecorded = false;
        }
    }

    private void HandleJumpExecuted(PlayerController source)
    {
        if (!sessionOpen || source == null)
        {
            return;
        }

        Vector2 position = GetCurrentPosition();
        Vector2 impulse = source.LastJumpVector;
        WriteRecord(new PlaytestLogRecord
        {
            type = PlaytestLogRecordTypes.Jump,
            time = Time.timeSinceLevelLoad,
            scene = sessionSceneName,
            timestamp = DateTime.Now.ToString("o"),
            player = sessionPlayerName,
            x = position.x,
            y = position.y,
            angle = source.LastJumpAngle,
            power = source.LockedPower,
            impulseX = impulse.x,
            impulseY = impulse.y
        });
    }

    private void HandleLanded(PlayerController source)
    {
        if (!sessionOpen || source == null)
        {
            return;
        }

        Vector2 position = GetCurrentPosition();
        Vector2 impulse = source.LastJumpVector;
        WriteRecord(new PlaytestLogRecord
        {
            type = PlaytestLogRecordTypes.Landing,
            time = Time.timeSinceLevelLoad,
            scene = sessionSceneName,
            timestamp = DateTime.Now.ToString("o"),
            player = sessionPlayerName,
            x = position.x,
            y = position.y,
            angle = source.LastJumpAngle,
            power = source.LockedPower,
            impulseX = impulse.x,
            impulseY = impulse.y
        });
    }

    private void HandleDebugJumpHistoryMoved(PlayerController source)
    {
        if (!sessionOpen || source == null)
        {
            return;
        }

        hasLastSamplePosition = false;
        nextSampleTime = 0f;
        WritePositionRecord(PlaytestLogRecordTypes.PathBreak, GetCurrentPosition());
    }

    private void WritePositionRecord(string type, Vector2 position)
    {
        WriteRecord(new PlaytestLogRecord
        {
            type = type,
            time = Time.timeSinceLevelLoad,
            scene = sessionSceneName,
            timestamp = DateTime.Now.ToString("o"),
            player = sessionPlayerName,
            x = position.x,
            y = position.y
        });
    }

    private void WriteRecord(PlaytestLogRecord record)
    {
        if (!sessionOpen || writer == null || record == null)
        {
            return;
        }

        try
        {
            writer.WriteLine(JsonUtility.ToJson(record));
            bufferedRecordCount++;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Playtest Logger: 로그 기록을 중단합니다. {exception.Message}");
            AbortSession();
            enabled = false;
        }
    }

    private void FlushBufferedRecordsIfNeeded()
    {
        if (!sessionOpen || writer == null || bufferedRecordCount <= 0)
        {
            return;
        }

        bool reachedRecordLimit = bufferedRecordCount >= Mathf.Max(1, maxBufferedRecords);
        if (!reachedRecordLimit && Time.realtimeSinceStartup < nextFlushTime)
        {
            return;
        }

        try
        {
            writer.Flush();
            bufferedRecordCount = 0;
            nextFlushTime = Time.realtimeSinceStartup + Mathf.Max(0.5f, flushInterval);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Playtest Logger: 로그 기록을 중단합니다. {exception.Message}");
            AbortSession();
            enabled = false;
        }
    }

    private void AbortSession()
    {
        sessionOpen = false;
        try
        {
            writer?.Flush();
            writer?.Dispose();
        }
        catch (Exception)
        {
            // Ignore close failures; gameplay must not depend on telemetry cleanup.
        }

        writer = null;
        bufferedRecordCount = 0;
    }

    private Vector2 GetCurrentPosition()
    {
        return body != null ? body.position : (Vector2)transform.position;
    }

    private void CacheReferences()
    {
        if (player == null)
        {
            player = GetComponent<PlayerController>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void OnValidate()
    {
        sampleInterval = Mathf.Max(0.02f, sampleInterval);
        minimumSampleDistance = Mathf.Max(0f, minimumSampleDistance);
        flushInterval = Mathf.Max(0.5f, flushInterval);
        maxBufferedRecords = Mathf.Max(1, maxBufferedRecords);
        fallResetMargin = Mathf.Max(0f, fallResetMargin);
    }
}
