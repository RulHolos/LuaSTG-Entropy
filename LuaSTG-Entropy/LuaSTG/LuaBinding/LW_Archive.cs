using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.FileSystem;
using LuaSTG.Core.GameObjects;
using LuaSTG.Core.LuaBindings;
using LuaSTG.Core.Rendering;
using LuaSTG.Core.Resources;
using LuaSTG.Core.Resources.Impl;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding;

public unsafe partial class LW_Archive : ILuaBinding
{
    #region Interop Helpers

    public static bool Is(LuaState L, int index)
    {
        LuaStack ctx = new(L);
        return ctx.IsMetatable(index, "lstg.Archive");
    }

    public static NativeArchiveWrapper* As(LuaState L, int index)
    {
        LuaStack ctx = new(L);
        return ctx.AsUserData<NativeArchiveWrapper>(index);
    }

    public static void CreateAndPush(LuaState L, FileSystemArchive? archive)
    {
        LuaStack ctx = new(L);
        var self = ctx.CreateUserData<NativeArchiveWrapper>();
        var selfIndex = ctx.IndexOfTop();
        *self = default;
        self->SetArchive(archive);
        ctx.SetMetatable(selfIndex, "lstg.Archive");
    }

    #endregion
    #region Functions

    [LuaBind]
    public static int IsValid(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        ctx.Push<bool>(self->Archive is not null);
        return 1;
    }

    private static int EnumerateFilesImpl(LuaState L, bool recursive)
    {
        LuaStack ctx = new(L);
        try
        {
            var self = As(L, 1);
            var resultTable = ctx.CreateArray();

            var archive = self->Archive;
            if (archive is null)
                return 1;

            var directory = ctx.GetValue<string>(2);
            var prefix = NormalizeDirectoryPrefix(directory);

            var i = 1;
            foreach (var path in archive.EnumerateNodes(directory, recursive))
            {
                var isDirectory = path.EndsWith('/');
                var trimmed = isDirectory ? path[..^1] : path;
                var name = trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? trimmed[prefix.Length..]
                    : trimmed;

                var nodeTable = ctx.CreateArray();
                ctx.SetArrayValue(nodeTable, 1, name);
                ctx.SetArrayValue(nodeTable, 2, isDirectory);
                ctx.SetArrayValue(resultTable, i, nodeTable);
                ctx.PopValue();
                i++;
            }

            return 1;
        }
        catch (Exception ex)
        {
            ctx.RaiseError($"lstg.Archive: {ex.Message}");
            return 0;
        }
    }

    private static string NormalizeDirectoryPrefix(string directory)
    {
        var normalized = directory.Replace('\\', '/').Trim('/');
        if (!string.IsNullOrEmpty(normalized) && !normalized.EndsWith('/'))
            normalized += "/";
        return normalized;
    }

    [LuaBind]
    public static int EnumFiles(LuaState L) => EnumerateFilesImpl(L, false);

    [LuaBind]
    public static int ListFiles(LuaState L) => EnumerateFilesImpl(L, true);

    [LuaBind]
    public static int FileExist(LuaState L)
    {
        LuaStack ctx = new(L);
        try
        {
            var self = As(L, 1);
            var archive = self->Archive;
            if (archive is null)
            {
                ctx.Push<bool>(false);
                return 1;
            }
            var path = ctx.GetValue<string>(2);
            ctx.Push(archive.HasFile(path));
            return 1;
        }
        catch (Exception ex)
        {
            ctx.RaiseError($"lstg.Archive: {ex.Message}");
            return 0;
        }
    }

    [LuaBind]
    public static int GetName(LuaState L)
    {
        LuaStack ctx = new(L);
        try
        {
            var self = As(L, 1);
            var archive = self->Archive;
            if (archive is null)
            {
                ctx.PushNil();
                return 1;
            }
            ctx.Push(archive.GetArchivePath());
            return 1;
        }
        catch (Exception ex)
        {
            ctx.RaiseError($"lstg.Archive: {ex.Message}");
            return 0;
        }
    }

    [LuaBind]
    public static int GetPriority(LuaState L)
    {
        _ = As(L, 1);
        lua_pushinteger(L, 0);
        return 1;
    }

    [LuaBind]
    public static int SetPriority(LuaState L)
    {
        _ = As(L, 1);
        return 0;
    }

    #endregion
    #region Metamethods

    [LuaBind]
    private static int Meta_ToString(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        var archive = self->Archive;
        ctx.Push(archive is not null ? $"lstg.Archive(\"{archive.GetArchivePath()}\")" : "lstg.Archive(null)");
        return 1;
    }

    [LuaBind]
    private static int Meta_GC(LuaState L)
    {
        var self = As(L, 1);
        self->Archive?.Dispose();
        self->Release();
        return 0;
    }

    #endregion

    private static readonly luaL_Reg[] tMethods =
    [
        new("IsValid", CFunctions.IsValid),
        new("EnumFiles", CFunctions.EnumFiles),
        new("ListFiles", CFunctions.ListFiles),
        new("FileExist", CFunctions.FileExist),
        new("GetName", CFunctions.GetName),

        new("GetPriority", CFunctions.GetPriority),
        new("SetPriority", CFunctions.SetPriority),

        new(),
    ];

    private static readonly luaL_Reg[] tMetaTable =
    [
        new("__tostring", CFunctions.Meta_ToString),
        new("__gc", CFunctions.Meta_GC),

        new(),
    ];

    public static void Register(LuaState L)
    {
        LuaWrapper.EnsureLSTGInStack(L);
        Logger.luastg.Verbose($"Registering bindings: 'LW_Archive'");

        fixed (luaL_Reg* tMetaTablePtr = tMetaTable, tMethodsPtr = tMethods)
            LuaWrapper.RegisterMethodD(L, "lstg.Archive", tMethodsPtr, tMetaTablePtr);
    }
}
