using System;

[Serializable]
public sealed class PlaytestLogRecord
{
    public string type;
    public float time;
    public string scene;
    public string timestamp;
    public string player;
    public float x;
    public float y;
    public float angle;
    public float power;
    public float impulseX;
    public float impulseY;
}

public static class PlaytestLogRecordTypes
{
    public const string SessionStart = "session_start";
    public const string SessionEnd = "session_end";
    public const string Sample = "sample";
    public const string PathBreak = "path_break";
    public const string Jump = "jump";
    public const string Landing = "landing";
    public const string Fall = "fall";
}
