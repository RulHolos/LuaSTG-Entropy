using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;

namespace luajit_sharp.cjson;

/*

public static class CJsonNull
{
    private static readonly IntPtr Token = Marshal.AllocHGlobal(1);

    public static void Push(LuaStack stack) => lua_pushlightuserdata(stack.L, Token);

    public static bool Is(LuaStack stack, StackIndex index) =>
        stack.IsLightUserData(index) && lua_topointer(stack.L, index.Value) == Token;
}

internal sealed class CJsonException(string message) : Exception(message)
{
}

/// <summary>
/// C# re-implementation of lua-cjson. (encode/decode/null)
/// </summary>
public static class CJsonModule
{
    private const int MaxDepth = 1000;
    private const string class_name = "cjson";

    public static unsafe void Register(LuaState L)
    {
        LuaStack ctx = new(L);

        StackIndex module = ctx.CreateModule(class_name);
        ctx.SetMapValue(module, "encode", &Encode);
        ctx.SetMapValue(module, "decode", &Decode);
        ctx.Push("null");
        CJsonNull.Push(ctx);
        lua_settable(L, module.Value);

        ctx.PopValue();
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int Encode(LuaState L)
    {
        LuaStack ctx = new(L);
        try
        {
            var node = LuaValueToJsonNode(ctx, 1, 0);
            ctx.Push(node is null ? "null" : node.ToJsonString());
            return 1;
        }
        catch (CJsonException ex)
        {
            ctx.RaiseError(ex.Message);
            return 0;
        }
    }

    #region Impl

    private static JsonNode? LuaValueToJsonNode(LuaStack stack, StackIndex index, int depth)
    {
        if (depth > MaxDepth)
            throw new CJsonException("cjson: nested too deeply, maybe a circular reference?");

        if (stack.IsNoneOrNil(index) || CJsonNull.Is(stack, index))
            return null; //Null representation

        if (stack.IsBoolean(index))
            return JsonValue.Create(stack.GetValue<bool>(index));

        if (stack.IsNumber(index))
        {
            double d = stack.GetValue<double>(index);
            if (double.IsNaN(d) || double.IsInfinity(d))
                throw new CJsonException("cjson: cannot serialize NaN or Infinity");
            return JsonValue.Create(d);
        }

        if (stack.IsString(index))
            return JsonValue.Create(stack.GetValue<string>(index));

        if (stack.IsTable(index))
            return LuaTableToJsonNode(stack, index, depth);

        throw new CJsonException("cjson: cannot serialize this value type");
    }

    private static JsonNode LuaTableToJsonNode(LuaStack stack, StackIndex index, int depth)
    {
        LuaState L = stack.L;
        var absIndex = stack.AbsIndex(index);
        //TODO: Continue;
    }

    #endregion
}
*/