using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace LuaSTG.Core.FileSystem;

public enum FileSystemNodeType { Unknown, File, Directory, }
public enum ResourceSchema : byte { Unknown, Resource, User }

public readonly record struct ResourceLocation(ResourceSchema Schema, string FileSystem, string Path);
public readonly record struct ResolvedLocation(IFileSystem FileSystem, string Path);

public interface IFileSystem
{
    bool HasNode(string path);
    bool HasFile(string path);
    bool HasDirectory(string path);
    long GetFileSize(string path);
    bool ReadFile(string path, out byte[] data);
    FileSystemNodeType GetNodeType(string path);
    IEnumerable<string> EnumerateNodes(string directory, bool recursive);
}

internal readonly record struct NamedFileSystem(string Name, IFileSystem FileSystem);

public static class FileSystemManager
{
    private static ImmutableList<NamedFileSystem> _fileSystems = [];
    private static ImmutableList<string> _searchPaths = [];

    private static readonly object _writeLock = new();

    public static ResourceLocation ParseLocation(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return default;

        ResourceSchema schema = ResourceSchema.Resource;
        ReadOnlySpan<char> span = uri.AsSpan();
        bool isAbsoluteSchema = false;

        if (span.StartsWith("resource://", StringComparison.OrdinalIgnoreCase))
        {
            span = span["resource://".Length..];
            isAbsoluteSchema = true;
        }
        else if (span.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            span = span["res://".Length..];
            isAbsoluteSchema = true;
        }
        else if (span.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
        {
            schema = ResourceSchema.User;
            span = span["user://".Length..];
            isAbsoluteSchema = true;
        }

        string fileSystem = string.Empty;
        string path = span.ToString();

        if (schema == ResourceSchema.Resource && isAbsoluteSchema)
        {
            int delimiterIdx = span.IndexOfAny('/', '\\');
            if (delimiterIdx != 1)
            {
                fileSystem = span[..delimiterIdx].ToString();
                path = span[(delimiterIdx + 1)..].ToString();
            }
        }

        return new ResourceLocation(schema, fileSystem, path);
    }

    private static string NormalizeAndCombine(string baseDir, string subPath) => Path.Combine(baseDir, subPath).Replace('\\', '/');

    private static ResolvedLocation Resolve(ResourceLocation location)
    {
        if (location.Schema is ResourceSchema.Unknown)
            return default;

        var currentFileSystems = _fileSystems;
        var currentSearchPaths = _searchPaths;

        if (location.Schema is ResourceSchema.Resource)
        {
            foreach (var v in currentFileSystems)
            {
                if (!string.IsNullOrEmpty(location.FileSystem) && v.Name != location.FileSystem)
                    continue;

                for (int i = currentSearchPaths.Count - 1; i >= 0; i--)
                {
                    string p = NormalizeAndCombine(currentSearchPaths[i], location.Path);
                    if (p.Contains(".."))
                        continue;

                    if (v.FileSystem.HasNode(p))
                        return new ResolvedLocation(v.FileSystem, p);
                }

                if (v.FileSystem.HasNode(location.Path))
                    return new ResolvedLocation(v.FileSystem, location.Path);
            }
        }

        if (FileSystemOS.Instance is { } osFileSystem)
        {
            for (int i = currentSearchPaths.Count - 1; i >= 0; i--)
            {
                string p = NormalizeAndCombine(currentSearchPaths[i], location.Path);
                if (osFileSystem.HasNode(p))
                    return new ResolvedLocation(osFileSystem, p);
            }

            if (osFileSystem.HasNode(location.Path))
                return new ResolvedLocation(osFileSystem, location.Path);
        }

        return default;
    }

    public static void AddFileSystem(string name, IFileSystem fileSystem)
    {
        if (string.IsNullOrEmpty(name) || fileSystem == null)
            return;

        lock (_writeLock)
        {
            if (_fileSystems.Any(f => f.Name == name))
                return;
            _fileSystems = _fileSystems.Add(new NamedFileSystem(name, fileSystem));
        }
    }

    public static bool HasFileSystem(string name) => _fileSystems.Any(f => f.Name == name);

    public static void RemoveFileSystem(string name)
    {
        lock (_writeLock)
        {
            _fileSystems = _fileSystems.RemoveAll(f => f.Name == name);
        }
    }

    public static void RemoveFileSystem(IFileSystem fileSystem)
    {
        lock (_writeLock)
        {
            _fileSystems = _fileSystems.RemoveAll(f => f.FileSystem == fileSystem);
        }
    }

    public static void RemoveAllFileSystems()
    {
        lock (_writeLock)
        {
            _fileSystems = _fileSystems.Clear();
        }
    }

    public static IEnumerable<IFileSystem> GetFileSystems() => _fileSystems.Select(f => f.FileSystem);

    public static bool TryGetArchiveByPath(string path, out FileSystemArchive? archive)
    {
        archive = _fileSystems
            .Select(f => f.FileSystem)
            .OfType<FileSystemArchive>()
            .FirstOrDefault(arch => arch.GetArchivePath() == path);

        return archive != null;
    }

    public static void AddSearchPath(string path)
    {
        lock (_writeLock)
        {
            if (_searchPaths.Contains(path))
                return;
            _searchPaths = _searchPaths.Add(path);
        }
    }

    public static bool HasSearchPath(string path) => _searchPaths.Contains(path);

    public static void RemoveSearchPath(string path)
    {
        lock (_writeLock)
        {
            _searchPaths = _searchPaths.RemoveAll(p => p == path);
        }
    }

    public static void RemoveAllSearchPaths()
    {
        lock (_writeLock)
        {
            _searchPaths = _searchPaths.Clear();
        }
    }

    public static bool HasNode(string name) => Resolve(ParseLocation(name)).FileSystem != null;

    public static FileSystemNodeType GetNodeType(string name) =>
        Resolve(ParseLocation(name)) is var res && res.FileSystem != null
            ? res.FileSystem.GetNodeType(res.Path)
            : FileSystemNodeType.Unknown;

    public static bool HasFile(string name) =>
        Resolve(ParseLocation(name)) is var res && res.FileSystem != null && res.FileSystem.HasFile(res.Path);

    public static long GetFileSize(string name) =>
        Resolve(ParseLocation(name)) is var res && res.FileSystem != null
            ? res.FileSystem.GetFileSize(res.Path)
            : 0;

    public static bool ReadFile(string name, out byte[]? data)
    {
        var res = Resolve(ParseLocation(name));
        if (res.FileSystem == null)
        {
            data = null;
            return false;
        }
        return res.FileSystem.ReadFile(res.Path, out data);
    }

    public static bool ReadFile(string name, out string? data)
    {
        var res = Resolve(ParseLocation(name));
        if (res.FileSystem == null)
        {
            data = null;
            return false;
        }
        bool ret = res.FileSystem.ReadFile(res.Path, out byte[] str);
        data = Encoding.UTF8.GetString(str);
        return ret;
    }

    public static bool HasDirectory(string name) =>
        Resolve(ParseLocation(name)) is var res && res.FileSystem != null && res.FileSystem.HasDirectory(res.Path);

    public static bool WriteFile(string name, ReadOnlySpan<byte> data)
    {
        try
        {
            File.WriteAllBytes(name, data.ToArray());
            return true;
        }
        catch
        {
            return false;
        }
    }
}
