using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace luajit_sharp;

public readonly struct LuaStackBalancer : IDisposable
{
    private readonly LuaState L;
    private readonly int n;

    public LuaStackBalancer(LuaState state)
    {
        L = state;
        n = lua_gettop(state);
    }

    public void Dispose() => lua_settop(L, n);
}

public readonly record struct StackIndex(int Value)
{
    public static implicit operator StackIndex(int value) => new(value);
    public static implicit operator int(StackIndex index) => index.Value;

    public static bool operator >(StackIndex left, int right) => left.Value > right;
    public static bool operator >=(StackIndex left, int right) => left.Value >= right;
    public static bool operator <(StackIndex left, int right) => left.Value < right;
    public static bool operator <=(StackIndex left, int right) => left.Value <= right;
}

public interface ILuaUserData
{
    static abstract string class_name { get; }
}

public unsafe readonly partial struct LuaStack
{
    public readonly LuaState L;

    public LuaStack(LuaState state) => L = state;

    public StackIndex IndexOfTop() => lua_gettop(L);

    public void PopValue(int count = 1) => lua_settop(L, -count - 1);

    public void Push(bool value) => lua_pushboolean(L, value);
    public void Push(sbyte value) => lua_pushinteger(L, value);
    public void Push(byte value) => lua_pushinteger(L, value);
    public void Push(short value) => lua_pushinteger(L, value);
    public void Push(ushort value) => lua_pushinteger(L, value);
    public void Push(int value) => lua_pushinteger(L, value);

    public void Push(uint value)
    {
        if (value > int.MaxValue)
            lua_pushnumber(L, value);
        else
            lua_pushinteger(L, (int)value);
    }

    public void Push(long value) => lua_pushnumber(L, value);
    public void Push(ulong value) => lua_pushnumber(L, value);

    public void Push(float value) => lua_pushnumber(L, value);
    public void Push(double value) => lua_pushnumber(L, value);

    public void Push(string value) => lua_pushstring(L, value);
    public void Push(StackIndex value) => lua_pushvalue(L, value.Value);

    public void PushCFunction(lua_CFunction fn) => lua_pushcclosure(L, fn, 0);

    public void Push<T>(T value)
    {
        if (typeof(T) == typeof(bool)) { Push((bool)(object)value!); return; }
        if (typeof(T) == typeof(sbyte)) { Push((sbyte)(object)value!); return; }
        if (typeof(T) == typeof(byte)) { Push((byte)(object)value!); return; }
        if (typeof(T) == typeof(short)) { Push((short)(object)value!); return; }
        if (typeof(T) == typeof(ushort)) { Push((ushort)(object)value!); return; }
        if (typeof(T) == typeof(int)) { Push((int)(object)value!); return; }
        if (typeof(T) == typeof(uint)) { Push((uint)(object)value!); return; }
        if (typeof(T) == typeof(long)) { Push((long)(object)value!); return; }
        if (typeof(T) == typeof(ulong)) { Push((ulong)(object)value!); return; }
        if (typeof(T) == typeof(float)) { Push((float)(object)value!); return; }
        if (typeof(T) == typeof(double)) { Push((double)(object)value!); return; }
        if (typeof(T) == typeof(string)) { Push((string)(object)value!); return; }
        if (typeof(T) == typeof(StackIndex)) { Push((StackIndex)(object)value!); return; }
        if (typeof(T).IsEnum) { Push(Convert.ToInt64(value)); return; }
        throw new NotSupportedException($"No Lua push mapping for {typeof(T)}");
    }

    public void PushVector2<T>(T x, T y) where T : INumber<T>
    {
        var idx = CreateMap(2);
        SetMapValue(idx, "x", x);
        SetMapValue(idx, "y", y);
    }

    public void PushVector2(Vector2 vec) => PushVector2(vec.X, vec.Y);

    public StackIndex CreateArray(int size = 0)
    {
        lua_createtable(L, size, 0);
        return IndexOfTop();
    }

    public void SetArrayValueZeroBase<T>(int cIndex, T value)
    {
        Push(value);
        lua_rawseti(L, -2, cIndex + 1);
    }

    public void SetArrayValue<T>(StackIndex arrayIndex, StackIndex index, T value)
    {
        lua_pushinteger(L, index.Value);
        Push(value);
        lua_settable(L, arrayIndex.Value);
    }

    public void SetArrayValue(StackIndex arrayIndex, StackIndex index, IntPtr lightUserData)
    {
        lua_pushinteger(L, index.Value);
        lua_pushlightuserdata(L, lightUserData);
        lua_settable(L, arrayIndex.Value);
    }

    public nuint GetArraySize(StackIndex index) => lua_objlen(L, index.Value);

    public void PushArrayValueZeroBase(StackIndex arrayIndex, int cIndex) =>
        lua_rawgeti(L, arrayIndex.Value, cIndex + 1);

    public StackIndex CreateMap(int reserve = 0)
    {
        lua_createtable(L, 0, reserve);
        return IndexOfTop();
    }

    public void SetMapValue<T>(StackIndex index, string key, T value)
    {
        Push(key);
        Push(value);
        lua_settable(L, index.Value);
    }

    public void PushMapValue(StackIndex mapIndex, string key)
    {
        Push(key);
        lua_gettable(L, mapIndex.Value);
    }

    public void SetMapValue(StackIndex index, string key, lua_CFunction value)
    {
        Push(key);
        PushCFunction(value);
        lua_settable(L, index.Value);
    }

    public T GetMapValue<T>(StackIndex mapIndex, string key)
    {
        StackIndex topIndex = -1;
        Push(key);
        lua_gettable(L, mapIndex.Value);
        var result = GetValue<T>(topIndex);
        PopValue();
        return result;
    }

    public T GetMapValue<T>(StackIndex mapIndex, string key, T defaultValue)
    {
        StackIndex topIndex = -1;
        Push(key);
        lua_gettable(L, mapIndex.Value);
        var result = GetValue(topIndex, defaultValue);
        PopValue();
        return result;
    }

    public bool HasMapValue(StackIndex index, string key)
    {
        Push(key);
        lua_gettable(L, index.Value);
        var result = HasValue(-1) && !IsNil(-1);
        PopValue();
        return result;
    }

    public T GetValue<T>(StackIndex index)
    {
        if (typeof(T) == typeof(bool)) return (T)(object)lua_toboolean(L, index.Value);
        if (typeof(T) == typeof(sbyte)) return (T)(object)(sbyte)luaL_checkinteger(L, index.Value);
        if (typeof(T) == typeof(byte)) return (T)(object)(byte)luaL_checkinteger(L, index.Value);
        if (typeof(T) == typeof(short)) return (T)(object)(short)luaL_checkinteger(L, index.Value);
        if (typeof(T) == typeof(ushort)) return (T)(object)(ushort)luaL_checkinteger(L, index.Value);
        if (typeof(T) == typeof(int)) return (T)(object)(int)luaL_checkinteger(L, index.Value);
        if (typeof(T) == typeof(uint)) return (T)(object)(uint)luaL_checknumber(L, index.Value);
        if (typeof(T) == typeof(long)) return (T)(object)(long)luaL_checknumber(L, index.Value);
        if (typeof(T) == typeof(ulong)) return (T)(object)(ulong)luaL_checknumber(L, index.Value);
        if (typeof(T) == typeof(float)) return (T)(object)(float)luaL_checknumber(L, index.Value);
        if (typeof(T) == typeof(double)) return (T)(object)luaL_checknumber(L, index.Value);
        if (typeof(T) == typeof(string)) return (T)(object)GetCheckedString(index);
        if (typeof(T).IsEnum) return (T)Enum.ToObject(typeof(T), luaL_checkinteger(L, index.Value));
        throw new NotSupportedException($"No Lua get mapping for {typeof(T)}");
    }

    public T GetValue<T>(StackIndex index, T defaultValue) =>
        IsNoneOrNil(index) ? defaultValue : GetValue<T>(index);

    private string GetCheckedString(StackIndex index)
    {
        return luaL_checkstring(L, index.Value);
    }

    public unsafe T* CreateUserData<T>() where T : unmanaged
    {
        var ptr = lua_newuserdata(L, (nuint)sizeof(T));
        return (T*)ptr;
    }

    public unsafe T* AsUserData<T>(StackIndex index) where T : unmanaged, ILuaUserData
    {
        var ptr = luaL_checkudata(L, index.Value, T.class_name);
        return (T*)ptr;
    }

    public unsafe T* AsUserData<T>(StackIndex index, string className) where T : unmanaged
    {
        var ptr = luaL_checkudata(L, index.Value, className);
        return (T*)ptr;
    }

    public bool HasValue(StackIndex index) => lua_type(L, index.Value) != LUA_TNONE;

    public bool IsNoneOrNil(StackIndex index)
    {
        var t = lua_type(L, index.Value);
        return t == LUA_TNONE || t == LUA_TNIL;
    }

    public bool IsNil(StackIndex index) => lua_type(L, index.Value) == LUA_TNIL;
    public bool IsBoolean(StackIndex index) => lua_type(L, index.Value) == LUA_TBOOLEAN;
    public bool IsNumber(StackIndex index) => lua_type(L, index.Value) == LUA_TNUMBER;
    public bool IsString(StackIndex index) => lua_type(L, index.Value) == LUA_TSTRING;
    public bool IsTable(StackIndex index) => lua_type(L, index.Value) == LUA_TTABLE;
    public bool IsFunction(StackIndex index) => lua_type(L, index.Value) == LUA_TFUNCTION;
    public bool IsUserData(StackIndex index) => lua_type(L, index.Value) == LUA_TUSERDATA;
    public bool IsLightUserData(StackIndex index) => lua_type(L, index.Value) == LUA_TLIGHTUSERDATA;

    public StackIndex CreateModule(string name)
    {
        luaL_Reg[] list = [new(null, null)];
        fixed (luaL_Reg* reg = list)
            luaL_register(L, name, reg);
        return IndexOfTop();
    }

    public StackIndex PushModule(string name)
    {
        var n = lua_gettop(L);
        lua_getfield(L, LUA_REGISTRYINDEX, "_LOADED");
        lua_getfield(L, n + 1, name);
        lua_remove(L, n + 1);
        if (!lua_istable(L, n + 1))
            return luaL_error(L, $"module '{name} not found'");
        return n + 1;
    }

    public StackIndex CreateMetatable(string name)
    {
        luaL_newmetatable(L, name);
        return IndexOfTop();
    }

    public StackIndex PushMetatable(string name)
    {
        luaL_getmetatable(L, name);
        return IndexOfTop();
    }

    public void SetMetatable(StackIndex index, string name)
    {
        luaL_getmetatable(L, name);
        lua_setmetatable(L, index);
    }

    public bool IsMetatable(StackIndex index, string name)
    {
        if (!lua_getmetatable(L, index.Value))
            return false;
        var mtIndex = IndexOfTop();
        var namedMtIndex = PushMetatable(name);
        var result = lua_rawequal(L, mtIndex.Value, namedMtIndex.Value) != 0;
        PopValue(2);
        return result;
    }

    public void RaiseError(string message)
    {
        lua_pushstring(L, message);
        lua_error(L);
    }
}