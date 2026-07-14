using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.FileSystem;
using LuaSTG.Core.Rendering;
using LuaSTG.Core.Resources;
using LuaSTG.Core.Resources.Impl;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding;

public unsafe partial class LW_Audio : ILuaBinding
{
    #region Functions

    [LuaBind]
    public static int PlaySound(LuaState L)
    {
        string s = luaL_checkstring(L, 1);
        //TODO: FindSound
        Logger.luajit.Information($"PlaySound: {s}");
        return 0;
    }

    [LuaBind]
    public static int PlayMusic(LuaState L)
    {
        string s = luaL_checkstring(L, 1);
        MusicResource? music = (MusicResource?)ResourceManager.Instance.FindMusic(s);
        if (music == null)
            return luaL_error(L, $"music '{s}' not found.");
        RenderEngine.Instance.Device.AudioDevice.PlayBgm(music);
        return 0;
    }

    [LuaBind]
    public static int SetBGMVolume(LuaState L)
    {
        if (lua_gettop(L) <= 1)
        {
            float x = (float)luaL_checknumber(L, 1);
            x = Math.Clamp(x, 0f, 1f);
            RenderEngine.Instance.Device.AudioDevice.SetBgmTrackVolume(x);
            RenderEngine.Instance.Device.AudioDevice.BgmChannelVolume = x;
        }
        else
        {

        }
        return 0;
    }

    #endregion

    private static readonly luaL_Reg[] tFunctions =
    [
        new("PlaySound", CFunctions.PlaySound),

        new("PlayMusic", CFunctions.PlayMusic),
        new("SetBGMVolume", CFunctions.SetBGMVolume),

        new(null, null),
    ];

    public static void Register(LuaState L)
    {
        LuaWrapper.EnsureLSTGInStack(L);
        Logger.luastg.Verbose($"Registering bindings: 'LW_Audio'");

        fixed (luaL_Reg* tFunctionsPtr = tFunctions)
        {
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME, tFunctionsPtr);
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME + ".Audio", tFunctionsPtr);
        }
        lua_setfield(L, -1, "Audio");
        lua_pop(L, 1);
    }
}
