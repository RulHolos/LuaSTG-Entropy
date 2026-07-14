using LuaSTG.Core.Debugger;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace LuaSTG.Core.FileSystem;

public readonly record struct GeneratedFile(string Name, byte[] Data);

public class EmbeddedFileSystem : IFileSystem
{
    public static EmbeddedFileSystem Instance { get; } = new();

    private readonly Dictionary<string, byte[]> _files;
    private readonly HashSet<string> _directories;

    private EmbeddedFileSystem()
    {
        var sourceFiles = GetGeneratedFiles();

        _files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        _directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in sourceFiles)
        {
            string normalizedPath = NormalizePath(file.Name);

            if (normalizedPath.EndsWith('/'))
            {
                _directories.Add(normalizedPath);
            }
            else
            {
                _files[normalizedPath] = file.Data;
                PopulateImplicitDirectories(normalizedPath);
            }
        }
    }

    public bool HasNode(string path)
    {
        string normalized = NormalizePath(path);
        return _files.ContainsKey(normalized) || _directories.Contains(normalized) || _directories.Contains(normalized + "/");
    }

    public FileSystemNodeType GetNodeType(string path)
    {
        string normalized = NormalizePath(path);
        if (_files.ContainsKey(normalized))
            return FileSystemNodeType.File;
        if (_directories.Contains(normalized) || _directories.Contains(normalized + "/"))
            return FileSystemNodeType.Directory;
        return FileSystemNodeType.Unknown;
    }

    public bool HasFile(string path) => _files.ContainsKey(NormalizePath(path));

    public bool HasDirectory(string path)
    {
        string normalized = NormalizePath(path);
        return _directories.Contains(normalized) || _directories.Contains(normalized + "/");
    }

    public long GetFileSize(string path)
    {
        return _files.TryGetValue(NormalizePath(path), out var data) ? data.Length : 0;
    }

    public bool ReadFile(string path, out byte[]? data)
    {
        if (_files.TryGetValue(NormalizePath(path), out var originalData))
        {
            var visualData = new byte[originalData.Length];
            Buffer.BlockCopy(originalData, 0, visualData, 0, originalData.Length);

            ApplyMask(visualData);

            data = visualData;
            return true;
        }

        data = null;
        return false;
    }

    public IEnumerable<string> EnumerateNodes(string directory, bool recursive)
    {
        string targetDir = NormalizePath(directory);
        if (!string.IsNullOrEmpty(targetDir) && !targetDir.EndsWith('/'))
        {
            targetDir += "/";
        }

        var allNodes = _files.Keys.Concat(_directories);

        foreach (string node in allNodes)
        {
            if (!node.StartsWith(targetDir, StringComparison.OrdinalIgnoreCase) || node == targetDir)
                continue;

            string relativePart = node[targetDir.Length..];

            if (!recursive)
            {
                int firstSlash = relativePart.IndexOf('/');
                if (firstSlash != -1 && firstSlash != relativePart.Length - 1)
                    continue;
            }

            yield return node;
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

    private static void ApplyMask(Span<byte> data)
    {
        ReadOnlySpan<byte> mask = "lstg"u8;

        for (int i = 0; i < data.Length; i++)
            data[i] ^= mask[i % 4];
    }

    private static GeneratedFile[] GetGeneratedFiles()
    {
        List<string> filesToLoad = ["math.lua", "io.lua", "main.lua"];
        List<GeneratedFile> files = [];

        var assembly = Assembly.GetExecutingAssembly();

        foreach (string file in filesToLoad)
        {
            var resourceName = assembly.GetManifestResourceNames()
                //Namespace of the embedded resource. Don't include it in the real name.
                .FirstOrDefault(name => name.Equals("LuaSTG.Core.FileSystem.LuaScripts." + file, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                throw new FileNotFoundException($"Missing an embedded script: {file}");

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                continue;

            byte[] rawBytes = new byte[stream.Length];
            stream.ReadExactly(rawBytes, 0, rawBytes.Length);

            ApplyMask(rawBytes);

            files.Add(new("luastg/" + file, rawBytes));
        }

        return [.. files];
    }
}