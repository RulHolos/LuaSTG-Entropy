using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.GameObjects;

public sealed class IntrusiveLinkedList<T> where T : class
{
    private readonly Func<T, T?> getPrevious;
    private readonly Action<T, T?> setPrevious;
    private readonly Func<T, T?> getNext;
    private readonly Action<T, T?> setNext;

    private T? first;
    private T? last;

    public IntrusiveLinkedList(
        Func<T, T?> getPrevious, Action<T, T?> setPrevious,
        Func<T, T?> getNext, Action<T, T?> setNext)
    {
        this.getPrevious = getPrevious;
        this.setPrevious = setPrevious;
        this.getNext = getNext;
        this.setNext = setNext;
    }

    public bool IsEmpty => first is null && last is null;

    public T? First => first;

    public void Add(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsEmpty)
        {
            first = item;
            last = item;
            setPrevious(item, null);
            setNext(item, null);
        }
        else
        {
            var last = this.last!;
            setNext(last, item);
            setPrevious(item, last);
            last = item;
        }
    }

    public T? Remove(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsEmpty)
            throw new InvalidOperationException("List is empty.");

        if (ReferenceEquals(first, item) && ReferenceEquals(last, item))
        {
            first = null;
            last = null;
            return null;
        }

        var previous = getPrevious(item);
        var next = getNext(item);
        if (previous is not null)
            setNext(previous, next);
        if (next is not null)
            setPrevious(next, previous);
        setPrevious(item, null);
        setNext(item, null);
        if (ReferenceEquals(first, item))
            first = next;
        if (ReferenceEquals(last, item))
            last = previous;
        return next;
    }

    public void Clear()
    {
        var item = first;
        while (item is not null)
        {
            var current = item;
            item = getNext(item);
            setPrevious(current, null);
            setNext(current, null);
        }
        first = null;
        last = null;
    }
}
