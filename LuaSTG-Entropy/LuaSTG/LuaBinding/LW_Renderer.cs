using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.Rendering;
using LuaSTG.Core.Resources;
using LuaSTG.Core.Resources.Impl;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding;

public unsafe partial class LW_Renderer
{
    #region Functions

    [LuaBind]
    public static int Render(LuaState L)
    {
        //TODO: Validate render scope

        string name = luaL_checkstring(L, 1);
        ImageResource? img = ResourceManager.Instance.FindResourceInAllPools<ImageResource>(name);
        if (img == null)
            return luaL_error(L, $"can't find sprite '{name}'");

        double x = luaL_checknumber(L, 2);
        double y = luaL_checknumber(L, 3);
        double rot = luaL_optnumber(L, 4, 0.0) * L_DEG_TO_RAD;
        double hscale = luaL_optnumber(L, 5, 1.0); //TODO: Multiply with GlobalImageScaleFactor
        double vscale = luaL_optnumber(L, 6, hscale); //TODO: Multiply with GlobalImageScaleFactor
        double z = luaL_optnumber(L, 7, 0.5);

        Program.LAPP.WindowDevice.RenderEngine.SpriteRenderer.Draw(img, (float)x, (float)y, (float)rot, (float)hscale, (float)vscale);
        return 0;
    }

    [LuaBind]
    public static int RenderTexture(LuaState L)
    {
        return luaL_notimplemented(L);
        string name = luaL_checkstring(L, 1);

        return 0;
    }

    #endregion

    private static readonly luaL_Reg[] tFunctions =
    [
        new("Render", CFunctions.Render),
        new("RenderTexture", CFunctions.RenderTexture),
        new(null, null),
    ];

    public static void Register(LuaState L)
    {
        LuaWrapper.EnsureLSTGInStack(L);
        Logger.luastg.Verbose($"Registering bindings: 'LW_Renderer'");

        fixed (luaL_Reg* tFunctionsPtr = tFunctions)
        {
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME, tFunctionsPtr);
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME + ".Renderer", tFunctionsPtr);
        }
        lua_setfield(L, -1, "Renderer");
        lua_pop(L, 1);
    }
}
