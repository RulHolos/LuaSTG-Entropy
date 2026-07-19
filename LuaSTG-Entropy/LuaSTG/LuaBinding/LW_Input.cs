using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.FileSystem;
using LuaSTG.Core.Rendering;
using LuaSTG.Core.Resources;
using LuaSTG.Core.Resources.Impl;
using Silk.NET.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding;

public unsafe partial class LW_Input : ILuaBinding
{
    #region Functions

    [LuaBind]
    public static int GetKeyState(LuaState L)
    {
        var code = lua_tointeger(L, 1);
        lua_pushboolean(L, Program.LAPP.WindowDevice.InputDevice.GetKeyState(code));
        return 1;
    }

    [LuaBind]
    public static int GetLastKey(LuaState L)
    {
        lua_pushinteger(L, (int)Program.LAPP.WindowDevice.InputDevice.LastKeyPressed);
        return 1;
    }

    [LuaBind]
    public static int GetMouseState(LuaState L)
    {
        var code = lua_tointeger(L, 1);
        lua_pushboolean(L, Program.LAPP.WindowDevice.InputDevice.GetMouseState(code));
        return 1;
    }

    [LuaBind]
    public static int GetMousePosition(LuaState L)
    {
        var pos = Program.LAPP.WindowDevice.InputDevice.GetMousePosition();
        lua_pushinteger(L, (int)pos.X);
        lua_pushinteger(L, (int)pos.Y);
        return 2;
    }

    [LuaBind]
    public static int GetMouseWheelDeltaNormalized(LuaState L)
    {
        lua_pushnumber(L, Program.LAPP.WindowDevice.InputDevice.GetMouseWheelDelta() / 120.0);
        return 1;
    }

    [LuaBind]
    public static int GetMouseWheelDelta(LuaState L)
    {
        lua_pushnumber(L, Program.LAPP.WindowDevice.InputDevice.GetMouseWheelDelta());
        return 1;
    }

    #endregion
    #region Keyboard

    public static void Register_Keyboard(LuaState L)
    {
        luaL_Reg[] tFunctions =
        [
            new("GetKeyState", CFunctions.GetKeyState),

            new(null, null),
        ];

        fixed (luaL_Reg* tFunctionsPtr = tFunctions, lib_emptyPtr = lib_empty)
        {
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME + ".Input", lib_emptyPtr);
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME + ".Input.Keyboard", tFunctionsPtr);
        }

        foreach (Key key in Enum.GetValues<Key>())
        {
            lua_pushstring(L, key.ToString());
            lua_pushinteger(L, (int)key);
            lua_settable(L, -3);
        }

        lua_setfield(L, -1, "Keyboard");
        lua_pop(L, 1);
    }

    #endregion
    #region Mouse

    public static void Register_Mouse(LuaState L)
    {
        luaL_Reg[] tFunctions =
        [
            new("GetMouseState", CFunctions.GetMouseState),
            new("GetMousePosition", CFunctions.GetMousePosition),

            new("GetMouseWheelDelta", CFunctions.GetMouseWheelDeltaNormalized),
            new(null, null),
        ];

        fixed (luaL_Reg* tFunctionsPtr = tFunctions, lib_emptyPtr = lib_empty)
        {
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME + ".Input", lib_emptyPtr);
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME + ".Input.Mouse", tFunctionsPtr);
        }

        foreach (MouseButton button in Enum.GetValues<MouseButton>())
        {
            lua_pushstring(L, button.ToString());
            lua_pushinteger(L, (int)button);
            lua_settable(L, -3);
        }

        lua_setfield(L, -1, "Mouse");
        lua_pop(L, 1);
    }

    #endregion

    private static readonly luaL_Reg[] tFunctions =
    [
        new("GetKeyState", CFunctions.GetKeyState),
        new("GetMouseState", CFunctions.GetMouseState),
        new("GetMousePosition", CFunctions.GetMousePosition),
        new("GetMouseWheelDelta", CFunctions.GetMouseWheelDelta),
        new("GetLastKey", CFunctions.GetLastKey),

        new(null, null),
    ];

    private static readonly luaL_Reg[] lib_empty = [new(null, null)];

    public static void Register(LuaState L)
    {
        LuaWrapper.EnsureLSTGInStack(L);
        Logger.luastg.Verbose($"Registering bindings: 'LW_Input'");

        fixed (luaL_Reg* tFunctionsPtr = tFunctions, lib_emptyPtr = lib_empty)
        {
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME, tFunctionsPtr);
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME + ".Input", lib_emptyPtr);
        }
        lua_setfield(L, -1, "Input");
        lua_pop(L, 1);

        Register_Keyboard(L);
        Register_Mouse(L);
    }
}
