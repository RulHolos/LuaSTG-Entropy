using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding;

public unsafe partial class LW_Window : ILuaBinding
{
    #region Functions

    [LuaBind]
    public static int SetTitle(LuaState L)
    {
        string title = luaL_checkstring(L, 1);

        Program.LAPP.WindowDevice.SetTitle(title);

        return 0;
    }

    [LuaBind]
    public static int SetResolution(LuaState L)
    {
        int width = (int)luaL_checkinteger(L, 1);
        int height = (int)luaL_checkinteger(L, 2);

        Program.LAPP.WindowDevice.SetResolution(width, height);

        return 0;
    }

    [LuaBind]
    public static int SetVSync(LuaState L)
    {
        bool enable = lua_toboolean(L, 1);

        Program.LAPP.WindowDevice.SetVSync(enable);

        return 0;
    }

    #endregion

    private static readonly luaL_Reg[] tFunctions =
    [
        new("SetTitle", CFunctions.SetTitle),
        new("SetResolution", CFunctions.SetResolution),
        new("SetVSync", CFunctions.SetVSync),

        new(null, null),
    ];

    public static void Register(LuaState L)
    {
        LuaWrapper.EnsureLSTGInStack(L);
        Logger.luastg.Verbose($"Registering bindings: 'LW_Window'");

        fixed (luaL_Reg* tFunctionsPtr = tFunctions)
            luaL_register(L, tFunctionsPtr);
        lua_setfield(L, -2, "Window");

        lua_pop(L, 1);
    }
}
