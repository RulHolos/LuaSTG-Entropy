using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Configuration;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.FileSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding;

public unsafe partial class LW_FileManager : ILuaBinding
{
    private sealed class EnumFilesConfig
    {
        public string SearchPath = "";
        public string SearchPath2 = "";
        public int HeadLen;
        public string ExtPath = "";
        public string PackName = "";
        public int Index = 1;
        public bool CheckExt;
        public bool CheckPack;
        public bool FindFiles;
    }

    #region Functions

    [LuaBind]
    public static int LoadArchive(LuaState L)
    {
        LuaStack ctx = new(L);
        string path = ctx.GetValue<string>(1);

        if (!FileSystemArchive.TryCreateFromFile(path, out FileSystemArchive? arc))
        {
            Logger.luastg.Error($"Unable to load resource pack '{path}'; file does not exist or is not a valid resource pack format (zip).");
            ctx.PushNil();
            return 1;
        }

        if (ctx.IsString(3))
        {
            string password = ctx.GetValue<string>(3);
            arc!.SetPassword(password);
        }
        else if (ctx.IsString(2))
        {
            string password = ctx.GetValue<string>(3);
            arc!.SetPassword(password);
        }

        FileSystemManager.AddFileSystem(path, arc!);

        LW_Archive.CreateAndPush(L, arc);
        return 1;
    }

    [LuaBind]
    public static int LoadPackSub(LuaState L)
    {
        var stack = new LuaStack(L);
        try
        {
            var path = stack.GetValue<string>(1);

            if (!FileSystemArchive.TryCreateFromFile(path, out var archive) || archive is null)
            {
                stack.PushNil();
                return 1;
            }

            archive.SetPassword(ConfigurationLoader.Instance.Window.Title ?? "No Title");
            FileSystemManager.AddFileSystem(path, archive);

            LW_Archive.CreateAndPush(L, archive);
            return 1;
        }
        catch (Exception ex)
        {
            stack.RaiseError($"lstg.LoadPackSub: {ex.Message}");
            return 0;
        }
    }

    [LuaBind]
    public static int UnloadArchive(LuaState L)
    {
        LuaStack ctx = new(L);
        string name = ctx.GetValue<string>(1);

        if (FileSystemManager.TryGetArchiveByPath(name, out var arc))
        {
            FileSystemManager.RemoveFileSystem(arc!);
            ctx.Push<bool>(true);
        }
        else
            ctx.Push<bool>(false);

        return 1;
    }

    [LuaBind]
    public static int UnloadAllArchive(LuaState L)
    {
        FileSystemManager.RemoveAllFileSystems();
        return 0;
    }

    [LuaBind]
    public static int ArchiveExist(LuaState L)
    {
        LuaStack ctx = new(L);
        string name = ctx.GetValue<string>(1);
        if (FileSystemManager.TryGetArchiveByPath(name, out var arc))
            ctx.Push<bool>(true);
        else
            ctx.Push<bool>(false);

        return 1;
    }

    [LuaBind]
    public static int GetArchive(LuaState L)
    {
        LuaStack ctx = new(L);
        string name = ctx.GetValue<string>(1);
        if (FileSystemManager.TryGetArchiveByPath(name, out var arc))
            LW_Archive.CreateAndPush(L, arc);
        else
            lua_pushnil(L);

        return 1;
    }

    [LuaBind]
    public static int EnumArchives(LuaState L)
    {
        LuaStack ctx = new(L);

        try
        {
            StackIndex resultTable = ctx.CreateArray();
            var i = 1;
            foreach (var archive in FileSystemManager.GetFileSystems().OfType<FileSystemArchive>())
            {
                var entryTable = ctx.CreateArray();
                ctx.SetArrayValue(entryTable, 1, archive.GetArchivePath());
                ctx.SetArrayValue(entryTable, 2, 0); // legacy "priority" slot kept for compat.
                ctx.SetArrayValue(resultTable, i, entryTable);
                ctx.PopValue();
                i++;
            }
            return 1;
        }
        catch (Exception ex)
        {
            ctx.RaiseError($"lstg.FileManager.EnumArchives: {ex.Message}");
            return 0;
        }
    }

    private static EnumFilesConfig InitEnumFiles(LuaState L, bool findFilesMode)
    {
        LuaStack ctx = new(L);

        string? searchPath = ctx.GetValue<string>(1).Replace('\\', '/');
        int headLen = 0;
        if (searchPath.Length > 0 && searchPath[^1] != '/')
            searchPath += "/";
        else if (searchPath.Length == 0)
        {
            searchPath = ".";
            headLen = 2;
        }

        string? searchPath2 = ctx.GetValue<string>(1).Replace('\\', '/');
        if (searchPath2.Length > 0 && searchPath2[^1] != '/')
            searchPath2 += "/";
        else if (searchPath2 == "." || searchPath2 == "./")
            searchPath2 = "";

        string extPath = "";
        bool checkExt = false;
        if (lua_gettop(L) >= 2 && lua_isstring(L, 2))
        {
            var argExt = ctx.GetValue<string>(2);
            if (argExt.Length > 0)
            {
                extPath = "." + argExt;
                checkExt = true;
            }
        }

        string packName = "";
        bool checkPack = false;
        //TODO: Check if this is not a bug. Probably should check 3rd argument instead of second?
        if (findFilesMode && lua_gettop(L) >= 2 && lua_isstring(L, 2))
        {
            var argPack = ctx.GetValue<string>(3);
            if (argPack.Length > 0)
            {
                packName = argPack;
                checkPack = true;
            }
        }

        return new EnumFilesConfig
        {
            SearchPath = searchPath,
            SearchPath2 = searchPath2,
            HeadLen = headLen,
            ExtPath = extPath,
            PackName = packName,
            Index = 1,
            CheckExt = checkExt,
            CheckPack = checkPack,
            FindFiles = findFilesMode,
        };
    }

    private static void EnumFilesSystem(LuaStack ctx, StackIndex resultTable, EnumFilesConfig cfg)
    {
        DirectoryInfo dir;
        try
        {
            dir = new DirectoryInfo(cfg.SearchPath);
            if (!dir.Exists)
                return;
        }
        catch { return; }

        foreach (var info in dir.EnumerateFileSystemInfos())
        {
            var isDir = info is DirectoryInfo;
            if ((cfg.CheckExt || cfg.FindFiles) && isDir)
                continue;

            if (cfg.CheckExt && !string.Equals(info.Extension, cfg.ExtPath, StringComparison.Ordinal))
                continue;

            string childPath = cfg.SearchPath.EndsWith('/') ? cfg.SearchPath + info.Name : cfg.SearchPath + "/" + info.Name;
            var relative = cfg.HeadLen > 0 && childPath.Length >= cfg.HeadLen ? childPath[cfg.HeadLen..] : childPath;
            if (isDir)
                relative += '/';

            var entryTable = ctx.CreateArray();
            ctx.SetArrayValue(entryTable, 1, relative);
            ctx.SetArrayValue(entryTable, 2, isDir);
            ctx.SetArrayValue(resultTable, cfg.Index, entryTable);
            ctx.PopValue();
            cfg.Index++;
        }
    }

    private static string NormalizeDirectoryPrefix(string directory)
    {
        var normalized = directory.Replace('\\', '/').Trim('/');
        if (!string.IsNullOrEmpty(normalized) && !normalized.EndsWith('/'))
            normalized += "/";
        return normalized;
    }

    private static void EnumFilesArchive(LuaStack ctx, StackIndex resultTable, EnumFilesConfig cfg)
    {
        foreach (var archive in FileSystemManager.GetFileSystems().OfType<FileSystemArchive>())
        {
            if (cfg.CheckPack && archive.GetArchivePath() != cfg.PackName)
                continue;

            var prefix = NormalizeDirectoryPrefix(cfg.SearchPath2);

            foreach (var path in archive.EnumerateNodes(cfg.SearchPath2, recursive: false))
            {
                var isDirectory = path.EndsWith('/');
                var trimmed = isDirectory ? path[..^1] : path;
                var name = trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? trimmed[prefix.Length..]
                    : trimmed;

                if (cfg.FindFiles && isDirectory)
                    continue;
                if (cfg.CheckExt && !name.EndsWith(cfg.ExtPath, StringComparison.Ordinal))
                    continue;

                var entryTable = ctx.CreateArray();
                ctx.SetArrayValue(entryTable, 1, name);
                if (cfg.FindFiles)
                {
                    ctx.SetArrayValue(entryTable, 2, archive.GetArchivePath());
                }
                else
                {
                    ctx.SetArrayValue(entryTable, 2, isDirectory);
                    ctx.SetArrayValue(entryTable, 3, archive.GetArchivePath());
                }
                ctx.SetArrayValue(resultTable, cfg.Index, entryTable);
                ctx.PopValue();
                cfg.Index++;
            }
        }
    }

    [LuaBind]
    public static int EnumFiles(LuaState L)
    {
        LuaStack ctx = new(L);
        try
        {
            var cfg = InitEnumFiles(L, false);
            StackIndex resultTable = ctx.CreateArray();

            if (lua_toboolean(L, 3))
                EnumFilesArchive(ctx, resultTable, cfg);
            EnumFilesSystem(ctx, resultTable, cfg);

            return 1;
        }
        catch (Exception ex)
        {
            ctx.RaiseError($"lstg.FileManager.EnumFiles: {ex.Message}");
            return 0;
        }
    }

    [LuaBind]
    public static int EnumFilesEx(LuaState L)
    {
        LuaStack ctx = new(L);
        try
        {
            var cfg = InitEnumFiles(L, findFilesMode: false);
            var resultTable = ctx.CreateArray();

            EnumFilesArchive(ctx, resultTable, cfg);
            EnumFilesSystem(ctx, resultTable, cfg);

            return 1;
        }
        catch (Exception ex)
        {
            ctx.RaiseError($"lstg.FileManager.EnumFilesEx: {ex.Message}");
            return 0;
        }
    }

    [LuaBind]
    public static int FindFiles(LuaState L)
    {
        LuaStack ctx = new(L);
        try
        {
            var cfg = InitEnumFiles(L, findFilesMode: true);
            var resultTable = ctx.CreateArray();

            EnumFilesArchive(ctx, resultTable, cfg);
            if (!cfg.CheckPack)
                EnumFilesSystem(ctx, resultTable, cfg);

            return 1;
        }
        catch (Exception ex)
        {
            ctx.RaiseError($"lstg.FileManager.FindFiles: {ex.Message}");
            return 0;
        }
    }

    [LuaBind]
    public static int FileExist(LuaState L)
    {
        LuaStack ctx = new(L);
        string path = ctx.GetValue<string>(1);
        bool all = ctx.GetValue<bool>(2);
        if (all)
            ctx.Push(FileSystemManager.HasFile(path));
        else
            ctx.Push(FileSystemOS.Instance.HasFile(path));
        return 1;
    }

    [LuaBind]
    public static int FileExistEx(LuaState L)
    {
        LuaStack ctx = new(L);
        string path = ctx.GetValue<string>(1);
        ctx.Push(FileSystemManager.HasFile(path));
        return 1;
    }

    [LuaBind]
    public static int AddSearchPath(LuaState L)
    {
        LuaStack ctx = new(L);
        string path = ctx.GetValue<string>(1);
        FileSystemManager.AddSearchPath(path);
        return 0;
    }

    [LuaBind]
    public static int RemoveSearchPath(LuaState L)
    {
        LuaStack ctx = new(L);
        string path = ctx.GetValue<string>(1);
        FileSystemManager.RemoveSearchPath(path);
        return 0;
    }

    [LuaBind]
    public static int ClearSearchPath(LuaState L)
    {
        FileSystemManager.RemoveAllSearchPaths();
        return 0;
    }

    [LuaBind]
    public static int SetCurrentDirectory(LuaState L)
    {
        LuaStack ctx = new(L);
        string path = ctx.GetValue<string>(1);

        try
        {
            Directory.SetCurrentDirectory(path);
            lua_pushboolean(L, true);
            return 1;
        }
        catch (Exception ex)
        {
            lua_pushboolean(L, false);
            ctx.Push(ex.Message);
            lua_pushinteger(L, ex.HResult);
            return 3;
        }
    }

    [LuaBind]
    public static int GetCurrentDirectory(LuaState L)
    {
        LuaStack ctx = new(L);
        try
        {
            ctx.Push(Directory.GetCurrentDirectory().Replace('\\', '/'));
            return 1;
        }
        catch (Exception ex)
        {
            ctx.PushNil();
            ctx.Push(ex.Message);
            lua_pushinteger(L, ex.HResult);
            return 3;
        }
    }

    [LuaBind]
    public static int CreateDirectory(LuaState L)
    {
        LuaStack ctx = new(L);
        string path = ctx.GetValue<string>(1);

        try
        {
            Directory.CreateDirectory(path);
            lua_pushboolean(L, true);
            return 1;
        }
        catch (Exception ex)
        {
            lua_pushboolean(L, false);
            ctx.Push(ex.Message);
            lua_pushinteger(L, ex.HResult);
            return 3;
        }
    }

    [LuaBind]
    public static int RemoveDirectory(LuaState L)
    {
        LuaStack ctx = new(L);
        var path = ctx.GetValue<string>(1);

        try
        {
            Directory.Delete(path, recursive: true);
            lua_pushboolean(L, true);
            return 1;
        }
        catch (Exception ex)
        {
            lua_pushboolean(L, false);
            ctx.Push(ex.Message);
            lua_pushinteger(L, ex.HResult);
            return 3;
        }
    }

    [LuaBind]
    public static int DirectoryExist(LuaState L)
    {
        LuaStack ctx = new(L);
        try
        {
            var path = ctx.GetValue<string>(1);
            var all = ctx.GetValue<bool>(2);

            if (path.Length == 0)
            {
                ctx.Push(true);
                return 1;
            }

            ctx.Push(all ? FileSystemManager.HasDirectory(path) : (FileSystemOS.Instance?.HasDirectory(path) ?? false));
            return 1;
        }
        catch (Exception ex)
        {
            ctx.RaiseError($"lstg.FileManager.DirectoryExist: {ex.Message}");
            return 0;
        }
    }

    [LuaBind]
    public static int ExtractRes(LuaState L)
    {
        LuaStack ctx = new(L);
        string path = ctx.GetValue<string>(1);
        string target = ctx.GetValue<string>(2);

        if (!FileSystemManager.ReadFile(path, out byte[]? data) || data is null || !FileSystemManager.WriteFile(target, data))
        {
            ctx.RaiseError($"Failed to extract resource '{path}' to '{target}'.");
            return 0;
        }

        return 0;
    }

    #endregion

    private static readonly luaL_Reg[] tFunctions =
    [
        new("LoadArchive", CFunctions.LoadArchive),
        new("UnloadArchive", CFunctions.UnloadArchive),
        new("UnloadAllArchive", CFunctions.UnloadAllArchive),
        new("ArchiveExist", CFunctions.ArchiveExist),
        new("GetArchive", CFunctions.GetArchive),

        new("EnumArchives", CFunctions.EnumArchives),
        new("EnumFiles", CFunctions.EnumFiles),
        new("EnumFilesEx", CFunctions.EnumFilesEx),
        new("FileExist", CFunctions.FileExist),
        new("FileExistEx", CFunctions.FileExistEx),
        
        new("AddSearchPath", CFunctions.AddSearchPath),
        new("RemoveSearchPath", CFunctions.RemoveSearchPath),
        new("ClearSearchpath", CFunctions.ClearSearchPath),

        new("SetCurrentDirectory", CFunctions.SetCurrentDirectory),
        new("GetCurrentDirectory", CFunctions.GetCurrentDirectory),
        new("CreateDirectory", CFunctions.CreateDirectory),
        new("RemoveDirectory", CFunctions.RemoveDirectory),
        new("DirectoryExist", CFunctions.DirectoryExist),

        new(null, null),
    ];

    private static readonly luaL_Reg[] compat_lib =
    [
        new("LoadPackSub", CFunctions.LoadPackSub),
        new("UnloadPack", CFunctions.UnloadArchive),
        new("ExtractRes", CFunctions.ExtractRes),
        new("FindFiles", CFunctions.FindFiles),

        new(null, null),
    ];

    public static void Register(LuaState L)
    {
        LuaWrapper.EnsureLSTGInStack(L);
        Logger.luastg.Verbose($"Registering bindings: 'LW_FileManager'");

        lua_newtable(L);
        fixed (luaL_Reg* tFunctionsPtr = tFunctions)
            luaL_register(L, tFunctionsPtr);
        lua_setfield(L, -2, "FileManager");

        lua_pop(L, 1);
    }
}
