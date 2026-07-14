global using unsafe lua_CFunction = delegate* unmanaged[Cdecl]<luajit_sharp.LuaState, int>;
using unsafe lua_Reader = delegate* unmanaged[Cdecl]<luajit_sharp.LuaState, void*, nuint*, byte*>;
using unsafe lua_Writer = delegate* unmanaged[Cdecl]<luajit_sharp.LuaState, void*, nuint, void*, int>;
using unsafe lua_Alloc = delegate* unmanaged[Cdecl]<void*, void*, nuint, nuint, void*>;

using System.Runtime.InteropServices;
using System.Text;
using System.Runtime.CompilerServices;

namespace luajit_sharp;

public unsafe static partial class LuaNative
{
    private const string Lib = "luajit";

    public const string LUA_VERSION = "Lua 5.1";
    public const string LUA_RELEASE = "Lua 5.1.4";
    public const int LUA_VERSION_NUM = 501;
    public const string LUA_COPYRIGHT = "Copyright (C) 1994-2008 Lua.org, PUC-Rio";
    public const string LUA_AUTHORS = "R. Ierusalimschy, L. H. de Figueiredo & W. Celes";

    public const string LUAJIT_VERSION = "LuaJIT 2.1.1783585446";
    public const int LUAJIT_VERSION_NUM = 20199;
    public const string LUAJIT_COPYRIGHT = "Copyright (C) 2005-2026 Mike Pall";
    public const string LUAJIT_URL = "https://luajit.org/";

    public const int LUAJIT_MODE_MASK = 0x00ff;

    public const string LUA_SIGNATURE = "\033Lua";
    public const int LUA_MULTRET = -1;

    //Pseudo-indices
    public const int LUA_REGISTRYINDEX = -10000;
    public const int LUA_ENVIRONINDEX = -10001;
    public const int LUA_GLOBALSINDEX = -10002;

    //Thread status
    public const int LUA_OK = 0;
    public const int LUA_YIELD = 1;
    public const int LUA_ERRRUN = 2;
    public const int LUA_ERRSYNTAX = 3;
    public const int LUA_ERRMEM = 4;
    public const int LUA_ERRERR = 5;

    //Basic types
    public const int LUA_TNONE = -1;
    public const int LUA_TNIL = 0;
    public const int LUA_TBOOLEAN = 1;
    public const int LUA_TLIGHTUSERDATA = 2;
    public const int LUA_TNUMBER = 3;
    public const int LUA_TSTRING = 4;
    public const int LUA_TTABLE = 5;
    public const int LUA_TFUNCTION = 6;
    public const int LUA_TUSERDATA = 7;
    public const int LUA_TTHREAD = 8;

    public const int LUA_MINSTACK = 20;

    // Regions are taken directly from lua.h and luajit.h

    #region State manipulation
    [LibraryImport(Lib)]
    public static partial LuaState luaL_newstate();

    [LibraryImport(Lib)]
    public static partial void lua_close(LuaState L);

    [LibraryImport(Lib)]
    public static partial LuaState lua_newthread(LuaState L);
    #endregion
    #region Basic stack manipulation
    [LibraryImport(Lib)]
    public static partial int lua_gettop(LuaState L);

    [LibraryImport(Lib)]
    public static partial void lua_settop(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial void lua_pushvalue(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial void lua_remove(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial void lua_insert(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial void lua_replace(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial int lua_checkstack(LuaState L, int sz);

    [LibraryImport(Lib)]
    public static partial void lua_xmove(LuaState from, LuaState to, int n);
    #endregion
    #region Access functions (stack -> C)
    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool lua_isnumber(LuaState L, int idx);

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool lua_isstring(LuaState L, int idx);

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool lua_iscfunction(LuaState L, int idx);

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool lua_isuserdata(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial int lua_type(LuaState L, int idx);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial string lua_typename(LuaState L, int tp);

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool lua_equal(LuaState L, int idx1, int idx2);

    [LibraryImport(Lib)]
    public static partial int lua_rawequal(LuaState L, int idx1, int idx2);

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool lua_lessthan(LuaState L, int idx1, int idx2);

    [LibraryImport(Lib)]
    public static partial double lua_tonumber(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial int lua_tointeger(LuaState L, int idx);

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool lua_toboolean(LuaState L, int idx);

    [LibraryImport(Lib, EntryPoint = "lua_tolstring")]
    internal static partial IntPtr _lua_tolstring(LuaState L, int idx, out nuint len);
    public static string? lua_tostring(LuaState L, int idx)
    {
        IntPtr ptr = _lua_tolstring(L, idx, out nuint len);
        if (ptr == IntPtr.Zero)
            return null;
        unsafe
        {
            var span = new ReadOnlySpan<byte>((void*)ptr, checked((int)len));
            return Encoding.UTF8.GetString(span);
        }
    }

    [LibraryImport(Lib)]
    public static partial nuint lua_objlen(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial lua_CFunction lua_tocfunction(LuaState L, int idx);

    //TODO: Fix the IntPtr to usedata
    [LibraryImport(Lib)]
    public static partial void* lua_touserdata(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial LuaState lua_tothread(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial IntPtr lua_topointer(LuaState L, int idx);
    #endregion
    #region Push functions (C -> stack)
    [LibraryImport(Lib)]
    public static partial void lua_pushnil(LuaState L);

    [LibraryImport(Lib)]
    public static partial void lua_pushnumber(LuaState L, double n);

    [LibraryImport(Lib)]
    public static partial void lua_pushinteger(LuaState L, int n);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void lua_pushstring(LuaState L, string s);

    [LibraryImport(Lib)]
    public static partial void lua_pushcclosure(LuaState L, lua_CFunction fn, int n);

    [LibraryImport(Lib)]
    public static partial void lua_pushboolean(LuaState L, [MarshalAs(UnmanagedType.Bool)] bool b);

    [LibraryImport(Lib)]
    public static partial void lua_pushlightuserdata(LuaState L, IntPtr p);

    [LibraryImport(Lib)]
    public static partial int lua_pushthread(LuaState L);
    #endregion
    #region Get functions (Lua -> stack)
    [LibraryImport(Lib)]
    public static partial void lua_gettable(LuaState L, int idx);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void lua_getfield(LuaState L, int idx, string k);

    [LibraryImport(Lib)]
    public static partial void lua_getfield(LuaState L, int idx, byte* k);

    [LibraryImport(Lib)]
    public static partial void lua_rawget(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial void lua_rawgeti(LuaState L, int idx, int n);

    [LibraryImport(Lib)]
    public static partial void lua_createtable(LuaState L, int narr, int nrec);

    [LibraryImport(Lib)]
    public static partial IntPtr lua_newuserdata(LuaState L, nuint sz);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr luaL_checkudata(LuaState L, int ud, string tname);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int luaL_newmetatable(LuaState L, string tname);

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool lua_getmetatable(LuaState L, int objindex);

    public static void luaL_getmetatable(LuaState L, byte* name) => lua_getfield(L, LUA_REGISTRYINDEX, name);
    public static void luaL_getmetatable(LuaState L, string name)
    {
        IntPtr nativeString = Marshal.StringToHGlobalAnsi(name);
        try
        {
            lua_getfield(L, LUA_REGISTRYINDEX, (byte*)nativeString);
        }
        finally
        {
            Marshal.FreeHGlobal(nativeString);
        }
    }

    [LibraryImport(Lib)]
    public static partial void lua_getfenv(LuaState L, int idx);
    #endregion
    #region Set functions (stack -> Lua)
    [LibraryImport(Lib)]
    public static partial void lua_settable(LuaState L, int idx);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void lua_setfield(LuaState L, int idx, string k);

    [LibraryImport(Lib)]
    public static partial void lua_rawset(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial void lua_rawseti(LuaState L, int idx, int n);

    [LibraryImport(Lib)]
    public static partial int lua_setmetatable(LuaState L, int objindex);

    [LibraryImport(Lib)]
    public static partial int lua_setfenv(LuaState L, int idx);
    #endregion
    #region 'load' and 'call' functions (load and run Lua code)
    [LibraryImport(Lib)]
    public static partial void lua_call(LuaState L, int nargs, int nresults);

    [LibraryImport(Lib)]
    public static partial int lua_pcall(LuaState L, int nargs, int nresults, int errfunc);

    //TODO: lua_cpcall, lua_load and lua_dump
    #endregion
    #region coroutine functions
    [LibraryImport(Lib)]
    public static partial int lua_yield(LuaState L, int nresults);

    [LibraryImport(Lib)]
    public static partial int lua_resume(LuaState L, int nargs);

    [LibraryImport(Lib)]
    public static partial int lua_status(LuaState L);
    #endregion
    #region garbage-collection function and options
    public const int LUA_GCSTOP = 0;
    public const int LUA_GCRESTART = 1;
    public const int LUA_GCCOLLECT = 2;
    public const int LUA_GCCOUNT = 3;
    public const int LUA_GCCOUNTB = 4;
    public const int LUA_GCSTEP = 5;
    public const int LUA_GCSETPAUSE = 6;
    public const int LUA_GCSETSTEPMUL = 7;
    public const int LUA_GCISRUNNING = 9;

    [LibraryImport(Lib)]
    public static partial int lua_gc(LuaState L, int what, int data);
    #endregion
    #region Miscellaneous functions
    [LibraryImport(Lib)]
    public static partial int lua_error(LuaState L);

    [LibraryImport(Lib)]
    public static partial int lua_next(LuaState L, int idx);

    [LibraryImport(Lib)]
    public static partial void lua_concat(LuaState L, int n);

    //TODO: lua_getallocf and lua_setallocf
    #endregion
    #region Some useful macros
    public static void lua_pop(LuaState L, int n) => lua_settop(L, -(n) - 1);
    public static void lua_newtable(LuaState L) => lua_createtable(L, 0, 0);
    public static void lua_pushcfunction(LuaState L, lua_CFunction f) => lua_pushcclosure(L, f, 0);
    public static nuint lua_strlen(LuaState L, int i) => lua_objlen(L, (i));

    public static bool lua_isfunction(LuaState L, int idx) => lua_type(L, idx) == LUA_TFUNCTION;
    public static bool lua_istable(LuaState L, int idx) => lua_type(L, idx) == LUA_TTABLE;
    public static bool lua_islightuserdata(LuaState L, int idx) => lua_type(L, idx) == LUA_TLIGHTUSERDATA;
    public static bool lua_isnil(LuaState L, int idx) => lua_type(L, idx) == LUA_TNIL;
    public static bool lua_isboolean(LuaState L, int idx) => lua_type(L, idx) == LUA_TBOOLEAN;
    public static bool lua_isthread(LuaState L, int idx) => lua_type(L, idx) == LUA_TTHREAD;
    public static bool lua_isnone(LuaState L, int idx) => lua_type(L, idx) == LUA_TNONE;
    public static bool lua_isnoneornil(LuaState L, int idx) => lua_type(L, idx) <= 0;

    public static void lua_setglobal(LuaState L, string s) => lua_setfield(L, LUA_GLOBALSINDEX, s);
    public static void lua_getglobal(LuaState L, string s) => lua_getfield(L, LUA_GLOBALSINDEX, s);

    //TODO: lua_register, lua_pushcfunction
    #endregion
    #region Debug API
    public const int LUA_HOOKCALL = 0;
    public const int LUA_HOOKRET = 1;
    public const int LUA_HOOKLINE = 2;
    public const int LUA_HOOKCOUNT = 3;
    public const int LUA_HOOKTAILRET = 4;

    public const int LUA_MASKCALL = 1 << LUA_HOOKCALL;
    public const int LUA_MASKRET = 1 << LUA_HOOKRET;
    public const int LUA_MASKLINE = 1 << LUA_HOOKLINE;
    public const int LUA_MASKCOUNT = 1 << LUA_HOOKCOUNT;

    //TODO
    #endregion

    #region lualib.h
    [LibraryImport(Lib)]
    public static partial void luaL_openlibs(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_base(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_math(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_string(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_table(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_io(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_os(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_package(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_debug(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_bit(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_jit(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_ffi(LuaState L);

    [LibraryImport(Lib)]
    public static partial int luaopen_string_buffer(LuaState L);
    #endregion
    #region lauxlib.h
    public const int LUA_ERRFILE = LUA_ERRERR + 1;
    public const int LUA_NOREF = -2;
    public const int LUA_REFNIL = -1;

    [LibraryImport(Lib)]
    public static partial nint luaL_checkinteger(LuaState L, int numArg);

    [LibraryImport(Lib)]
    public static partial double luaL_checknumber(LuaState L, int numArg);

    [LibraryImport(Lib)]
    public static partial double luaL_optnumber(LuaState L, int numArg, double def);

    [LibraryImport(Lib, EntryPoint = "luaL_checklstring")]
    public static partial byte* _luaL_checklstring(LuaState L, int numArg, out nuint l);
    public static string luaL_checkstring(LuaState L, int numArg)
    {
        byte* strPtr = _luaL_checklstring(L, numArg, out var len);
        if (strPtr == null)
            return string.Empty;

        return Encoding.UTF8.GetString(strPtr, (int)len);
    }

    [LibraryImport(Lib, EntryPoint = "luaL_optlstring")]
    public static partial byte* _luaL_optlstring(LuaState L, int numArg, byte* def, nuint* l);
    public static string luaL_optstring(LuaState L, int numArg, string def)
    {
        nuint len;

        byte[]? defBytes = Encoding.UTF8.GetBytes(def + "\0");
        fixed (byte* defPtr = defBytes)
        {
            byte* strPtr = _luaL_optlstring(L, numArg, defPtr, &len);
            if (strPtr == null)
                return def;

            return Encoding.UTF8.GetString(strPtr, (int)len);
        }
    }

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int luaL_loadfile(LuaState L, string filename);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int luaL_loadbuffer(LuaState L, string buff, nuint sz, string name);
    [LibraryImport(Lib)]
    public static partial int luaL_loadbuffer(LuaState L, byte* buff, nuint sz, [MarshalAs(UnmanagedType.LPStr)] string name);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int luaL_loadstring(LuaState L, string s);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int luaL_error(LuaState L, string fmt);

    [LibraryImport(Lib)]
    public static partial byte* luaL_gsub(LuaState L, [MarshalAs(UnmanagedType.LPStr)] string s, [MarshalAs(UnmanagedType.LPStr)] string p, [MarshalAs(UnmanagedType.LPStr)] string r);

    #region Some useful macros

    #endregion
    #endregion
    #region luajit.h
    public enum LUAJIT_MODE
    {
        ENGINE,
        DEBUG,
        FUNC,
        ALLFUNC,
        ALLSUBFUNC,
        TRACE,
        WRAPCFUNC = 0x10,
        MAX,

        OFF = 0x0000,
        ON = 0x0100,
        FLUSH = 0x0200,
    }

    public const int LUAJIT_MODE_OFF = 0x0000;
    public const int LUAJIT_MODE_ON = 0x0100;
    public const int LUAJIT_MODE_FLUSH = 0x0200;

    [LibraryImport(Lib)]
    public static partial int luaJIT_setmode(LuaState L, int idx, LUAJIT_MODE mode);
    #endregion
    #region Entropy Specific Helpers

    public static int luaL_notimplemented(LuaState L, [CallerMemberName] string func = "")
        => luaL_error(L, $"function '{func}' is not implemented.");

    #endregion
}