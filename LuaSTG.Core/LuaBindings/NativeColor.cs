using luajit_sharp;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.Core.LuaBindings;

[StructLayout(LayoutKind.Sequential)]
public struct NativeColor : ILuaUserData
{
    public byte A;
    public byte R;
    public byte G;
    public byte B;

    public NativeColor(byte a, byte r, byte g, byte b)
    {
        A = a; R = r; G = g; B = b;
    }

    public uint Packed
    {
        readonly get => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;
        set
        {
            A = (byte)((value >> 24) & 0xFF);
            R = (byte)((value >> 16) & 0xFF);
            G = (byte)((value >> 8) & 0xFF);
            B = (byte)(value & 0xFF);
        }
    }

    public static string class_name => "lstg.Color";

    public static bool operator ==(NativeColor left, NativeColor right) => left.Packed == right.Packed;
    public static bool operator !=(NativeColor left, NativeColor right) => !(left == right);
    public override readonly bool Equals(object? obj) => obj is NativeColor other && this == other;
    public override readonly int GetHashCode() => (int)Packed;

    public override readonly string ToString() => $"lstg.Color({A}, {R}, {G}, {B})";
}
