using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class PlaytestLogger : MonoBehaviour
{
    public const string AutoLoggingEnabledPlayerPrefsKey = "JumpTiming.PlaytestLogger.AutoLoggingEnabled";
    public const string FallYThresholdPlayerPrefsKey = "JumpTiming.PlaytestLogger.FallYThreshold";
    public const float DefaultFallYThreshold = -8f;

    [Header("Recording")]
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private bool respectGlobalAutoLoggingSetting = true;

    [Header("Movement Samples")]
    [SerializeField, Min(0.02f)] private float sampleInterval = 0.08f;
    [SerializeField, Min(0f)] private float minimumSampleDistance = 0.03f;

    [Header("Fall Event")]
    [SerializeField] private bool useConfiguredFallYThreshold = true;
    [SerializeField] private float fallYThreshold = DefaultFallYThreshold;
    [SerializeField, Min(0f)] private float fallResetMargin = 1f;

    private PlayerController player;
    private Rigidbody2D body;
    private StreamWriter writer;
    private string logPath;
    private float nextSampleTime;
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
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.JumpExecuted -= HandleJumpExecuted;
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
            string sceneName = activeScene.IsValid() ? activeScene.name : "UnknownScene";
            string fileName = string.Format(
                "{0}_{1}{2}",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                PlaytestLogPaths.SanitizeFileNamePart(sceneName),
                PlaytestLogPaths.LogFileExtension);
            logPath = Path.Combine(logDirectory, fileName);
            writer = new StreamWriter(logPath, false, Encoding.UTF8);
            sessionOpen = true;
            nextSampleTime = 0f;
            hasLastSamplePosition = false;
            fallRecorded = false;

            WriteRecord(new PlaytestLogRecord
            {
                type = PlaytestLogRecordTypes.SessionStart,
                time = Time.timeSinceLevelLoad,
                scene = sceneName,
                timestamp = DateTime.Now.ToString("o"),
                player = player != null ? player.name : name
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
                    scene = SceneManager.GetActiveScene().name,
                    timestamp = DateTime.Now.ToString("o"),
                    player = player != null ? player.name : name
                });
            }
        }
        finally
        {
            sessionOpen = false;
            writer?.Flush();
            writer?.Dispose();
            writer = null;
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
        float threshold = useConfiguredFallYThreshold ? ConfiguredFallYThreshold : fallYThreshold;
        if (!fallRecorded && position.y <= threshold)
        {
            fallRecorded = true;
            WritePositionRecord(PlaytestLogRecordTypes.Fall, position, flushImmediately: true);
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
            scene = SceneManager.GetActiveScene().name,
            timestamp = DateTime.Now.ToString("o"),
            player = source.name,
            x = position.x,
            y = position.y,
            angle = source.LastJumpAngle,
            power = source.LockedPower,
            impulseX = impulse.x,
            impulseY = impulse.y
        }, flushImmediately: true);
    }

    private void WritePositionRecord(string type, Vector2 position, bool flushImmediately = false)
    {
        WriteRecord(new PlaytestLogRecord
        {
            type = type,
            time = Time.timeSinceLevelLoad,
            scene = SceneManager.GetActiveScene().name,
            timestamp = DateTime.Now.ToString("o"),
            player = player != null ? player.name : name,
            x = position.x,
            y = position.y
        }, flushImmediately);
    }

    private void WriteRecord(PlaytestLogRecord record, bool flushImmediately = false)
    {
        if (!sessionOpen || writer == null || record == null)
        {
            return;
        }

        try
        {
            writer.WriteLine(JsonUtility.ToJson(record));
            if (flushImmediately)
            {
                writer.Flush();
            }
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
        fallResetMargin = Mathf.Max(0f, fallResetMargin);
    }
}
