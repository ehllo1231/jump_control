#if UNITY_ANDROID
using System;
using System.IO;
using UnityEditor.Android;

public sealed class AndroidLibraryNamespacePostprocessor : IPostGenerateGradleAndroidProject
{
    private const string LibraryModuleName = "JumpTimingVibration.androidlib";
    private const string NamespaceLine = "    namespace 'com.jumptiming.vibration'";

    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string gradleFilePath = FindGradleFile(path);
        if (string.IsNullOrEmpty(gradleFilePath) || !File.Exists(gradleFilePath))
        {
            return;
        }

        string contents = File.ReadAllText(gradleFilePath);
        if (contents.Contains("namespace "))
        {
            return;
        }

        const string androidBlock = "android {";
        int androidBlockIndex = contents.IndexOf(androidBlock, StringComparison.Ordinal);
        if (androidBlockIndex < 0)
        {
            return;
        }

        string newline = contents.Contains("\r\n") ? "\r\n" : "\n";
        int insertIndex = androidBlockIndex + androidBlock.Length;
        string updatedContents = contents.Insert(insertIndex, newline + NamespaceLine);
        File.WriteAllText(gradleFilePath, updatedContents);
    }

    private static string FindGradleFile(string path)
    {
        string directPath = Path.Combine(path, LibraryModuleName, "build.gradle");
        if (File.Exists(directPath))
        {
            return directPath;
        }

        string nestedPath = Path.Combine(path, "unityLibrary", LibraryModuleName, "build.gradle");
        if (File.Exists(nestedPath))
        {
            return nestedPath;
        }

        DirectoryInfo parent = Directory.GetParent(path);
        if (parent == null)
        {
            return string.Empty;
        }

        string siblingUnityLibraryPath = Path.Combine(parent.FullName, "unityLibrary", LibraryModuleName, "build.gradle");
        return File.Exists(siblingUnityLibraryPath) ? siblingUnityLibraryPath : string.Empty;
    }
}
#endif
