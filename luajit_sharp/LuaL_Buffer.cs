using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace luajit_sharp;

public unsafe static partial class LuaNative
{
    public const int LUAL_BUFFERSIZE = 512;

    [LibraryImport(Lib)]
    public static partial void luaL_buffinit(LuaState L, luaL_Buffer* B);

    [LibraryImport(Lib)]
    public static partial byte* luaL_prepbuffer(luaL_Buffer* B);

    [LibraryImport(Lib)]
    public static partial void luaL_addlstring(luaL_Buffer* B, byte* s, nuint l);

    [LibraryImport(Lib)]
    public static partial void luaL_addstring(luaL_Buffer* B, byte* s);

    [LibraryImport(Lib)]
    public static partial void luaL_addvalue(luaL_Buffer* B);

    [LibraryImport(Lib)]
    public static partial void luaL_pushresult(luaL_Buffer* B);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void luaL_addchar(luaL_Buffer* B, byte c)
    {
        if (B->p >= B->buffer + LUAL_BUFFERSIZE)
            luaL_prepbuffer(B);

        *B->p = c;
        B->p++;
    }

    // compatibility only
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void luaL_putchar(luaL_Buffer* B, byte c) => luaL_addchar(B, c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void luaL_addsize(luaL_Buffer* B, int n)
    {
        B->p += n;
    }

    public static void luaL_addstring(luaL_Buffer* B, string s)
    {
        if (string.IsNullOrEmpty(s)) return;

        byte[] bytes = Encoding.UTF8.GetBytes(s);
        fixed (byte* ptr = bytes)
        {
            luaL_addlstring(B, ptr, (nuint)bytes.Length);
        }
    }

    public static void luaL_addspan(luaL_Buffer* B, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return;

        fixed (byte* ptr = data)
        {
            luaL_addlstring(B, ptr, (nuint)data.Length);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct luaL_Buffer
{
    public byte* p;
    public int lvl;
    public LuaState L;

    public fixed byte buffer[LuaNative.LUAL_BUFFERSIZE];
}
