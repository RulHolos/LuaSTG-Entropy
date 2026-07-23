global using unsafe lua_CFunction = delegate* unmanaged[Cdecl]<luajit_sharp.LuaState, int>;
global using static luajit_sharp.LuaNative;
global using static LuaSTG.Core.Constants;

using luajit_sharp;
using LuaSTG.Core.Debugger;
using LuaSTG.LuaSTG;
using Steamworks;
using System.Diagnostics;
using LuaSTG.Core.Configuration;

namespace LuaSTG;

public static class SteamAPI_Helper
{
    public static bool Init()
    {
        if (HAVE_STEAM_API)
        {
            if (KEEP_LAUNCH_BY_STEAM)
            {
                if (SteamAPI.RestartAppIfNecessary(STEAM_APP_ID))
                {
                    return false;
                }
            }
            if (!SteamAPI.Init())
            {
                return false;
            }
        }

        return true;
    }
}

public class Program
{
    public static AppFrame LAPP = new();

    static void Main(string[] args)
    {
        long t1 = Stopwatch.GetTimestamp();

        // STAGE 1: Load application configuration

        ConfigurationLoader config_loader = ConfigurationLoader.Instance;
        if (ConfigurationLoader.Exists(LUASTG_CONFIGURATION_FILE) && !config_loader.LoadFromFile(LUASTG_CONFIGURATION_FILE))
        {
            Core.Window.WindowDevice.MessageBox(LUASTG_INFO, config_loader.GetFormattedMessage(), Silk.NET.SDL.MessageBoxFlags.Error);
            return;
        }

        // STAGE 2: Configure single instance

        using ApplicationSingleInstance singleInstance = new(LUASTG_INFO);
        if (config_loader.Application.SingleInstance)
            if (!singleInstance.Initialize(config_loader.Application.Uuid!))
                return;

        // STAGE 3: Start

        bool logRes = Logger.Create();

        long t2 = Stopwatch.GetTimestamp();
        double durationBeforeLog = Stopwatch.GetElapsedTime(t1, t2).TotalSeconds;
        Logger.core.Information($"Duration before logging system: {durationBeforeLog:F5}s");

        if (SteamAPI_Helper.Init())
        {
            if (LAPP.Init())
            {
                long t3 = Stopwatch.GetTimestamp();
                double durationAfterInit = Stopwatch.GetElapsedTime(t2, t3).TotalSeconds;
                Logger.core.Information($"Duration of intialization: {durationAfterInit:F5}s");

                LAPP.Run();
            }
            else
            {
                Core.Window.WindowDevice.MessageBox(
                    LUASTG_INFO,
                    "Engine initialization failed.\n" +
                    "Check the log file (engine.log) for more information.\n" +
                    "Please try to restart this application or contact the developers.",
                    Silk.NET.SDL.MessageBoxFlags.Error
                );
            }
            LAPP.Shutdown();
            SteamAPI.Shutdown();
        }
    }
}