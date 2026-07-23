using LuaSTG.Core.Configuration;
using Serilog;
using Serilog.Events;
using Silk.NET.OpenGL;

namespace LuaSTG.Core.Debugger;

public static class Logger
{
    public static ILogger core = Log.ForContext("Tag", "core");
    public static ILogger luajit = Log.ForContext("Tag", "luajit");
    public static ILogger luastg = Log.ForContext("Tag", "luastg");
    public static ILogger lua = Log.ForContext("Tag", "lua");

    private static void SetMinimumLevel(ref LoggerConfiguration cfg)
    {
        switch (ConfigurationLoader.Instance.Logging.Debugger.Threshold)
        {
            case ConfigurationLoader.LogLevel.Verbose:
                cfg.MinimumLevel.Verbose();
                break;
            case ConfigurationLoader.LogLevel.Debug:
                cfg.MinimumLevel.Debug();
                break;
            case ConfigurationLoader.LogLevel.Info:
                cfg.MinimumLevel.Information();
                break;
            case ConfigurationLoader.LogLevel.Warn:
                cfg.MinimumLevel.Warning();
                break;
            case ConfigurationLoader.LogLevel.Error:
                cfg.MinimumLevel.Error();
                break;
            case ConfigurationLoader.LogLevel.Fatal:
                cfg.MinimumLevel.Fatal();
                break;
        }
    }

    public static void WithLevel(this ILogger logger, int level, string text)
    {
        switch (level)
        {
            case 0:
                logger.Verbose(text);
                break;
            case 1:
                logger.Debug(text);
                break;
            case 2:
                logger.Information(text);
                break;
            case 3:
                logger.Warning(text);
                break;
            case 4:
                logger.Error(text);
                break;
            case 5:
                logger.Fatal(text);
                break;
        }
    }

    public static LogEventLevel GetMinLevel(ConfigurationLoader.LogLevel level)
    {
        return level switch
        {
            ConfigurationLoader.LogLevel.Verbose => LogEventLevel.Verbose,
            ConfigurationLoader.LogLevel.Debug => LogEventLevel.Debug,
            ConfigurationLoader.LogLevel.Info => LogEventLevel.Information,
            ConfigurationLoader.LogLevel.Warn => LogEventLevel.Warning,
            ConfigurationLoader.LogLevel.Error => LogEventLevel.Error,
            ConfigurationLoader.LogLevel.Fatal => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }

    public static bool Create()
    {
        LoggerConfiguration cfg = new();
        //SetMinimumLevel(ref cfg);

        const string template = "[{Timestamp:yyyy-mm-dd HH:mm:ss}] [{Level:u1}] [{Tag}] {Message:lj}{NewLine}{Exception}";

        //TODO: restrictedToMinimumLevel doesn't work, it's always Info

        var logcfg = ConfigurationLoader.Instance.Logging;

        if (logcfg.Console.Enable)
        {
            cfg.WriteTo.Console(
                outputTemplate: template,
                restrictedToMinimumLevel: GetMinLevel(logcfg.Console.Threshold)
            );
        }

        if (logcfg.File.Enable)
        {
            string pathToLog = Path.Combine(Directory.GetCurrentDirectory(), string.IsNullOrEmpty(logcfg.File.Path)
                ? "engine.log"
                : logcfg.File.Path);
            if (File.Exists(pathToLog))
                File.Delete(pathToLog);
            cfg.WriteTo.File(
                pathToLog,
                outputTemplate: template,
                restrictedToMinimumLevel: GetMinLevel(logcfg.File.Threshold)
            );
        }

        if (logcfg.RollingFile.Enable)
        {
            string secondPath = string.IsNullOrEmpty(logcfg.RollingFile.Path) ? "logs/" : logcfg.RollingFile.Path;
            string pathToLog = Path.Combine(Directory.GetCurrentDirectory(), secondPath, string.IsNullOrEmpty(logcfg.RollingFile.Path)
                ? "engine.log"
                : logcfg.RollingFile.Path);
            cfg.WriteTo.File(
                pathToLog,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: (int)logcfg.RollingFile.MaxHistory,
                restrictedToMinimumLevel: GetMinLevel(logcfg.RollingFile.Threshold)
            );
        }

        Log.Logger = cfg.CreateLogger();

        return true;
    }
}
