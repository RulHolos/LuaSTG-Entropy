using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace luajit_sharp;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct luaL_Reg
{
    public byte* name;
    public lua_CFunction func;

    public luaL_Reg(string? name, lua_CFunction func)
    {
        this.name = (byte*)Marshal.StringToHGlobalAnsi(name);
        this.func = func;
    }

    public luaL_Reg()
    {
        this.name = (byte*)Marshal.StringToHGlobalAnsi(null);
        this.func = null;
    }
}

public unsafe static partial class LuaNative
{
    [LibraryImport(Lib, EntryPoint = "luaL_register")]
    public static partial void _luaL_register(LuaState L, byte* libname, luaL_Reg* l);

    public static void luaL_register(LuaState L, luaL_Reg* l)
    {
        _luaL_register(L, null, l);
    }

    public static void luaL_register(LuaState L, string libname, luaL_Reg* l)
    {
        if (libname == null)
        {
            _luaL_register(L, null, l);
            return;
        }

        IntPtr nativeString = Marshal.StringToHGlobalAnsi(libname);
        try
        {
            _luaL_register(L, (byte*)nativeString, l);
        }
        finally
        {
            Marshal.FreeHGlobal(nativeString);
        }
    }
}