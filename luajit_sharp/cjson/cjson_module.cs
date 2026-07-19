using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace luajit_sharp.cjson;

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

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int Decode(LuaState L)
    {
        LuaStack ctx = new(L);
        try
        {
            var json = ctx.GetValue<string>(1);

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new CJsonException($"cjson: invalid JSON: {ex.Message}");
            }

            PushJsonNode(ctx, node);
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

        var isArray = true;
        long maxIntKey = 0;
        long keyCount = 0;

        lua_pushnil(L);
        while (lua_next(L, absIndex.Value))
        {
            keyCount++;
            if (isArray)
            {
                if (lua_type(L, -2) == LUA_TNUMBER)
                {
                    var keyNum = luaL_checknumber(L, -2);
                    var keyInt = (long)keyNum;
                    if (keyInt == keyNum && keyInt >= 1)
                        maxIntKey = Math.Max(maxIntKey, keyInt);
                    else
                        isArray = false;
                }
                else
                    isArray = false;
            }
            lua_settop(L, -2);
        }
        isArray = isArray && maxIntKey == keyCount;

        if (keyCount == 0)
            return new JsonObject();

        if (isArray)
        {
            var arr = new JsonArray();
            for (long i = 1; i <= maxIntKey; i++)
            {
                lua_rawgeti(L, absIndex.Value, (int)i);
                arr.Add(LuaValueToJsonNode(stack, -1, depth + 1));
                lua_settop(L, -2);
            }
            return arr;
        }
        else
        {
            var obj = new JsonObject();
            lua_pushnil(L);
            while (lua_next(L, absIndex.Value))
            {
                if (lua_type(L, -2) == LUA_TSTRING)
                {
                    var key = GetStringAt(L, -2);
                    obj[key] = LuaValueToJsonNode(stack, -1, depth + 1);
                }
                lua_settop(L, -2);
            }
            return obj;
        }
    }

    private static unsafe string GetStringAt(LuaState L, int idx)
    {
        var ptr = _lua_tolstring(L, idx, out var len);
        return Encoding.UTF8.GetString((byte*)ptr, checked((int)len));
    }

    private static void PushJsonNode(LuaStack stack, JsonNode? node)
    {
        if (node is null)
        {
            CJsonNull.Push(stack);
            return;
        }

        switch (node.GetValueKind())
        {
            case JsonValueKind.Object:
                PushJsonObject(stack, node.AsObject());
                break;
            case JsonValueKind.Array:
                PushJsonArray(stack, node.AsArray());
                break;
            case JsonValueKind.String:
                stack.Push(node.GetValue<string>());
                break;
            case JsonValueKind.Number:
                stack.Push(node.GetValue<double>());
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                stack.Push(node.GetValue<bool>());
                break;
            default:
                CJsonNull.Push(stack);
                break;
        }
    }

    private static void PushJsonObject(LuaStack stack, JsonObject obj)
    {
        var L = stack.L;
        var idx = stack.CreateMap(obj.Count);
        foreach (var (key, value) in obj)
        {
            stack.Push(key);
            PushJsonNode(stack, value);
            lua_settable(L, idx.Value);
        }
    }

    private static void PushJsonArray(LuaStack stack, JsonArray arr)
    {
        var L = stack.L;
        var idx = stack.CreateArray(arr.Count);
        for (int i = 0; i < arr.Count; i++)
        {
            PushJsonNode(stack, arr[i]);
            lua_rawseti(L, idx.Value, i + 1);
        }
    }

    #endregion
}