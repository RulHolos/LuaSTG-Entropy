using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace luajit_sharp;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct lua_Debug
{
    public const int LUA_IDSIZE = 60; // <-- verify against your luaconf.h

    public int Event;

    public IntPtr NamePtr;
    public IntPtr NameWhatPtr;
    public IntPtr WhatPtr;
    public IntPtr SourcePtr;

    public int CurrentLine;
    public int NumUpvalues;
    public int LineDefined;
    public int LastLineDefined;

    public fixed byte ShortSrcBuffer[LUA_IDSIZE];

    internal int PrivateActiveFunctionIndex;

    public readonly string? Name => PtrToStringUtf8OrNull(NamePtr);
    public readonly string? NameWhat => PtrToStringUtf8OrNull(NameWhatPtr);
    public readonly string? What => PtrToStringUtf8OrNull(WhatPtr);
    public readonly string? Source => PtrToStringUtf8OrNull(SourcePtr);

    public readonly string ShortSrc
    {
        get
        {
            fixed (byte* p = ShortSrcBuffer)
            {
                var len = 0;
                while (len < LUA_IDSIZE && p[len] != 0) len++;
                return Encoding.UTF8.GetString(p, len);
            }
        }
    }

    private static string? PtrToStringUtf8OrNull(IntPtr ptr)
        => ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
}