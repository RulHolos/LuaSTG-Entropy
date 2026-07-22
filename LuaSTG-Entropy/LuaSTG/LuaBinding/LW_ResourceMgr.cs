using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.Resources;
using LuaSTG.Core.Resources.Impl;
using LuaSTG.LuaSTG.LuaBinding.Modern;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding;

/// <summary>
/// TODO: For each "Load" resource, return a native handler to that resource.
/// </summary>
public unsafe partial class LW_ResourceMgr
{
    #region Functions

    [LuaBind]
    public static int GetTextureSize(LuaState L)
    {
        LuaStack ctx = new(L);
        string tex_name = ctx.GetValue<string>(1);

        ResourcePool? pool = ResourceManager.Instance.GetCurrentResourcePool();
        if (pool == null)
            return luaL_error(L, "can't retrieve resources at this time");

        if (!pool.TryFindResource<TextureResource>(ResourceType.Texture, tex_name, out var res))
            return luaL_error(L, $"GetTextureSize: texture '{tex_name}' wasn't found.");

        ctx.Push<int>(res!.Width);
        ctx.Push<int>(res!.Height);
        return 1;
    }

    [LuaBind]
    public static int LoadTexture(LuaState L)
    {
        LuaStack ctx = new(L);
        string name = ctx.GetValue<string>(1);
        string path = ctx.GetValue<string>(2);
        bool mipmap = lua_toboolean(L, 3);

        ResourcePool? pool = ResourceManager.Instance.GetCurrentResourcePool();
        if (pool == null)
            return luaL_error(L, "can't load resource at this time");

        if (!pool.LoadTexture(name, path, mipmaps: mipmap))
            return luaL_error(L, $"can't load texture from file '{path}'.");
        return 0;
    }

    [LuaBind]
    public static int LoadImage(LuaState L)
    {
        LuaStack ctx = new(L);
        string name = ctx.GetValue<string>(1);
        string tex_name = ctx.GetValue<string>(2);
        //TODO: Make this nullable somehow
        double x = luaL_checknumber(L, 3);
        double y = luaL_checknumber(L, 4);
        double width = luaL_checknumber(L, 5);
        double height = luaL_checknumber(L, 6);
        double a = luaL_optnumber(L, 7, 0.0);
        double b = luaL_optnumber(L, 8, 0.0);
        bool rect = lua_toboolean(L, 9);

        ResourcePool? pool = ResourceManager.Instance.GetCurrentResourcePool();
        if (pool == null)
            return luaL_error(L, "can't load resource at this time");

        if (!pool.LoadSprite(name, tex_name, x, y, width, height, a, b, rect))
            return luaL_error(L, $"load image failed (name='{name}', tex='{tex_name}').");
        return 0;
    }

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
        LuaStack ctx = new(L);
        string name = luaL_checkstring(L, 1);
        string path = luaL_checkstring(L, 2);

        ResourcePool? pool = ResourceManager.Instance.GetCurrentResourcePool();
        if (pool == null)
            return luaL_error(L, "can't load resource at this time");

        double loop_end = luaL_checknumber(L, 3);
        double loop_duration = luaL_checknumber(L, 4);
        double loop_start = Math.Max(0.0, loop_end - loop_duration);

        if (!pool.TryLoadMusic(name, path, loop_start, loop_end, (lua_gettop(L) >= 5) && lua_toboolean(L, 5), out var musicRes))
            return luaL_error(L, $"load music failed (name={name}, path={path}, loop={loop_start}~{loop_end})");

        NativeMusicResource.Push(L, musicRes!);
        return 1;
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
        new("LoadTexture", CFunctions.LoadTexture),
        new("LoadImage", CFunctions.LoadImage),
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
