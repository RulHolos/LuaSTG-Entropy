using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.LuaBindings;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding.Modern;

public unsafe partial class Color : ILuaBinding, ILuaUserData
{
    private const string class_name = "lstg.Color";
    static string ILuaUserData.class_name => class_name;
    private const double Inv255 = 1.0 / 255.0;

    #region Interop Helpers

    public static bool Is(LuaState L, int index)
    {
        LuaStack ctx = new(L);
        return ctx.IsMetatable(index, class_name);
    }

    public static NativeColor* As(LuaState L, int index)
    {
        LuaStack ctx = new(L);
        return ctx.AsUserData<NativeColor>(index);
    }

    public static NativeColor* Create(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = ctx.CreateUserData<NativeColor>();
        var selfIndex = ctx.IndexOfTop();
        ctx.SetMetatable(selfIndex, class_name);
        *self = default;
        return self;
    }

    public static void CreateAndPush(LuaState L, NativeColor color)
    {
        var self = Create(L);
        *self = color;
    }

    #endregion
    #region Color math

    private static byte ClampByte(nint value) => (byte)Math.Clamp((long)value, 0, 255);
    private static byte ClampToByte(double value) => (byte)Math.Clamp(value, 0.0, 255.0);

    private static (float H, float S, float V) RgbToHsv(float r, float g, float b)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var v = max;
        var s = max <= 0f ? 0f : delta / max;

        float h;
        if (delta <= 0f)
            h = 0f;
        else if (max == r)
            h = ((g - b) / delta) % 6f;
        else if (max == g)
            h = (b - r) / delta + 2f;
        else
            h = (r - g) / delta + 4f;
        h /= 6f;
        if (h < 0f) h += 1f;

        return (h, s, v);
    }

    private static (float R, float G, float B) HsvToRgb(float h, float s, float v)
    {
        var h6 = h * 6f;
        var i = (int)MathF.Floor(h6) % 6;
        if (i < 0) i += 6;
        var f = h6 - MathF.Floor(h6);
        var p = v * (1f - s);
        var q = v * (1f - f * s);
        var t = v * (1f - (1f - f) * s);

        return i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
    }

    private static NativeColor Hsv2Rgb(float hue, float saturation, float value, float alpha)
    {
        var (r, g, b) = HsvToRgb(hue * 0.01f, saturation * 0.01f, value * 0.01f);
        return new NativeColor(
            a: ClampToByte(Math.Clamp(alpha * 0.01f, 0f, 1f) * 255f),
            r: ClampToByte(Math.Clamp(r, 0f, 1f) * 255f),
            g: ClampToByte(Math.Clamp(g, 0f, 1f) * 255f),
            b: ClampToByte(Math.Clamp(b, 0f, 1f) * 255f)
        );
    }

    private static (float Hue, float Saturation, float Value, float Alpha) Rgb2Hsv(byte r, byte g, byte b, byte a)
    {
        var (h, s, v) = RgbToHsv(r / 255f, g / 255f, b / 255f);
        return (h * 100f, s * 100f, v * 100f, a / 255f * 100f);
    }

    #endregion
    #region Dispatch

    private enum ColorMember { None, A, R, G, B, H, S, V, Argb, FuncArgb, FuncAhsv }

    private static ColorMember MapColorMember(ReadOnlySpan<byte> key)
    {
        return key switch
        {
            _ when key.SequenceEqual("a"u8) => ColorMember.A,
            _ when key.SequenceEqual("r"u8) => ColorMember.R,
            _ when key.SequenceEqual("g"u8) => ColorMember.G,
            _ when key.SequenceEqual("b"u8) => ColorMember.B,
            _ when key.SequenceEqual("h"u8) => ColorMember.H,
            _ when key.SequenceEqual("s"u8) => ColorMember.S,
            _ when key.SequenceEqual("v"u8) => ColorMember.V,
            _ when key.SequenceEqual("argb"u8) => ColorMember.Argb,
            _ when key.SequenceEqual("ARGB"u8) => ColorMember.FuncArgb,
            _ when key.SequenceEqual("AHSV"u8) => ColorMember.FuncAhsv,
            _ => ColorMember.None,
        };
    }

    #endregion
    #region Methods

    [LuaBind]
    public static int ARGB(LuaState L)
    {
        LuaStack ctx = new(L);
        var p = As(L, 1);
        var argc = lua_gettop(L);
        if (argc == 1)
        {
            lua_pushinteger(L, p->A);
            lua_pushinteger(L, p->R);
            lua_pushinteger(L, p->G);
            lua_pushinteger(L, p->B);
            return 4;
        }
        else if (argc == 2)
        {
            p->Packed = (uint)luaL_checknumber(L, 2);
            return 0;
        }
        else if (argc == 5)
        {
            p->A = ClampByte(luaL_checkinteger(L, 2));
            p->R = ClampByte(luaL_checkinteger(L, 3));
            p->G = ClampByte(luaL_checkinteger(L, 4));
            p->B = ClampByte(luaL_checkinteger(L, 5));
            return 0;
        }
        else
        {
            ctx.RaiseError("Invalid args.");
            return 0;
        }
    }

    [LuaBind]
    public static int AHSV(LuaState L)
    {
        LuaStack ctx = new(L);
        var p = As(L, 1);
        var argc = lua_gettop(L);
        if (argc == 1)
        {
            var (hue, saturation, value, alpha) = Rgb2Hsv(p->R, p->G, p->B, p->A);
            lua_pushnumber(L, alpha);
            lua_pushnumber(L, hue);
            lua_pushnumber(L, saturation);
            lua_pushnumber(L, value);
            return 4;
        }
        else if (argc == 5)
        {
            var alpha = (float)Math.Clamp(luaL_checknumber(L, 2), 0.0, 100.0);
            var hue = (float)Math.Clamp(luaL_checknumber(L, 3), 0.0, 100.0);
            var saturation = (float)Math.Clamp(luaL_checknumber(L, 4), 0.0, 100.0);
            var value = (float)Math.Clamp(luaL_checknumber(L, 5), 0.0, 100.0);
            *p = Hsv2Rgb(hue, saturation, value, alpha);
            return 0;
        }
        else
        {
            ctx.RaiseError("Invalid args.");
            return 0;
        }
    }

    [LuaBind]
    public static int New(LuaState L)
    {
        if (lua_gettop(L) == 1)
        {
            var color = new NativeColor { Packed = (uint)luaL_checknumber(L, 1) };
            CreateAndPush(L, color);
        }
        else
        {
            var a = ClampByte(luaL_checkinteger(L, 1));
            var r = ClampByte(luaL_checkinteger(L, 2));
            var g = ClampByte(luaL_checkinteger(L, 3));
            var b = ClampByte(luaL_checkinteger(L, 4));
            CreateAndPush(L, new NativeColor(a, r, g, b));
        }
        return 1;
    }

    [LuaBind]
    public static int HSVColor(LuaState L)
    {
        var alpha = (float)Math.Clamp(luaL_checknumber(L, 1), 0.0, 100.0);
        var hue = (float)Math.Clamp(luaL_checknumber(L, 2), 0.0, 100.0);
        var saturation = (float)Math.Clamp(luaL_checknumber(L, 3), 0.0, 100.0);
        var value = (float)Math.Clamp(luaL_checknumber(L, 4), 0.0, 100.0);
        CreateAndPush(L, Hsv2Rgb(hue, saturation, value, alpha));
        return 1;
    }

    [LuaBind]
    public static int White(LuaState L)
    {
        CreateAndPush(L, new NativeColor(255, 255, 255, 255));
        return 1;
    }

    [LuaBind]
    public static int Black(LuaState L)
    {
        CreateAndPush(L, new NativeColor(255, 0, 0, 0));
        return 1;
    }

    #endregion
    #region Metamethods

    [LuaBind]
    public static int Meta_Index(LuaState L)
    {
        LuaStack ctx = new(L);
        var p = As(L, 1);
        var keyPtr = _luaL_checklstring(L, 2, out var len);
        var key = new ReadOnlySpan<byte>(keyPtr, checked((int)len));

        switch (MapColorMember(key))
        {
            case ColorMember.A: lua_pushinteger(L, p->A); break;
            case ColorMember.R: lua_pushinteger(L, p->R); break;
            case ColorMember.G: lua_pushinteger(L, p->G); break;
            case ColorMember.B: lua_pushinteger(L, p->B); break;
            case ColorMember.H: lua_pushnumber(L, Rgb2Hsv(p->R, p->G, p->B, p->A).Hue); break;
            case ColorMember.S: lua_pushnumber(L, Rgb2Hsv(p->R, p->G, p->B, p->A).Saturation); break;
            case ColorMember.V: lua_pushnumber(L, Rgb2Hsv(p->R, p->G, p->B, p->A).Value); break;
            case ColorMember.Argb: lua_pushnumber(L, p->Packed); break;
            case ColorMember.FuncArgb: ctx.PushCFunction(CFunctions.ARGB); break;
            case ColorMember.FuncAhsv: ctx.PushCFunction(CFunctions.AHSV); break;
            default:
                ctx.RaiseError("Invalid index key.");
                break;
        }
        return 1;
    }

    [LuaBind]
    public static int Meta_NewIndex(LuaState L)
    {
        var ctx = new LuaStack(L);
        var p = As(L, 1);
        var keyPtr = _luaL_checklstring(L, 2, out var len);
        var key = new ReadOnlySpan<byte>(keyPtr, checked((int)len));

        switch (MapColorMember(key))
        {
            case ColorMember.A: p->A = ClampByte(luaL_checkinteger(L, 3)); break;
            case ColorMember.R: p->R = ClampByte(luaL_checkinteger(L, 3)); break;
            case ColorMember.G: p->G = ClampByte(luaL_checkinteger(L, 3)); break;
            case ColorMember.B: p->B = ClampByte(luaL_checkinteger(L, 3)); break;
            case ColorMember.H:
                {
                    var (Hue, Saturation, Value, Alpha) = Rgb2Hsv(p->R, p->G, p->B, p->A);
                    var hue = (float)Math.Clamp(luaL_checknumber(L, 3), 0.0, 100.0);
                    *p = Hsv2Rgb(hue, Saturation, Value, Alpha);
                    break;
                }
            case ColorMember.S:
                {
                    var (Hue, Saturation, Value, Alpha) = Rgb2Hsv(p->R, p->G, p->B, p->A);
                    var saturation = (float)Math.Clamp(luaL_checknumber(L, 3), 0.0, 100.0);
                    *p = Hsv2Rgb(Hue, saturation, Value, Alpha);
                    break;
                }
            case ColorMember.V:
                {
                    var (Hue, Saturation, Value, Alpha) = Rgb2Hsv(p->R, p->G, p->B, p->A);
                    var value = (float)Math.Clamp(luaL_checknumber(L, 3), 0.0, 100.0);
                    *p = Hsv2Rgb(Hue, Saturation, value, Alpha);
                    break;
                }
            case ColorMember.Argb:
                p->Packed = (uint)luaL_checknumber(L, 3);
                break;
            default:
                ctx.RaiseError("Invalid index key.");
                return 0;
        }
        return 0;
    }

    [LuaBind]
    public static int Meta_Eq(LuaState L)
    {
        var pA = As(L, 1);
        var pB = As(L, 2);
        lua_pushboolean(L, *pA == *pB);
        return 1;
    }

    private static void ArithMetamethod(LuaState L, Func<double, double, double> scalarOp, Func<double, double, double> channelOp)
    {
        NativeColor result;

        if (lua_isnumber(L, 1))
        {
            var v = luaL_checknumber(L, 1);
            var p = As(L, 2);
            result = new NativeColor(
                a: ClampToByte(scalarOp(v, p->A)),
                r: ClampToByte(scalarOp(v, p->R)),
                g: ClampToByte(scalarOp(v, p->G)),
                b: ClampToByte(scalarOp(v, p->B)));
        }
        else if (lua_isnumber(L, 2))
        {
            var v = luaL_checknumber(L, 2);
            var p = As(L, 1);
            result = new NativeColor(
                a: ClampToByte(scalarOp(p->A, v)),
                r: ClampToByte(scalarOp(p->R, v)),
                g: ClampToByte(scalarOp(p->G, v)),
                b: ClampToByte(scalarOp(p->B, v)));
        }
        else
        {
            var pA = As(L, 1);
            var pB = As(L, 2);
            result = new NativeColor(
                a: ClampToByte(channelOp(pA->A, pB->A)),
                r: ClampToByte(channelOp(pA->R, pB->R)),
                g: ClampToByte(channelOp(pA->G, pB->G)),
                b: ClampToByte(channelOp(pA->B, pB->B)));
        }

        CreateAndPush(L, result);
    }

    [LuaBind]
    private static int Meta_Add(LuaState L)
    {
        ArithMetamethod(L, static (x, y) => x + y, static (x, y) => x + y);
        return 1;
    }

    [LuaBind]
    private static int Meta_Sub(LuaState L)
    {
        ArithMetamethod(L, static (x, y) => x - y, static (x, y) => x - y);
        return 1;
    }

    [LuaBind]
    private static int Meta_Mul(LuaState L)
    {
        ArithMetamethod(L,
            static (x, y) => x * y,
            static (x, y) => 255.0 * ((x * Inv255) * (y * Inv255)));
        return 1;
    }

    [LuaBind]
    private static int Meta_Div(LuaState L)
    {
        ArithMetamethod(L,
            static (x, y) => x / y,
            static (x, y) => 255.0 * ((x * Inv255) / (y * Inv255)));
        return 1;
    }

    [LuaBind]
    private static int Meta_ToString(LuaState L)
    {
        var p = As(L, 1);
        lua_pushstring(L, $"lstg.Color({p->A}, {p->R}, {p->G}, {p->B})");
        return 1;
    }

    #endregion

    private static readonly luaL_Reg[] tMethods =
    [
        new("ARGB", CFunctions.ARGB),
        new("AHSV", CFunctions.AHSV),

        new(null, null),
    ];

    private static readonly luaL_Reg[] tMetaTable =
    [
        new("__index", CFunctions.Meta_Index),
        new("__newindex", CFunctions.Meta_NewIndex),
        new("__eq", CFunctions.Meta_Eq),
        new("__add", CFunctions.Meta_Add),
        new("__sub", CFunctions.Meta_Sub),
        new("__mul", CFunctions.Meta_Mul),
        new("__div", CFunctions.Meta_Div),
        new("__tostring", CFunctions.Meta_ToString),

        new(null, null),
    ];

    private static readonly luaL_Reg[] lib =
    [
        new("Color", CFunctions.New),
        new("HSVColor", CFunctions.HSVColor),

        //Entropy only
        new("WhiteColor", CFunctions.White),
        new("BlackColor", CFunctions.Black),

        new(null, null),
    ];

    public static void Register(LuaState L)
    {
        LuaWrapper.EnsureLSTGInStack(L);
        Logger.luastg.Verbose($"Registering bindings: 'Color'");

        fixed (luaL_Reg* libPtr = lib)
            luaL_register(L, LuaWrapper.LUASTG_LUA_LIBNAME, libPtr);
        fixed (luaL_Reg* tMethodsPtr = tMethods, tMetaTablePtr = tMetaTable)
            LuaWrapper.RegisterClassIntoTable2(L, ".Color", tMethodsPtr, class_name, tMetaTablePtr);
        lua_pop(L, 1);
    }
}
