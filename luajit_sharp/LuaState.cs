global using static luajit_sharp.LuaNative;
using System;

namespace luajit_sharp;

public unsafe readonly struct LuaState : IEquatable<LuaState>
{
    private readonly void* _ptr;

    public LuaState(IntPtr ptr) => _ptr = (void*)ptr;
    public LuaState(void* ptr) => _ptr = ptr;

    public bool IsNull => _ptr == null;

    //Implicit conversions
    public static implicit operator void*(LuaState state) => state._ptr;
    public static implicit operator IntPtr(LuaState state) => (IntPtr)state._ptr;
    public static explicit operator LuaState(IntPtr ptr) => new(ptr);

    //Equality Checks
    public bool Equals(LuaState other) => _ptr == other._ptr;
    public override bool Equals(object? obj) => obj is LuaState other && Equals(other);
    public override int GetHashCode() => ((IntPtr)_ptr).GetHashCode();

    public static bool operator ==(LuaState left, LuaState right) => left.Equals(right);
    public static bool operator !=(LuaState left, LuaState right) => !left.Equals(right);

    public override string ToString() => $"lua_State* [0x{(long)_ptr:X}]";
}
