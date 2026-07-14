using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.FileSystem;

public class FileSystemOS : IFileSystem
{
    public static FileSystemOS Instance { get; } = new();

    static FileSystemOS()
    {
        FileSystemOS.Instance = Instance;
    }

    private FileSystemOS() { }

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
            //TODO: Transfer Logger to Core instead of Entropy
            Console.WriteLine($"Error: Case difference in file paths. Provided: '{path}', Expected: '{correctCasePath}'");
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
        correctCasePath = path;
        if (!File.Exists(path) && !Directory.Exists(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var current = new DirectoryInfo(fullPath);

            while (current.Parent != null)
            {
                var match = current.Parent.GetFileSystemInfos(current.Name).FirstOrDefault();
                if (match == null || match.Name != current.Name)
                {
                    correctCasePath = match != null
                        ? Path.Combine(current.Parent.FullName, match.Name).Replace('\\', '/')
                        : fullPath.Replace('\\', '/');
                    return false;
                }
                current = current.Parent;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
