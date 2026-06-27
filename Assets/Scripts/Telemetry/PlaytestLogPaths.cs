using System.IO;
using System.Text;
using UnityEngine;

public static class PlaytestLogPaths
{
    public const string LogDirectoryName = "PlaytestLogs";
    public const string LogFileExtension = ".jsonl";

    public static string GetLogDirectory()
    {
#if UNITY_EDITOR
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, LogDirectoryName);
#else
        return Path.Combine(Application.persistentDataPath, LogDirectoryName);
#endif
    }

    public static string SanitizeFileNamePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Untitled";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char candidate = value[i];
            bool invalid = false;
            for (int invalidIndex = 0; invalidIndex < invalidChars.Length; invalidIndex++)
            {
                if (candidate == invalidChars[invalidIndex])
                {
                    invalid = true;
                    break;
                }
            }

            builder.Append(invalid ? '_' : candidate);
        }

        return builder.ToString();
    }
}
