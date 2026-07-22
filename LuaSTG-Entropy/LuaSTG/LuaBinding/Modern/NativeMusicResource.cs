using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.LuaBindings;
using LuaSTG.Core.Resources.Impl;
using LuaSTG.Core.Window.Audio;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding.Modern;

public unsafe partial class NativeMusicResource : ILuaBinding
{
    private const string class_name = "lstg.BGM";
    private const string ActiveTableField = "__lstg_bgm_active";

    private static void PushActiveTable(LuaState L)
    {
        lua_getfield(L, LUA_REGISTRYINDEX, ActiveTableField);
        if (lua_isnil(L, -1))
        {
            lua_pop(L, 1);
            lua_newtable(L);
            lua_pushvalue(L, -1);
            lua_setfield(L, LUA_REGISTRYINDEX, ActiveTableField);
        }
    }

    private static void Anchor(LuaState L, int index)
    {
        PushActiveTable(L);
        void* key = As(L, index);
        lua_pushinteger(L, (int)key);
        lua_pushvalue(L, index);
        lua_settable(L, -3);
        lua_pop(L, 1);
    }

    private static void Unanchor(LuaState L, void* key)
    {
        PushActiveTable(L);
        lua_pushinteger(L, (int)key);
        lua_pushnil(L);
        lua_settable(L, -3);
        lua_pop(L, 1);
    }

    private struct Handle
    {
        public IntPtr GcHandle;
    }

    private sealed class State
    {
        public required MusicResource Resource;
        public BgmChannel? Channel;
        public float PendingVolume = 1f;
    }

    #region Interop Helpers

    private static bool Is(LuaState L, int index)
    {
        LuaStack ctx = new(L);
        return ctx.IsMetatable(index, class_name);
    }

    private static Handle* As(LuaState L, int index)
    {
        void* userdata = lua_touserdata(L, index);
        if (userdata == null)
            luaL_error(L, $"Expected {class_name} userdata pointer, got null.");
        return (Handle*)userdata;
    }

    private static State? Resolve(Handle* handle)
    {
        if (handle->GcHandle == IntPtr.Zero)
            return null;

        var gcHandle = GCHandle.FromIntPtr(handle->GcHandle);
        return gcHandle.Target as State;
    }

    public static MusicResource? Get(LuaState L, int index)
        => Resolve(As(L, index))?.Resource;

    public static void Push(LuaState L, MusicResource resource)
    {
        LuaStack ctx = new(L);

        var self = ctx.CreateUserData<Handle>();
        self->GcHandle = GCHandle.ToIntPtr(GCHandle.Alloc(new State { Resource = resource }));

        ctx.SetMetatable(ctx.IndexOfTop(), class_name);
    }

    #endregion
    #region Metamethods

    [LuaBind]
    public static int __gc(LuaState L)
    {
        var self = As(L, 1);

        if (self->GcHandle != IntPtr.Zero)
        {
            var gcHandle = GCHandle.FromIntPtr(self->GcHandle);

            if (gcHandle.Target is State state)
            {
                state.Channel?.Dispose();
                state.Resource.RequestUnload(); //Deferred.
            }

            gcHandle.Free();
            self->GcHandle = IntPtr.Zero;
        }

        return 0;
    }

    [LuaBind]
    public static int __tostring(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        var state = Resolve(self);

        ctx.Push(state is null ? $"{class_name} (unloaded)" : $"{class_name} ({state.Resource.Name})");
        return 1;
    }

    #endregion
    #region Methods

    [LuaBind]
    public static int Play(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        var state = Resolve(self);

        if (state is null)
        {
            ctx.RaiseError($"{class_name} has already been unloaded.");
            return 0;
        }

        float volume = (float)luaL_optnumber(L, 2, state.PendingVolume);

        bool loop = true;
        if (!lua_isnoneornil(L, 3))
            loop = lua_toboolean(L, 3);

        state.Channel ??= Program.LAPP.WindowDevice.AudioDevice.CreateBgmChannel();
        state.PendingVolume = volume;

        bool success = state.Channel.Play(state.Resource, volume, loop);
        if (success)
            Anchor(L, 1);

        ctx.Push(success);
        return 1;
    }

    [LuaBind]
    public static int Pause(LuaState L)
    {
        var self = As(L, 1);
        Resolve(self)?.Channel?.Pause();
        return 0;
    }

    [LuaBind]
    public static int Resume(LuaState L)
    {
        var self = As(L, 1);
        Resolve(self)?.Channel?.Resume();
        return 0;
    }

    [LuaBind]
    public static int Stop(LuaState L)
    {
        var self = As(L, 1);
        Resolve(self)?.Channel?.Stop();
        Unanchor(L, self); //TODO: Make unanchor when the music stops naturally (no loop)
        return 0;
    }

    [LuaBind]
    public static int SetVolume(LuaState L)
    {
        var self = As(L, 1);
        var state = Resolve(self);
        float volume = (float)luaL_checknumber(L, 2);

        if (state is null)
            return 0;

        state.PendingVolume = volume;
        state.Channel?.SetVolume(volume);
        return 0;
    }

    [LuaBind]
    public static int GetVolume(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        var state = Resolve(self);

        ctx.Push(state?.PendingVolume ?? 0f);
        return 1;
    }

    [LuaBind]
    public static int IsPlaying(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        var state = Resolve(self);

        ctx.Push(state?.Channel?.IsPlaying ?? false);
        return 1;
    }

    [LuaBind]
    public static int GetName(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        var state = Resolve(self);

        if (state is null)
        {
            ctx.RaiseError($"{class_name} has already been unloaded.");
            return 0;
        }

        ctx.Push(state.Resource.Name);
        return 1;
    }

    #endregion

    public static void Register(LuaState L)
    {
        LuaWrapper.EnsureLSTGInStack(L);
        Logger.luastg.Verbose($"Registering bindings: 'lstg.BGM'");

        LuaStack ctx = new(L);

        //Methods
        StackIndex methodTable = ctx.CreateModule(class_name);
        ctx.SetMapValue(methodTable, "Play", CFunctions.Play);
        ctx.SetMapValue(methodTable, "Stop", CFunctions.Stop);
        ctx.SetMapValue(methodTable, "Pause", CFunctions.Pause);
        ctx.SetMapValue(methodTable, "Resume", CFunctions.Resume);
        ctx.SetMapValue(methodTable, "SetVolume", CFunctions.SetVolume);
        ctx.SetMapValue(methodTable, "GetVolume", CFunctions.GetVolume);
        ctx.SetMapValue(methodTable, "IsPlaying", CFunctions.IsPlaying);
        ctx.SetMapValue(methodTable, "GetName", CFunctions.GetName);

        //Metamethods
        StackIndex metatable = ctx.CreateMetatable(class_name);
        ctx.SetMapValue(metatable, "__tostring", CFunctions.__tostring);
        ctx.SetMapValue(metatable, "__gc", CFunctions.__gc);

        //Direct poiting
        ctx.SetMapValue(metatable, "__index", methodTable);
    }
}
