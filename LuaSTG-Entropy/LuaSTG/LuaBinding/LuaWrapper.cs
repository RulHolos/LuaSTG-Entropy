using luajit_sharp;
using LuaSTG.Core.Debugger;
using LuaSTG.LuaSTG.LuaBinding.Modern;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding;

public interface ILuaBinding
{
    static void Register(LuaState L) { }
}

public static class LuaWrapper
{
    public const string LUASTG_LUA_LIBNAME = "lstg";

    public unsafe static void RegisterBuiltInClassWrapper(LuaState L)
    {
        //Legacy code from Sub/Flux, shouldn't be used anywhere (was hosting StopWatch and some other shit...)
        luaL_Reg[] constructors =
        [
            new(null, null)
        ];

        fixed (luaL_Reg* constructorsPtr = constructors)
            luaL_register(L, LUASTG_LUA_LIBNAME, constructorsPtr);
        Color.Register(L);
        lua_pop(L, 1);

        //Classic
        LW_LuaSTG.Register(L);
        LW_ResourceMgr.Register(L);
        LW_Audio.Register(L);
        LW_Renderer.Register(L);
        LW_FileManager.Register(L);
        LW_Input.Register(L);
        LW_Archive.Register(L);
        LW_Window.Register(L);
        lua_settop(L, 0);

        //Modern
        NativeMusicResource.Register(L);
        Vector2.Register(L);
        Vector3.Register(L);
        Well512.Register(L);
    }

    public static void EnsureLSTGInStack(LuaState L)
    {
        lua_getfield(L, LUA_GLOBALSINDEX, LUASTG_LUA_LIBNAME);
        if (lua_type(L, -1) != LUA_TTABLE)
        {
            Logger.luajit.Error("Cannot register binding because global 'lstg' table does not exist yet.");
            lua_pop(L, 1);
            return;
        }
    }

    public static unsafe void RegisterMethodD(LuaState L, string name, luaL_Reg* methods, luaL_Reg* metamethods)
    {
        luaL_register(L, name, methods);
        luaL_newmetatable(L, name);
        luaL_register(L, metamethods);
        lua_pushstring(L, "__index");
        lua_pushvalue(L, -3);
        lua_rawset(L, -3);
        lua_pushstring(L, "__metatable");
        lua_pushvalue(L, -3);
        lua_rawset(L, -3);
        lua_pop(L, 2);
    }

    public static unsafe void RegisterClassIntoTable2(LuaState L, string name, luaL_Reg* methods, string metaname, luaL_Reg* metamethods)
    {
        lua_pushstring(L, name);
        lua_newtable(L);
        luaL_register(L, methods);
        luaL_newmetatable(L, metaname);
        luaL_register(L, metamethods);
        lua_pushstring(L, "__metatable");
        lua_pushvalue(L, -3);
        lua_rawset(L, -3);
        lua_pop(L, 1);
        lua_settable(L, -3);
    }
}
