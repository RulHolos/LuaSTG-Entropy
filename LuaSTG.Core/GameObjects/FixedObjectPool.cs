using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.GameObjects;

/// <summary>
/// Preallocates <paramref name="capacity"/> instances of <typeparamref name="T"/>
/// </summary>
public sealed class FixedObjectPool<T> where T : class, new()
{
    private readonly T[] slots;
    private readonly int[] freeStack;
    private int freeCount;
    
    public int Capacity { get; }

    public int Count { get; private set; }

    public FixedObjectPool(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
        slots = new T[capacity];
        for (var i = 0; i < capacity; i++)
            slots[i] = new T();
        freeStack = new int[capacity];
        Count = 0;
        ResetFreeStack();
    }

    private void ResetFreeStack()
    {
        for (var i = 0; i < Capacity; i++)
            freeStack[i] = Capacity - 1 - i;
        freeCount = Capacity;
    }

    public bool TryAlloc(out int id)
    {
        if (freeCount == 0)
        {
            id = -1;
            return false;
        }
        id = freeStack[--freeCount];
        Count++;
        return true;
    }

    public T Object(int id) => slots[id];

    public void Free(int id)
    {
        freeStack[freeCount++] = id;
        Count--;
    }

    public void Clear()
    {
        Count = 0;
        ResetFreeStack();
    }
}
