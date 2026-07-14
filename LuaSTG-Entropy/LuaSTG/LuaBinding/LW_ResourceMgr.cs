using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.Resources;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding;

public unsafe partial class LW_ResourceMgr
{
    #region Functions

    [LuaBind]
    public static int LoadSound(LuaState L)
    {
        string s = luaL_checkstring(L, 1);
        //TODO: FindSound
        Logger.luajit.Information($"PlaySound: {s}");
        return 0;
    }

    [LuaBind]
    public static int LoadMusic(LuaState L)
    {
        string name = luaL_checkstring(L, 1);
        string path = luaL_checkstring(L, 2);

        ResourcePool? pool = ResourceManager.Instance.GetCurrentResourcePool();
        if (pool == null)
            return luaL_error(L, "can't load resource at this time");

        double loop_end = luaL_checknumber(L, 3);
        double loop_duration = luaL_checknumber(L, 4);
        double loop_start = Math.Max(0.0, loop_end - loop_duration);

        if (!pool.LoadMusic(name, path, loop_start, loop_end, (lua_gettop(L) >= 5) && lua_toboolean(L, 5)))
            return luaL_error(L, $"load music failed (name={name}, path={path}, loop={loop_start}~{loop_end})");
        return 0;
    }

    [LuaBind]
    public static int CheckRes(LuaState L)
    {
        ResourceType tResourceType = (ResourceType)luaL_checkinteger(L, 1);
        string tResourceName = luaL_checkstring(L, 2);

        var pools = ResourceManager.Instance.EnumPools();
        foreach (var pool in pools)
        {
            if (pool.FindResource(tResourceType, tResourceName) != null)
            {
                lua_pushstring(L, pool.poolName);
                return 1;
            }
        }
        lua_pushnil(L);
        return 1;
    }

    #endregion

    private static readonly luaL_Reg[] tFunctions =
    [
        new("LoadSound", CFunctions.LoadSound),
        new("LoadMusic", CFunctions.LoadMusic),
        new(null, null),
    ];

    public static void Register(LuaState L)
    {
        LuaWrapper.EnsureLSTGInStack(L);
        Logger.luastg.Verbose($"Registering bindings: 'LW_ResourceMgr'");

        fixed (luaL_Reg* tFunctionsPtr = tFunctions)
        {
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME, tFunctionsPtr);
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME + ".ResourceManager", tFunctionsPtr);
        }
        lua_setfield(L, -1, "ResourceManager");
        lua_pop(L, 1);
    }
}
