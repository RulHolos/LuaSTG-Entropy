using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace LuaSTG.Core.FileSystem;

public class FileSystemArchive : IFileSystem
{
    private readonly object _archiveLock = new();
    private readonly string _archivePath;
    private ZipArchive? _archive;
    private string _password = string.Empty;

    private readonly Dictionary<string, ZipArchiveEntry> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public string GetArchivePath() => _archivePath;

    private FileSystemArchive(string path)
    {
        _archivePath = path.Replace('\\', '/');
    }

    public static bool TryCreateFromFile(string path, out FileSystemArchive? archive)
    {
        var instance = new FileSystemArchive(path);
        if (instance.Open())
        {
            archive = instance;
            return true;
        }

        archive = null;
        return false;
    }

    public bool SetPassword(string password)
    {
        lock (_archiveLock)
        {
            if (_archive == null)
                return false;
            _password = password;
            return true;
        }
    }

    private bool Open()
    {
        try
        {
            var stream = new FileStream(_archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _archive = new ZipArchive(stream, ZipArchiveMode.Read);

            foreach (var entry in _archive.Entries)
            {
                string normalizedPath = NormalizePath(entry.FullName);

                if (normalizedPath.EndsWith('/') || entry.FullName.EndsWith('/') || entry.Length == 0 && string.IsNullOrEmpty(entry.Name))
                {
                    _directories.Add(normalizedPath.TrimEnd('/'));
                }
                else
                {
                    _files[normalizedPath] = entry;
                    PopulateImplicitDirectories(normalizedPath);
                }
            }
            return true;
        }
        catch
        {
            Dispose();
            return false;
        }
    }

    public bool HasNode(string path)
    {
        string normalized = NormalizePath(path);
        return _files.ContainsKey(normalized) || _directories.Contains(normalized);
    }

    public FileSystemNodeType GetNodeType(string path)
    {
        string normalized = NormalizePath(path);
        if (_files.ContainsKey(normalized))
            return FileSystemNodeType.File;
        if (_directories.Contains(normalized))
            return FileSystemNodeType.Directory;
        return FileSystemNodeType.Unknown;
    }

    public bool HasFile(string path) => _files.ContainsKey(NormalizePath(path));

    public bool HasDirectory(string path) => _directories.Contains(NormalizePath(path));

    public long GetFileSize(string path) =>
        _files.TryGetValue(NormalizePath(path), out var entry) ? entry.Length : 0;

    public bool ReadFile(string path, out byte[]? data)
    {
        if (!_files.TryGetValue(NormalizePath(path), out var entry))
        {
            data = null;
            return false;
        }

        lock (_archiveLock)
        {
            try
            {
                using var entryStream = entry.Open();
                using var memoryStream = new MemoryStream();
                entryStream.CopyTo(memoryStream);
                data = memoryStream.ToArray();
                return true;
            }
            catch
            {
                data = null;
                return false;
            }
        }
    }

    public IEnumerable<string> EnumerateNodes(string directory, bool recursive)
    {
        string targetDir = NormalizePath(directory);
        if (!string.IsNullOrEmpty(targetDir) && !targetDir.EndsWith('/'))
        {
            targetDir += "/";
        }

        List<string> allPaths;
        lock (_archiveLock)
        {
            allPaths = [.. _files.Keys, .. _directories.Select(d => d + "/")];
        }

        foreach (string path in allPaths)
        {
            if (!path.StartsWith(targetDir, StringComparison.OrdinalIgnoreCase) || path == targetDir)
                continue;

            string relativePart = path[targetDir.Length..];

            if (!recursive)
            {
                int firstSlash = relativePart.IndexOf('/');
                if (firstSlash != -1 && firstSlash != relativePart.Length - 1)
                    continue;
            }

            yield return path;
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        return path.Replace('\\', '/').Trim('/');
    }

    private void PopulateImplicitDirectories(string filePath)
    {
        int slashIdx = filePath.IndexOf('/');
        while (slashIdx != -1)
        {
            _directories.Add(filePath[..slashIdx]);
            slashIdx = filePath.IndexOf('/', slashIdx + 1);
        }
    }

    public void Dispose()
    {
        lock (_archiveLock)
        {
            _archive?.Dispose();
            _archive = null;
            _files.Clear();
            _directories.Clear();
        }
    }

    ~FileSystemArchive() => Dispose();
}
