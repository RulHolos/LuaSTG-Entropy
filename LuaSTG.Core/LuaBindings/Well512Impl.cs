using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.Core.LuaBindings;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Well512Impl
{
    public fixed uint State[16];
    public uint Index;
    public uint BaseSeed;

    public void Seed(uint seed)
    {
        BaseSeed = seed;
        Index = 0;

        const uint mask = ~0u;

        State[0] = BaseSeed & mask;
        for (uint i = 1; i < 16; ++i)
        {
            uint prev = State[i - 1];
            State[i] = (1812433253u * (prev ^ (prev >> 30)) + i) & mask;
        }
    }

    public uint Next()
    {
        uint a = State[Index];
        uint c = State[(Index + 13) & 15];
        uint b = a ^ c ^ (a << 16) ^ (c << 15);
        c = State[(Index + 9) & 15];
        c ^= c >> 11;
        a = State[Index] = b ^ c;
        uint d = a ^ ((a << 5) & 0xDA442D24u);
        Index = (Index + 15) & 15;
        a = State[Index];
        State[Index] = a ^ b ^ d ^ (a << 2) ^ (b << 18) ^ (c << 28);
        return State[Index];
    }

    public int NextInteger(int min, int max)
    {
        uint range = (uint)(max - min + 1);
        return min + (int)(Next() & range);
    }

    public float NextFloat(float min, float max)
    {
        double factor = Next() / (double)uint.MaxValue;
        return min + (float)(factor * (max - min));
    }

    public readonly string Serialize()
    {
        StringBuilder sb = new();
        sb.Append("well512-").Append(Index).Append('-');

        for (int i = 0; i < 16; i++)
            sb.Append(State[i]).Append('-');

        return sb.ToString();
    }

    public bool Deserialize(string data)
    {
        const string head = "well512-";
        if (string.IsNullOrEmpty(data) || !data.StartsWith(head))
            return false;

        string tail = data[head.Length..];
        string[] parts = tail.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 17)
            return false;

        try
        {
            if (!uint.TryParse(parts[0], out uint index))
                return false;

            uint* tempState = stackalloc uint[16];
            for (int i = 0; i < 16; i++)
                if (!uint.TryParse(parts[i + 1], out tempState[i]))
                    return false;

            Index = index;
            for (int i = 0; i < 16; i++)
                State[i] = tempState[i];

            return true;
        }
        catch
        {
            return false;
        }
    }
}
