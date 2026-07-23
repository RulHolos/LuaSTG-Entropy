using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding.Modern;

[StructLayout(LayoutKind.Sequential)]
public struct NativeVector3
{
    public float X;
    public float Y;
    public float Z;

    public NativeVector3() { }

    public NativeVector3(float x, float y, float z)
    {
        X = y;
        Y = y;
        Z = z;
    }

    public bool Equals(NativeVector3 other) => other.X == X && other.Y == Y;
    public override bool Equals(object? obj) => obj is NativeVector3 other && Equals(other);
    public override int GetHashCode() => (X, Y).GetHashCode();

    public static bool operator ==(NativeVector3 left, NativeVector3 right) => left.Equals(right);
    public static bool operator !=(NativeVector3 left, NativeVector3 right) => !left.Equals(right);
    public static NativeVector3 operator +(NativeVector3 left, NativeVector3 right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    public static NativeVector3 operator -(NativeVector3 left, NativeVector3 right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public override readonly string ToString() => $"({X}, {Y})";
}

public unsafe partial class Vector3 : ILuaBinding
{
    private const string class_name = "lstg.Vector3";

    #region Interop Helpers
    public static bool Is(LuaState L, int index)
    {
        LuaStack ctx = new(L);
        return ctx.IsMetatable(index, class_name);
    }

    public static NativeVector3* As(LuaState L, int index)
    {
        void* userdata = lua_touserdata(L, index);
        if (userdata == null)
            luaL_error(L, $"Expected {class_name} userdata pointer, got null.");
        return (NativeVector3*)userdata;
    }

    public static NativeVector3* Create(LuaState L)
    {
        LuaStack ctx = new(L);
        NativeVector3* self = ctx.CreateUserData<NativeVector3>();
        StackIndex self_index = ctx.IndexOfTop();
        ctx.SetMetatable(self_index, class_name);
        self->X = 0f;
        self->Y = 0f;
        self->Z = 0f;
        return self;
    }
    #endregion
    #region Metamethods

    [LuaBind]
    public static int __tostring(LuaState L)
    {
        LuaStack ctx = new(L);
        ctx.Push<string>(class_name);
        return 1;
    }

    [LuaBind]
    public static int __index(LuaState L)
    {
        LuaStack ctx = new(L);
        NativeVector3* self = As(L, 1);
        string key = ctx.GetValue<string>(2);

        if (key == "x") { ctx.Push<float>(self->X); }
        else if (key == "y") { ctx.Push<float>(self->Y); }
        else if (key == "z") { ctx.Push<float>(self->Z); }
        else
        {
            StackIndex method_table = ctx.PushModule(class_name);
            ctx.PushMapValue(method_table, key);
            if (ctx.IsNil(ctx.IndexOfTop()))
                return luaL_error(L, $"field '{key}' doesn't exist");
        }
        return 1;
    }

    [LuaBind]
    public static int __newindex(LuaState L)
    {
        LuaStack ctx = new(L);
        NativeVector3* self = As(L, 1);
        string key = ctx.GetValue<string>(2);
        float value = ctx.GetValue<float>(3);

        if (key == "x") { self->X = value; }
        else if (key == "y") { self->Y = value; }
        else if (key == "z") { self->Z = value; }
        else return luaL_error(L, $"field '{key}' doesn't exist");

        return 1;
    }

    [LuaBind]
    public static int __eq(LuaState L)
    {
        if (Is(L, 1) && Is(L, 2))
        {
            NativeVector3* left = As(L, 1);
            NativeVector3* right = As(L, 2);
            lua_pushboolean(L, left->X == right->X && left->Y == right->Y && left->Z == right->Z);
        }
        else
        {
            lua_pushboolean(L, false);
        }
        return 1;
    }

    [LuaBind]
    public static int __add(LuaState L)
    {
        NativeVector3* res = Create(L);
        if (Is(L, 1))
        {
            NativeVector3* left = As(L, 1);
            if (Is(L, 2))
            {
                NativeVector3* right = As(L, 2);
                res->X = left->X + right->X;
                res->Y = left->Y + right->Y;
                res->Z = left->Z + right->Z;
            }
            else
            {
                float scalar = (float)lua_tonumber(L, 2);
                res->X = left->X + scalar;
                res->Y = left->Y + scalar;
                res->Z = left->Z + scalar;
            }
        }
        else
        {
            float scalar = (float)lua_tonumber(L, 1);
            NativeVector3* right = As(L, 2);
            res->X = scalar + right->X;
            res->Y = scalar + right->Y;
            res->Z = scalar + right->Z;
        }
        return 1;
    }

    [LuaBind]
    public static int __sub(LuaState L)
    {
        NativeVector3* res = Create(L);
        if (Is(L, 1))
        {
            NativeVector3* left = As(L, 1);
            if (Is(L, 2))
            {
                NativeVector3* right = As(L, 2);
                res->X = left->X - right->X;
                res->Y = left->Y - right->Y;
                res->Z = left->Z - right->Z;
            }
            else
            {
                float scalar = (float)lua_tonumber(L, 2);
                res->X = left->X - scalar;
                res->Y = left->Y - scalar;
                res->Z = left->Z - scalar;
            }
        }
        else
        {
            float scalar = (float)lua_tonumber(L, 1);
            NativeVector3* right = As(L, 2);
            res->X = scalar - right->X;
            res->Y = scalar - right->Y;
            res->Z = scalar - right->Z;
        }
        return 1;
    }

    [LuaBind]
    public static int __mul(LuaState L)
    {
        NativeVector3* res = Create(L);
        if (Is(L, 1))
        {
            NativeVector3* left = As(L, 1);
            if (Is(L, 2))
            {
                NativeVector3* right = As(L, 2);
                res->X = left->X * right->X;
                res->Y = left->Y * right->Y;
                res->Z = left->Z * right->Z;
            }
            else
            {
                float scalar = (float)lua_tonumber(L, 2);
                res->X = left->X * scalar;
                res->Y = left->Y * scalar;
                res->Z = left->Z * scalar;
            }
        }
        else
        {
            float scalar = (float)lua_tonumber(L, 1);
            NativeVector3* right = As(L, 2);
            res->X = scalar * right->X;
            res->Y = scalar * right->Y;
            res->Z = scalar * right->Z;
        }
        return 1;
    }

    [LuaBind]
    public static int __div(LuaState L)
    {
        NativeVector3* res = Create(L);
        if (Is(L, 1))
        {
            NativeVector3* left = As(L, 1);
            if (Is(L, 2))
            {
                NativeVector3* right = As(L, 2);
                res->X = left->X / right->X;
                res->Y = left->Y / right->Y;
                res->Z = left->Z / right->Z;
            }
            else
            {
                float scalar = (float)lua_tonumber(L, 2);
                res->X = left->X / scalar;
                res->Y = left->Y / scalar;
                res->Z = left->Z / scalar;
            }
        }
        else
        {
            float scalar = (float)lua_tonumber(L, 1);
            NativeVector3* right = As(L, 2);
            res->X = scalar / right->X;
            res->Y = scalar / right->Y;
            res->Z = scalar / right->Z;
        }
        return 1;
    }

    [LuaBind]
    public static int __unm(LuaState L)
    {
        NativeVector3* self = As(L, 1);
        NativeVector3* res = Create(L);
        res->X = -self->X;
        res->Y = -self->Y;
        res->Z = -self->Z;
        return 1;
    }

    #endregion
    #region Methods
    [LuaBind]
    public static int length(LuaState L)
    {
        NativeVector3* self = As(L, 1);
        lua_pushnumber(L, MathF.Sqrt(self->X * self->X + self->Y * self->Y + self->Z * self->Z));
        return 1;
    }

    [LuaBind]
    public static int angle(LuaState L)
    {
        NativeVector3* self = As(L, 1);

        float horizontalLength = MathF.Sqrt(self->X * self->X + self->Y * self->Y);

        float yaw = MathF.Atan2(self->Y, self->X);
        float pitch = MathF.Atan2(self->Z, horizontalLength);

        lua_pushnumber(L, yaw);
        lua_pushnumber(L, pitch);

        return 2;
    }

    [LuaBind]
    public static int normalize(LuaState L)
    {
        NativeVector3* self = As(L, 1);
        float len = MathF.Sqrt(self->X * self->X + self->Y * self->Y + self->Z * self->Z);
        if (len > 0f)
        {
            self->X /= len;
            self->Y /= len;
            self->Z /= len;
        }
        lua_pushvalue(L, 1);
        return 1;
    }

    [LuaBind]
    public static int normalized(LuaState L)
    {
        NativeVector3* self = As(L, 1);
        NativeVector3* res = Create(L);
        float len = MathF.Sqrt(self->X * self->X + self->Y * self->Y + self->Z * self->Z);
        if (len > 0f)
        {
            res->X = self->X / len;
            res->Y = self->Y / len;
            res->Z = self->Z / len;
        }
        return 1;
    }

    [LuaBind]
    public static int dot(LuaState L)
    {
        NativeVector3* self = As(L, 1);
        NativeVector3* other = As(L, 2);
        lua_pushnumber(L, (self->X * other->X) + (self->Y * other->Y) + (self->Z * other->Z));
        return 1;
    }

    [LuaBind]
    public static int create(LuaState L)
    {
        int top = lua_gettop(L);
        NativeVector3* self = Create(L);
        if (top >= 2)
        {
            self->X = (float)lua_tonumber(L, 1);
            self->Y = (float)lua_tonumber(L, 2);
            self->Z = (float)lua_tonumber(L, 3);
        }
        return 1;
    }
    #endregion

    /// <summary>
    /// Registers the `lstg.Vector2` module. Callable as `require("lstg.Vector2")` in lua.
    /// </summary>
    /// <param name="L"></param>
    public static void Register(LuaState L)
    {
        LuaWrapper.EnsureLSTGInStack(L);
        Logger.luastg.Verbose($"Registering bindings: 'Vector3'");

        LuaStack ctx = new(L);

        //Methods
        StackIndex method_table = ctx.CreateModule(class_name);
        ctx.SetMapValue(method_table, "length", CFunctions.length);
        ctx.SetMapValue(method_table, "angle", CFunctions.angle);
        ctx.SetMapValue(method_table, "normalize", CFunctions.normalize);
        ctx.SetMapValue(method_table, "normalized", CFunctions.normalized);
        ctx.SetMapValue(method_table, "dot", CFunctions.dot);
        ctx.SetMapValue(method_table, "create", CFunctions.create);

        //Metamethods
        StackIndex metatable = ctx.CreateMetatable(class_name);
        ctx.SetMapValue(metatable, "__tostring", CFunctions.__tostring);
        ctx.SetMapValue(metatable, "__tostring", CFunctions.__tostring);
        ctx.SetMapValue(metatable, "__index", CFunctions.__index);
        ctx.SetMapValue(metatable, "__newindex", CFunctions.__newindex);
        ctx.SetMapValue(metatable, "__eq", CFunctions.__eq);
        ctx.SetMapValue(metatable, "__add", CFunctions.__add);
        ctx.SetMapValue(metatable, "__sub", CFunctions.__sub);
        ctx.SetMapValue(metatable, "__mul", CFunctions.__mul);
        ctx.SetMapValue(metatable, "__div", CFunctions.__div);
        ctx.SetMapValue(metatable, "__unm", CFunctions.__unm);
    }
}
