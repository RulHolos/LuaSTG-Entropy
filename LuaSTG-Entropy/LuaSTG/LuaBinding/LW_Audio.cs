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
    public static int SetMasterVolume(LuaState L)
    {
        float x = (float)luaL_checknumber(L, 1);
        x = Math.Clamp(x, 0f, 1f);
        Program.LAPP.WindowDevice.AudioDevice.SetMasterVolume(x);
        return 0;
    }

    [LuaBind]
    public static int PlaySound(LuaState L)
    {
        string s = luaL_checkstring(L, 1);
        SoundEffectResource? se = ResourceManager.Instance.FindResourceInAllPools<SoundEffectResource>(s);
        if (se == null)
            return luaL_error(L, $"sound effect '{s}' not found.");
        Program.LAPP.WindowDevice.AudioDevice.PlaySe(se);
        return 0;
    }

    [LuaBind]
    public static int SetSEVolume(LuaState L)
    {
        if (lua_gettop(L) <= 1)
        {
            float x = (float)luaL_checknumber(L, 1);
            x = Math.Clamp(x, 0f, 1f);
            Program.LAPP.WindowDevice.AudioDevice.SetSeTrackVolume(x);
            Program.LAPP.WindowDevice.AudioDevice.SeChannelVolume = x;
        }
        else
        {
            //TODO: Set global sound volume
        }
        return 0;
    }

    //See to remove.
    [LuaBind]
    public static int PlayMusic(LuaState L)
    {
        string s = luaL_checkstring(L, 1);
        MusicResource? music = ResourceManager.Instance.FindResourceInAllPools<MusicResource>(s);
        if (music == null)
            return luaL_error(L, $"music '{s}' not found.");
        Program.LAPP.WindowDevice.AudioDevice.PlayBgm(music);
        return 0;
    }

    [LuaBind]
    public static int SetBGMVolume(LuaState L)
    {
        if (lua_gettop(L) <= 1)
        {
            float x = (float)luaL_checknumber(L, 1);
            x = Math.Clamp(x, 0f, 1f);
            Program.LAPP.WindowDevice.AudioDevice.SetBgmTrackVolume(x);
            Program.LAPP.WindowDevice.AudioDevice.BgmChannelVolume = x;
        }
        else
        {
            //TODO: Set global music volume (Channel specific????)
        }
        return 0;
    }

    #endregion

    private static readonly luaL_Reg[] tFunctions =
    [
        new("SetMasterVolume", CFunctions.SetMasterVolume),

        new("PlaySound", CFunctions.PlaySound),
        new("SetSEVolume", CFunctions.SetSEVolume),

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
