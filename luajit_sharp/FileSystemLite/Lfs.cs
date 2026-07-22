using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace luajit_sharp.FileSystemLite;

public static unsafe class Lfs
{
    private enum NodeKind { Other, File, Directory }

    private static void SetAttributesTable(LuaStack ctx, StackIndex index, NodeKind kind, int size)
    {
        ctx.SetMapValue(index, "dev", 0);
        ctx.SetMapValue(index, "ino", 0);
        ctx.SetMapValue(index, "mode", kind switch
        {
            NodeKind.File => "file",
            NodeKind.Directory => "directory",
            _ => "other",
        });
        ctx.SetMapValue(index, "nlink", 0);
        ctx.SetMapValue(index, "uid", 0);
        ctx.SetMapValue(index, "gid", 0);
        ctx.SetMapValue(index, "rdev", 0);
        ctx.SetMapValue(index, "access", 0);
        ctx.SetMapValue(index, "modification", 0);
        ctx.SetMapValue(index, "change", 0);
        ctx.SetMapValue(index, "size", size);
        ctx.SetMapValue(index, "permissions", "-r--r--r--");
        ctx.SetMapValue(index, "blocks", 0);
        ctx.SetMapValue(index, "blksize", 0);
    }

    //Putain ça c'est de la fonction
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int Attributes(LuaState L)
    {
        LuaStack ctx = new(L);
        try
        {
            string path = ctx.GetValue<string>(1);

            if (lua_isstring(L, 2))
            {
                string requestName = ctx.GetValue<string>(2);
                
                if (requestName == "mode")
                {
                    if (File.Exists(path))
                    {
                        ctx.Push("file");
                        return 1;
                    }
                    if (Directory.Exists(path))
                    {
                        ctx.Push("directory");
                        return 1;
                    }
                    ctx.PushNil();
                    return 1;
                }

                if (requestName == "size")
                {
                    long size = 0;
                    try {
                        size = new FileInfo(path).Length;
                    }
                    catch
                    {
                        size = 0;
                    }
                    ctx.Push(size);
                    return 1;
                }

                ctx.RaiseError("not supported");
                return 0;
            }
            else
            {
                var kind = File.Exists(path) ? NodeKind.File : Directory.Exists(path) ? NodeKind.Directory : NodeKind.Other;
                int size = 0;
                
                try
                {
                    size = (int)new FileInfo(path).Length;
                }
                catch (Exception ex)
                {
                    size = 0;
                }

                if (lua_istable(L, 2))
                {
                    SetAttributesTable(ctx, 2, kind, size);
                    lua_pushvalue(L, 2);
                }
                else
                {
                    StackIndex table = ctx.CreateMap(14);
                    SetAttributesTable(ctx, table, kind, size);
                }
                return 1;
            }
        }
        catch (Exception ex)
        {
            ctx.RaiseError($"lfd.attributes: {ex.Message}");
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int ChDir(LuaState L)
    {
        LuaStack ctx = new(L);
        string path = ctx.GetValue<string>(1);
        
        try
        {
            Directory.SetCurrentDirectory(path);
            ctx.Push(true);
            return 1;
        }
        catch (Exception ex)
        {
            ctx.PushNil();
            ctx.Push(ex.Message);
            lua_pushinteger(L, ex.HResult);
            return 3;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int CurrentDir(LuaState L)
    {
        LuaStack ctx = new(L);
        try
        {
            ctx.Push(Directory.GetCurrentDirectory());
            return 1;
        }
        catch (Exception ex)
        {
            ctx.PushNil();
            ctx.Push(ex.Message);
            lua_pushinteger(L, ex.HResult);
            return 3;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int MkDir(LuaState L)
    {
        LuaStack ctx = new(L);
        string path = ctx.GetValue<string>(1);

        if (Directory.Exists(path))
        {
            ctx.PushNil();
            ctx.Push("The operation completed successfully");
            lua_pushinteger(L, 0);
            return 3;
        }

        try
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                throw new DirectoryNotFoundException("The system cannot find the path specified.");

            Directory.CreateDirectory(path);
            ctx.Push(true);
            return 1;
        }
        catch (Exception ex)
        {
            ctx.PushNil();
            ctx.Push(ex.Message);
            lua_pushinteger(L, ex.HResult);
            return 3;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int RmDir(LuaState L)
    {
        LuaStack ctx = new(L);
        string path = ctx.GetValue<string>(1);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            else
                Directory.Delete(path, recursive: false);
            ctx.Push(true);
            return 1;
        }
        catch (Exception ex)
        {
            ctx.PushNil();
            ctx.Push(ex.Message);
            lua_pushinteger(L, ex.HResult);
            return 3;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int NotSupported(LuaState L)
    {
        luaL_error(L, "not supported");
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeDirIterator : ILuaUserData
    {
        public static string class_name => "lfs.dir";

        private IntPtr handle;

        public readonly IEnumerator<string>? Enumerator =>
            handle == IntPtr.Zero ? null : (IEnumerator<string>?)GCHandle.FromIntPtr(handle).Target;

        public void SetEnumerator(IEnumerator<string> enumerator)
        {
            Release();
            handle = GCHandle.ToIntPtr(GCHandle.Alloc(enumerator));
        }

        public void Release()
        {
            if (this.handle == IntPtr.Zero)
                return;
            var handle = GCHandle.FromIntPtr(this.handle);
            (handle.Target as IDisposable)?.Dispose();
            handle.Free();
            this.handle = IntPtr.Zero;
        }
    }

    private static class DirObj
    {
        public static NativeDirIterator* As(LuaState L, int index)
        {
            LuaStack ctx = new(L);
            return ctx.AsUserData<NativeDirIterator>(index);
        }

        public static void Create(LuaState L, string path)
        {
            LuaStack ctx = new(L);
            var self = ctx.CreateUserData<NativeDirIterator>();
            var selfIndex = ctx.IndexOfTop();
            *self = default;
            self->SetEnumerator(Directory.EnumerateFileSystemEntries(path).GetEnumerator());
            ctx.SetMetatable(selfIndex, NativeDirIterator.class_name);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int Next(LuaState L)
        {
            LuaStack ctx = new(L);
            var self = As(L, 1);
            var enumerator = self->Enumerator;

            if (enumerator is null)
            {
                ctx.PushNil();
                return 1;
            }

            try
            {
                if (!enumerator.MoveNext())
                {
                    ctx.PushNil();
                    return 1;
                }
            }
            catch
            {
                ctx.PushNil();
                return 1;
            }

            ctx.Push(Path.GetFileName(enumerator.Current));
            return 1;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int Close(LuaState L)
        {
            var self = As(L, 1);
            self->Release();
            return 0;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int MetaToString(LuaState L)
        {
            _ = As(L, 1);
            LuaStack ctx = new(L);
            ctx.Push(NativeDirIterator.class_name);
            return 1;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int MetaGC(LuaState L)
        {
            var self = As(L, 1);
            self->Release();
            return 0;
        }

        private static readonly luaL_Reg[] tMethods =
        [
            new("next", &Next),
            new("close", &Close),

            new(),
        ];

        private static readonly luaL_Reg[] tMetaTable =
        [
            new("__tostring", &MetaToString),
            new("__gc", &MetaGC),

            new(),
        ];

        public static void Register(LuaState L)
        {
            var stack = new LuaStack(L);
            luaL_newmetatable(L, NativeDirIterator.class_name);

            lua_createtable(L, 0, 2);
            fixed (luaL_Reg* methodsPtr = tMethods)
                luaL_register(L, methodsPtr);
            lua_setfield(L, -2, "__index");

            fixed (luaL_Reg* metaPtr = tMetaTable)
                luaL_register(L, metaPtr);

            stack.PopValue();
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int Dir(LuaState L)
    {
        LuaStack ctx = new(L);
        string path;
        try
        {
            path = ctx.GetValue<string>(1);
        }
        catch (Exception ex)
        {
            ctx.RaiseError($"lfs.dir: {ex.Message}");
            return 0;
        }

        if (!Directory.Exists(path))
        {
            ctx.RaiseError($"'{path}' is not a directory");
            return 0;
        }

        lua_CFunction nextFn = &DirObj.Next;
        ctx.PushCFunction(nextFn);
        DirObj.Create(L, path);
        return 2;
    }

    private static readonly luaL_Reg[] lib =
    [
        new("attributes", &Attributes),
        new("chdir", &ChDir),
        new("lock_dir", &NotSupported),
        new("currentdir", &CurrentDir),
        new("dir", &Dir),
        new("lock", &NotSupported),
        new("link", &NotSupported),
        new("mkdir", &MkDir),
        new("rmdir", &RmDir),
        new("setmode", &NotSupported),
        new("symlinkattributes", &NotSupported),
        new("touch", &NotSupported),
        new("unlock", &NotSupported),

        new(),
    ];

    public static void Register(LuaState L)
    {
        LuaStack ctx = new(L);

        DirObj.Register(L);

        fixed (luaL_Reg* libPtr = lib)
            luaL_register(L, "lfs", libPtr);

        ctx.PopValue();
    }
}