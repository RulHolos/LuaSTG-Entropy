using luajit_sharp;
using LuaSTG.Core.FileSystem;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using LuaSTG.Core.Rendering;
using LuaSTG.Core.Resources;
using LuaSTG.Core.GameObjects;
using LuaSTG.Core.Window;

namespace LuaSTG.LuaSTG;

public enum AppStatus
{
    NotInitialized,
    Initializing,
    Initialized,
    Running,
    Aborted,
    Destroyed,
}

public partial class AppFrame
{
    public LuaState L { get; set; }
    public AppStatus Status { get; set; } = AppStatus.NotInitialized;

    public GameObjectPool? ObjectPool;

    public WindowDevice WindowDevice { get; private set; }

    public bool Init()
    {
        Debug.Assert(Status == AppStatus.NotInitialized);

        Logger.core.Information(LUASTG_INFO);
        Logger.luastg.Information("Initializing Engine");
        Status = AppStatus.Initializing;

        //////////////////////////////////////// Base

        ConfigurationLoader config_loader = ConfigurationLoader.Instance;
        var resources = config_loader.FileSystemConfig.Resources;
        foreach (var resource in resources)
        {
            var type = resource.Type;
            switch (type)
            {
                case ConfigurationLoader.ResourceType.Directory:
                    FileSystemManager.AddSearchPath(resource.Path!);
                    break;
                case ConfigurationLoader.ResourceType.Archive:
                    if (FileSystemArchive.TryCreateFromFile(resource.Path!, out var arc))
                        FileSystemManager.AddFileSystem(resource.Name!, arc!);
                    break;
            }
        }

        //////////////////////////////////////// Allocate space for object pools

        Logger.luastg.Information($"Initializing object pool with capacity: {GameObjectPool.Capacity}");
        ObjectPool = new();

        //////////////////////////////////////// Initialize async resource loader

        //////////////////////////////////////// Initialize Lua

        Logger.luastg.Information("Initializing LuaJIT");

        if (!OnOpenLuaEngine())
        {
            Logger.luastg.Information("Failed to initialize LuaJIT");
            return false;
        }

        if (!OnLoadLaunchScriptAndFiles())
            return false;

        //////////////////////////////////////// Initialize Engine

        //TODO: Create window, audio engine, input, ...
        WindowDevice = new();
        WindowDevice.Initialize();

        SetupWindowEvents();

        if (!OnLoadMainScriptAndFiles())
            return false;

        //////////////////////////////////////// Finalization

        Status = AppStatus.Initialized;
        Logger.luastg.Information("Initialization Complete");

        if (!SafeCallGlobalFunction("GameInit"))
            return false;

        return true;
    }

    public void Run()
    {
        Debug.Assert(Status == AppStatus.Initialized);
        Logger.luastg.Information("Start Update & Render Loop");

        WindowDevice.Run();

        Logger.luastg.Information("Exiting Update & Render Loop");
    }

    public void Shutdown()
    {
        WindowDevice?.Dispose();

        if (!L.IsNull)
            SafeCallGlobalFunction("GameExit");

        if (!L.IsNull)
        {
            lua_close(L);
            Logger.luastg.Information("LuaJIT shutdown");
        }

        Status = AppStatus.Destroyed;
        Logger.luastg.Information("Engine shutdown");

        Console.ReadLine();
    }

    private void SetupWindowEvents()
    {
        WindowDevice?.RenderEngine?.OnFrame += () =>
        {
            if (!SafeCallGlobalFunction("FrameFunc", 1))
            {
                Logger.luastg.Information("GameFrame returned false, exiting loop");
                WindowDevice?.RequestExit();
            }
        };
        WindowDevice?.RenderEngine?.OnRender += () =>
        {
            bool result = SafeCallGlobalFunction("RenderFunc");
            if (!result)
                WindowDevice?.RequestExit();
        };

        WindowDevice?.Window.FocusChanged += (focus) =>
        {
            if (focus)
            {
                if (!SafeCallGlobalFunction("FocusGainFunc"))
                    WindowDevice?.RequestExit();
            }
            else
            {
                if (!SafeCallGlobalFunction("FocusLoseFunc"))
                    WindowDevice?.RequestExit();
            }
        };
    }

    #region Helpers

    public int LoadTextFile(LuaState L, string path, string packname)
    {
        if (packname != null)
            Logger.luastg.Information($"Reading text file '{path}' in package '{packname}'");
        else
            Logger.luastg.Information($"Reading text file '{path}'");

        bool loaded = false;
        byte[] src = [];
        if (packname != null)
        {
            if (FileSystemManager.TryGetArchiveByPath(packname, out FileSystemArchive? arc))
            {
                loaded = arc!.ReadFile(path, out byte[]? srcb);
                src = srcb ?? [];
            }
        }
        else
        {
            loaded = FileSystemManager.ReadFile(path, out byte[]? srcb);
            src = srcb ?? [];
        }

        if (!loaded)
        {
            Logger.luastg.Error($"Unable to load file '{path}'");
            luaL_error(L, $"can't load file '{path}'");
            return 0;
        }
        lua_pushstring(L, Encoding.UTF8.GetString(src));
        return 1;
    }

    #endregion
}
