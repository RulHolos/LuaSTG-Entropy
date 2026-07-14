using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace luajit_sharp;

public unsafe static class LuaPrinter
{
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int CustomPrint(LuaState L)
    {
        int n = lua_gettop(L);

        lua_getglobal(L, "tostring");
        int tostringTargetIndex = lua_gettop(L);

        var sb = new StringBuilder();

        for (int i = 1; i <= n; i++)
        {
            lua_pushvalue(L, tostringTargetIndex);
            lua_pushvalue(L, i);

            lua_call(L, 1, 1);

            string? resultStr = lua_tostring(L, -1);
            if (resultStr != null)
                sb.Append(resultStr);

            if (i < n)
                sb.Append('\t');

            lua_pop(L, 1);
        }

        string finalOutput = sb.ToString();
        Console.WriteLine(finalOutput);

        return 0;
    }

    /*
    public static void Register(LuaState L)
    {
        var printPtr = (delegate* unmanaged[Cdecl]<LuaState, int>)&CustomPrint;
        lua_pushcclosure(L, printPtr, 0);
        lua_setglobal(L, "print");
    }
    */
}
