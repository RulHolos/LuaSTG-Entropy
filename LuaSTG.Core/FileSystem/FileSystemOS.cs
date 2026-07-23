using System;
using System.Collections.Generic;
using System.Text;
using LuaSTG.Core.Debugger;

namespace LuaSTG.Core.FileSystem;

public class FileSystemOS : IFileSystem
{
    public static FileSystemOS Instance { get; } = new();

    private FileSystemOS() { }

    private static string Normalize(string path) => path.Replace('\\', '/');

    public bool HasNode(string path) => HasFile(path) || HasDirectory(path);

    public FileSystemNodeType GetNodeType(string path)
    {
        if (HasFile(path))
            return FileSystemNodeType.File;
        if (HasDirectory(path))
            return FileSystemNodeType.Directory;
        return FileSystemNodeType.Unknown;
    }

    public bool HasFile(string path) => File.Exists(path);

    public bool HasDirectory(string path) => Directory.Exists(path);

    public long GetFileSize(string path)
    {
        try
        {
            return HasFile(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    public bool ReadFile(string path, out byte[]? data)
    {
        if (!VerifyFilePathCase(path, out string correctCasePath))
        {
            Logger.core.Error($"Case difference in file paths. Provided: '{path}', Expected: '{correctCasePath}'");
            data = null;
            return false;
        }

        try
        {
            data = File.ReadAllBytes(path);
            return true;
        }
        catch
        {
            data = null;
            return false;
        }
    }

    public IEnumerable<string> EnumerateNodes(string directory, bool recursive)
    {
        string targetDir = string.IsNullOrEmpty(directory) ? "." : directory;

        if (!Directory.Exists(targetDir))
            yield break;

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        IEnumerable<string> entries;

        try
        {
            entries = Directory.EnumerateFileSystemEntries(targetDir, "*", searchOption);
        }
        catch
        {
            yield break;
        }

        foreach (string entry in entries)
        {
            string normalized = entry.Replace('\\', '/');

            if (Directory.Exists(entry) && !normalized.EndsWith('/'))
            {
                normalized += "/";
            }

            yield return normalized;
        }
    }

    private static bool VerifyFilePathCase(string path, out string correctCasePath)
    {
        bool found = ResolveActualPath(path, out correctCasePath);
        if (!found)
        {
            correctCasePath = path;
            return false;
        }

        return string.Equals(path, correctCasePath, StringComparison.Ordinal);
    }

    private static bool ResolveActualPath(string path, out string actualPath)
    {
        actualPath = path;
        string norm = Normalize(path);

        if (File.Exists(norm) || Directory.Exists(norm))
        {
            actualPath = norm;
            return true;
        }

        try
        {
            string fullPath = Path.GetFullPath(norm);
            string root = Path.GetPathRoot(fullPath) ?? "/";
            string relative = fullPath[root.Length..];
            
            string[] segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string current = root;

            var enumOptions = new EnumerationOptions
            {
                MatchCasing = MatchCasing.CaseInsensitive,
                RecurseSubdirectories = false
            };

            foreach (var segment in segments)
            {
                var dirInfo = new DirectoryInfo(current);
                if (!dirInfo.Exists)
                    return false;

                var match = dirInfo.GetFileSystemInfos(segment, enumOptions).FirstOrDefault();
                if (match == null)
                    return false;

                current = match.FullName;
            }

            actualPath = Normalize(current);
            return File.Exists(actualPath) || Directory.Exists(actualPath);
        }
        catch
        {
            return false;
        }
    }
}
