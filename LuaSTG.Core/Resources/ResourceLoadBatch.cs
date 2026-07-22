using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LuaSTG.Core.Resources;

public sealed class ResourceLoadBatch
{
    private readonly ConcurrentQueue<Action> finalizers = [];
    private readonly ConcurrentQueue<(string Name, string Message)> errors = [];
    private int completed;
    public int Total { get; private set; }
    public int Completed => Volatile.Read(ref completed);

    internal ResourceLoadBatch(int total) => Total = total;

    public bool IsDone
    {
        get
        {
            Pump();
            return Completed >= Total && finalizers.IsEmpty;
        }
    }

    public bool HasErrors => !errors.IsEmpty;

    internal void EnqueueFinalizer(Action finalizer) => finalizers.Enqueue(finalizer);
    internal void ReportError(string name, Exception ex) => errors.Enqueue((name, ex.Message));
    internal void MarkOneCompleted() => Interlocked.Increment(ref completed);

    public void Pump()
    {
        while (finalizers.TryDequeue(out var finalizer))
            finalizer();
    }

    public (string Name, string Message)[] GetErrors()
    {
        Pump();
        return [.. errors];
    }
}
