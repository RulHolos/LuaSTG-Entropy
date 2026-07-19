using luajit_sharp;
using LuaSTG.Core.FileSystem;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.Core.GameObjects;

[StructLayout(LayoutKind.Sequential)]
public struct NativeArchiveWrapper : ILuaUserData
{
    private IntPtr _handle;

    public readonly FileSystemArchive? Archive =>
        _handle == IntPtr.Zero ? null : (FileSystemArchive?)GCHandle.FromIntPtr(_handle).Target;

    public static string class_name => "lstg.Archive";

    public void SetArchive(FileSystemArchive? archive)
    {
        Release();
        _handle = archive is not null ? GCHandle.ToIntPtr(GCHandle.Alloc(archive)) : IntPtr.Zero;
    }

    public void Release()
    {
        if (_handle != IntPtr.Zero)
        {
            GCHandle.FromIntPtr(_handle).Free();
            _handle = IntPtr.Zero;
        }
    }
}