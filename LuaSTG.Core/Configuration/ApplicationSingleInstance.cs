global using unsafe lua_CFunction = delegate* unmanaged[Cdecl]<luajit_sharp.LuaState, int>;
global using static luajit_sharp.LuaNative;
global using static LuaSTG.Core.Constants;

using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.Configuration;

public sealed class ApplicationSingleInstance : IDisposable
{
    private readonly string appName;
    private FileStream? lockStream;

    public ApplicationSingleInstance(string appName)
    {
        this.appName = appName;
    }

    /// <summary>
    /// Attempts to become the single running instance for the given UUID.
    /// </summary>
    /// <param name="uuid"></param>
    /// <returns>True if the process acquired the lock; otherwise, false if another instance already holds it.</returns>
    public bool Initialize(string uuid)
    {
        if (string.IsNullOrEmpty(uuid))
            throw new ArgumentException("uuid must be set when single_instance is enabled", nameof(uuid));

        string dir = Path.Combine(Path.GetTempPath(), "single-instance-locks");
        Directory.CreateDirectory(dir);
        string lockPath = Path.Combine(dir, $"{uuid}.lock");

        try
        {
            lockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        lockStream?.Dispose();
        lockStream = null;
    }
}
