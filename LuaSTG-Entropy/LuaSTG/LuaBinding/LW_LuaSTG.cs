using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding;

public unsafe partial class LW_LuaSTG : ILuaBinding
{
    #region Functions

    /// <summary>
    /// If set, will override the default candidates for the entry script (not the launch script, the actual entry point)
    /// </summary>
    /// <param name="L"></param>
    /// <returns></returns>
    [LuaBind]
    public static int SetEntryScript(LuaState L)
    {
        if (Program.LAPP.Status != AppStatus.Initializing)
        {
            Logger.luastg.Warning($"lstg.SetEntryScript() was called outside of the engine initialization step. This call will result in a no-op");
            return 0;
        }

        Program.LAPP.EntryScriptOverride = luaL_checkstring(L, 1);
        return 0;
    }

    [LuaBind]
    public static int GetVersionNumber(LuaState L)
    {
        lua_pushinteger(L, LUASTG_VERSION_MAJOR);
        lua_pushinteger(L, LUASTG_VERSION_MINOR);
        lua_pushinteger(L, LUASTG_VERSION_PATCH);
        return 3;
    }

    [LuaBind]
    public static int GetVersionName(LuaState L)
    {
        lua_pushstring(L, LUASTG_INFO);
        return 1;
    }

    [LuaBind]
    public static int GetBranchName(LuaState L)
    {
        lua_pushstring(L, LUASTG_BRANCH);
        return 1;
    }

    [LuaBind]
    public static int GetDevBranchName(LuaState L)
    {
        lua_pushstring(L, LUASTG_DEV_BRANCH);
        return 1;
    }

    [LuaBind]
    public static int Log(LuaState L)
    {
        LuaStack S = new(L);
        int level = S.GetValue<int>(1);
        string message = S.GetValue<string>(2);
        Logger.lua.WithLevel(level, message);
        return 0;
    }

    [LuaBind]
    public static int DoFile(LuaState L)
    {
        int args = lua_gettop(L);
        Program.LAPP.LoadScript(L, luaL_checkstring(L, 1), luaL_optstring(L, 2, null));
        return lua_gettop(L) - args;
    }

    [LuaBind]
    public static int LoadTextFile(LuaState L)
    {
        return Program.LAPP.LoadTextFile(L, luaL_checkstring(L, 1), luaL_optstring(L, 2, null));
    }

    [LuaBind]
    public static int SetFPS(LuaState L)
    {
        double fps = luaL_checkinteger(L, 1);
        Program.LAPP.WindowDevice.RenderEngine.SetFPS(fps);
        return 0;
    }

    [LuaBind]
    public static int GetFPS(LuaState L)
    {
        lua_pushnumber(L, Program.LAPP.WindowDevice.RenderEngine.GetCurrentFPS());
        return 1;
    }

    #endregion

    private static readonly luaL_Reg[] tFunctions =
    [
        new("SetEntryScript", CFunctions.SetEntryScript),

        new("GetVersionNumber", CFunctions.GetVersionNumber),
        new("GetVersionName", CFunctions.GetVersionName),
        new("GetBranchName", CFunctions.GetBranchName),
        new("GetDevBranchName", CFunctions.GetDevBranchName),
        new("Log", CFunctions.Log),
        new("DoFile", CFunctions.DoFile),
        new("LoadTextFile", CFunctions.LoadTextFile), 

        new("SetFPS", CFunctions.SetFPS),
        new("GetFPS", CFunctions.GetFPS),

        new(null, null),
    ];

    public static void Register(LuaState L)
    {
        Logger.luastg.Verbose($"Registering bindings: 'LW_LuaSTG'");

        fixed (luaL_Reg* tFunctionsPtr = tFunctions)
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME, tFunctionsPtr);
        lua_pop(L, 1);
    }
}
