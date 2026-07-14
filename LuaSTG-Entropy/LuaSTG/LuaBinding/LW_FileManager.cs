using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.FileSystem;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding;

public unsafe partial class LW_FileManager : ILuaBinding
{
    #region Functions

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

    #endregion

    private static readonly luaL_Reg[] tFunctions =
    [
        new("FileExist", CFunctions.FileExist),
        new("FileExistEx", CFunctions.FileExistEx),
        
        new("AddSearchPath", CFunctions.AddSearchPath),
        new("RemoveSearchPath", CFunctions.RemoveSearchPath),
        new("ClearSearchpath", CFunctions.ClearSearchPath),
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
