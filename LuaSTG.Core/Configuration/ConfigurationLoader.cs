using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LuaSTG.Core.Configuration;

internal static class JsonValidationExtensions
{
    public static string GetTypeName(this JsonNode? node)
    {
        if (node is null)
            return "null";

        return node.GetValueKind() switch
        {
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Null => "null",
            _ => "unknown",
        };
    }

    private static bool RequireKind(this JsonNode? node, string path, List<string> messages, string expectedName, params JsonValueKind[] allowed)
    {
        var kind = node?.GetValueKind() ?? JsonValueKind.Null;
        if (Array.IndexOf(allowed, kind) >= 0)
            return true;
        messages.Add($"[{path}] require {expectedName} type, but obtain {node.GetTypeName()}");
        return false;
    }

    public static bool RequireBoolean(this JsonNode? node, string path, List<string> messages) =>
        node.RequireKind(path, messages, "boolean", JsonValueKind.True, JsonValueKind.False);

    public static bool RequireString(this JsonNode? node, string path, List<string> messages) =>
        node.RequireKind(path, messages, "string", JsonValueKind.String);

    public static bool RequireArray(this JsonNode? node, string path, List<string> messages) =>
        node.RequireKind(path, messages, "array", JsonValueKind.Array);

    public static bool RequireObject(this JsonNode? node, string path, List<string> messages) =>
        node.RequireKind(path, messages, "object", JsonValueKind.Object);

    public static bool RequireNumber(this JsonNode? node, string path, List<string> messages) =>
        node.RequireKind(path, messages, "number", JsonValueKind.Number);

    public static bool RequireUnsignedInteger(this JsonNode? node, string path, List<string> messages)
    {
        if (node is JsonValue value && value.TryGetValue<uint>(out _))
            return true;
        messages.Add($"[{path}] require unsigned integer type, but obtain {node.GetTypeName()}");
        return false;
    }
}

public sealed class ConfigurationLoader
{
    public sealed class DebugSettings
    {
        public bool TrackWindowFocus { get; set; }
    }

    public sealed class ApplicationSettings
    {
        public string? Uuid { get; set; }
        public bool SingleInstance { get; set; }
        public bool HasUuid => !string.IsNullOrEmpty(Uuid);
    }

    public enum LogLevel { Verbose, Debug, Info, Warn, Error, Fatal }

    public class LogSink
    {
        public virtual bool Enable { get; set; }
        public LogLevel Threshold { get; set; } = LogLevel.Debug;
    }

    public sealed class ConsoleLogSink : LogSink
    {
        public override bool Enable { get; set; } = true;
    }

    public sealed class FileLogSink : LogSink
    {
        public override bool Enable { get; set; } = true;
        public string? Path { get; set; } = "engine.log";
    }

    public sealed class RollingFileLogSink : LogSink
    {
        public string? Path { get; set; }
        public uint MaxHistory { get; set; }
    }

    public sealed class LoggingSettings
    {
        public LogSink Debugger { get; } = new();
        public ConsoleLogSink Console { get; } = new();
        public FileLogSink File { get; } = new();
        public RollingFileLogSink RollingFile { get; } = new();
    }

    public enum ResourceType { Directory, Archive }

    public sealed class ResourceFileSystem
    {
        public string? Name { get; set; }
        public string? Path { get; set; }
        public ResourceType Type { get; set; } = ResourceType.Directory;
    }

    public sealed class FileSystemSettings
    {
        public List<ResourceFileSystem> Resources { get; } = [];
        public string? User { get; set; }
    }

    public sealed class TimingSettings
    {
        public uint FrameRate { get; set; }
        public uint AsyncLoaderThreads { get; set; }
    }

    public sealed class WindowSettings
    {
        public string? Title { get; set; } = null;
        public bool CursorVisible { get; set; } = true;
        public bool AllowWindowCorner { get; set; }
        public bool AllowTitleBarAutoHide { get; set; }
    }

    public sealed class GraphicsSystemSettings
    {
        public string? PreferredDeviceName { get; set; }
        public uint Width { get; set; } = 640;
        public uint Height { get; set; } = 480;
        public bool Fullscreen { get; set; } = false;
        public bool Borderless { get; set; } = false;
        public bool Vsync { get; set; } = true;
        public bool AllowSoftwareDevice { get; set; }
        public bool AllowExclusiveFullscreen { get; set; }
        public bool AllowModernSwapChain { get; set; } = true;
        public bool AllowDirectComposition { get; set; }
        public bool UseVulkanBackend { get; set; } = false;
    }

    public sealed class AudioSystemSettings
    {
        public string? PreferredEndpointName { get; set; }
        public float SoundEffectVolume { get; set; } = 1.0f;
        public float MusicVolume { get; set; } = 1.0f;
    }

    public DebugSettings Debug { get; } = new();
    public ApplicationSettings Application { get; } = new();
    public LoggingSettings Logging { get; } = new();
    public FileSystemSettings FileSystemConfig { get; } = new();
    public TimingSettings Timing { get; } = new();
    public WindowSettings Window { get; } = new();
    public GraphicsSystemSettings GraphicsSystem { get; } = new();
    public AudioSystemSettings AudioSystem { get; } = new();

    public List<string> Messages { get; } = [];

    private ConfigurationLoader()
    {
        Logging.Debugger.Enable = true;
        Logging.File.Enable = true;
    }

    public static ConfigurationLoader Instance { get; } = new();

    public string GetFormattedMessage() => string.Join('\n', Messages);

    public bool LoadFromFile(string path)
    {
        List<Include> includes = [];
        if (!ConfigurationLoaderContext.Load(this, includes, path))
            return false;

        foreach (var include in includes)
        {
            string resolvedPath;
            try
            {
                resolvedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(include.Path));
            }
            catch
            {
                if (include.Optional)
                    continue;
                Messages.Add($"resolve '{include.Path}' failed");
                return false;
            }

            if (!File.Exists(resolvedPath))
            {
                if (include.Optional)
                    continue;
                Messages.Add($"'{include.Path}' not found");
                return false;
            }

            if (!ConfigurationLoaderContext.Load(this, null, resolvedPath))
                return false;
        }

        if (Application.SingleInstance && !Application.HasUuid)
        {
            Messages.Add($"[{path}] single_instance require uuid string to be set");
            return false;
        }

        return true;
    }

    public static bool Exists(string path)
    {
        try
        {
            return File.Exists(Path.Combine(Directory.GetCurrentDirectory(), path));
        }
        catch
        {
            return false;
        }
    }

    public bool LoadFromCommandLineArguments(string[] args)
    {
        //TODO LoadFromCommandLineArguments

        return true;
    }

    internal sealed class Include
    {
        public string Path { get; set; } = string.Empty;
        public bool Optional { get; set; }
    }
}

internal static class ConfigurationLoaderContext
{
    public static bool Load(ConfigurationLoader loader, List<ConfigurationLoader.Include>? includeOut, string path)
    {
        string jsonText;
        try
        {
            jsonText = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            loader.Messages.Add($"read file '{path}' failed");
            return false;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(jsonText);
        }
        catch (JsonException e)
        {
            loader.Messages.Add($"parse config file '{path}' failed: {e.Message}");
            return false;
        }

        if (root is not JsonObject rootObj)
        {
            loader.Messages.Add($"parse config file '{path}' failed: root is not an object");
            return false;
        }

        return Merge(loader, includeOut, rootObj);
    }

    public static bool Merge(ConfigurationLoader loader, List<ConfigurationLoader.Include>? includeOut, JsonObject root)
    {
        var messages = loader.Messages;

        #region Include
        if (root.TryGetPropertyValue("include", out var includesNode))
        {
            if (!includesNode.RequireArray("/include", messages))
                return false;
            if (includeOut is null)
            {
                messages.Add("[/include] include is not allowed");
                return false;
            }
            var includesArray = includesNode!.AsArray();
            for (int i = 0; i < includesArray.Count; i++)
            {
                var item = includesArray[i];
                var itemPath = $"/include/{i}";
                if (!item.RequireObject(itemPath, messages))
                    return false;
                var itemObj = item!.AsObject();

                var inc = new ConfigurationLoader.Include();
                if (itemObj.TryGetPropertyValue("path", out var p))
                {
                    if (!p.RequireString($"{itemPath}/path", messages))
                        return false;
                    inc.Path = p!.GetValue<string>();
                }
                if (itemObj.TryGetPropertyValue("optional", out var o))
                {
                    if (!o.RequireBoolean($"{itemPath}/optional", messages))
                        return false;
                    inc.Optional = o!.GetValue<bool>();
                }
                includeOut.Add(inc);
            }
        }
        #endregion
        #region Debug
        if (root.TryGetPropertyValue("debug", out var debugNode))
        {
            if (!debugNode.RequireObject("/debug", messages))
                return false;
            var debug = debugNode!.AsObject();
            if (debug.TryGetPropertyValue("track_window_focus", out var t))
            {
                if (!t.RequireBoolean("/debug/track_window_focus", messages)) return false;
                loader.Debug.TrackWindowFocus = t!.GetValue<bool>();
            }
        }
        #endregion
        #region Application
        if (root.TryGetPropertyValue("application", out var appNode))
        {
            if (!appNode.RequireObject("/application", messages))
                return false;
            var app = appNode!.AsObject();
            if (app.TryGetPropertyValue("uuid", out var uuidNode))
            {
                if (!uuidNode.RequireString("/application/uuid", messages))
                    return false;
                var s = uuidNode!.GetValue<string>();
                if (!Guid.TryParse(s, out _))
                {
                    messages.Add($"[/application/uuid] require uuid string, but obtain '{s}'");
                    return false;
                }
                loader.Application.Uuid = s;
            }
            if (app.TryGetPropertyValue("single_instance", out var single))
            {
                if (!single.RequireBoolean("/application/single_instance", messages))
                    return false;
                loader.Application.SingleInstance = single!.GetValue<bool>();
            }
        }
        #endregion
        #region Logging
        if (root.TryGetPropertyValue("logging", out var loggingNode))
        {
            if (!loggingNode.RequireObject("/logging", messages))
                return false;
            var logging = loggingNode!.AsObject();

            bool MergeSink(string key, ConfigurationLoader.LogSink sink, string basePath)
            {
                if (!logging.TryGetPropertyValue(key, out var sinkNode))
                    return true;
                if (!sinkNode.RequireObject(basePath, messages))
                    return false;
                var sinkObj = sinkNode!.AsObject();
                if (sinkObj.TryGetPropertyValue("enable", out var enable))
                {
                    if (!enable.RequireBoolean($"{basePath}/enable", messages))
                        return false;
                    sink.Enable = enable!.GetValue<bool>();
                }
                if (sinkObj.TryGetPropertyValue("threshold", out var threshold))
                {
                    if (!threshold.RequireString($"{basePath}/threshold", messages))
                        return false;
                    var s = threshold!.GetValue<string>();
                    if (!Enum.TryParse<ConfigurationLoader.LogLevel>(s, ignoreCase: true, out var level))
                    {
                        messages.Add($"[{basePath}/threshold] unknown logging level '{s}'");
                        return false;
                    }
                    sink.Threshold = level;
                }
                return true;
            }

            if (!MergeSink("debugger", loader.Logging.Debugger, "/logging/debugger"))
                return false;
            if (!MergeSink("console", loader.Logging.Console, "/logging/console"))
                return false;
            if (!MergeSink("file", loader.Logging.File, "/logging/file"))
                return false;
            if (!MergeSink("rolling_file", loader.Logging.RollingFile, "/logging/rolling_file"))
                return false;

            if (logging.TryGetPropertyValue("file", out var fileNode) && fileNode is JsonObject fileObj
                && fileObj.TryGetPropertyValue("path", out var filePath))
            {
                if (!filePath.RequireString("/logging/file/path", messages))
                    return false;
                loader.Logging.File.Path = filePath!.GetValue<string>();
            }

            if (logging.TryGetPropertyValue("rolling_file", out var rfNode) && rfNode is JsonObject rfObj)
            {
                if (rfObj.TryGetPropertyValue("path", out var rfPath))
                {
                    if (!rfPath.RequireString("/logging/rolling_file/path", messages))
                        return false;
                    loader.Logging.RollingFile.Path = rfPath!.GetValue<string>();
                }
                if (rfObj.TryGetPropertyValue("max_history", out var maxHist))
                {
                    if (!maxHist.RequireUnsignedInteger("/logging/rolling_file/max_history", messages))
                        return false;
                    loader.Logging.RollingFile.MaxHistory = maxHist!.GetValue<uint>();
                }
            }
        }
        #endregion
        #region File System
        if (root.TryGetPropertyValue("file_system", out var fsNode))
        {
            if (!fsNode.RequireObject("/file_system", messages))
                return false;
            var fs = fsNode!.AsObject();
            if (fs.TryGetPropertyValue("resources", out var resNode))
            {
                if (!resNode.RequireArray("/file_system/resources", messages))
                    return false;
                var resArray = resNode!.AsArray();
                for (int i = 0; i < resArray.Count; i++)
                {
                    var item = resArray[i];
                    var itemPath = $"/file_system/resources/{i}";
                    if (!item.RequireObject(itemPath, messages))
                        return false;
                    var itemObj = item!.AsObject();
                    var res = new ConfigurationLoader.ResourceFileSystem();
                    if (itemObj.TryGetPropertyValue("name", out var name))
                    {
                        if (!name.RequireString($"{itemPath}/name", messages))
                            return false;
                        res.Name = name!.GetValue<string>();
                    }
                    if (itemObj.TryGetPropertyValue("path", out var p))
                    {
                        if (!p.RequireString($"{itemPath}/path", messages))
                            return false;
                        res.Path = p!.GetValue<string>();
                    }
                    if (itemObj.TryGetPropertyValue("type", out var typeNode))
                    {
                        if (!typeNode.RequireString($"{itemPath}/type", messages))
                            return false;
                        var s = typeNode!.GetValue<string>();
                        if (s == "directory")
                            res.Type = ConfigurationLoader.ResourceType.Directory;
                        else if (s == "archive")
                            res.Type = ConfigurationLoader.ResourceType.Archive;
                        else
                        {
                            messages.Add($"[{itemPath}/type] unknown resource type '{s}'");
                            return false;
                        }
                    }
                    loader.FileSystemConfig.Resources.Add(res);
                }
            }
            if (fs.TryGetPropertyValue("user", out var user))
            {
                if (!user.RequireString("/file_system/user", messages))
                    return false;
                loader.FileSystemConfig.User = user!.GetValue<string>();
            }
        }
        #endregion
        #region Timing
        if (root.TryGetPropertyValue("timing", out var timingNode))
        {
            if (!timingNode.RequireObject("/timing", messages))
                return false;
            var timing = timingNode!.AsObject();
            if (timing.TryGetPropertyValue("frame_rate", out var fr))
            {
                if (!fr.RequireUnsignedInteger("/timing/frame_rate", messages))
                    return false;
                loader.Timing.FrameRate = fr!.GetValue<uint>();
            }
            if (timing.TryGetPropertyValue("async_loader_threads", out var alt))
            {
                if (!alt.RequireUnsignedInteger("/timing/async_loader_threads", messages))
                    return false;
                loader.Timing.AsyncLoaderThreads = alt!.GetValue<uint>();
            }
        }
        #endregion
        #region Window
        if (root.TryGetPropertyValue("window", out var windowNode))
        {
            if (!windowNode.RequireObject("/window", messages))
                return false;
            var window = windowNode!.AsObject();
            if (window.TryGetPropertyValue("title", out var title))
            {
                if (!title.RequireString("/window/title", messages))
                    return false;
                loader.Window.Title = title!.GetValue<string>();
            }
            if (window.TryGetPropertyValue("cursor_visible", out var cv))
            {
                if (!cv.RequireBoolean("/window/cursor_visible", messages))
                    return false;
                loader.Window.CursorVisible = cv!.GetValue<bool>();
            }
            if (window.TryGetPropertyValue("allow_window_corner", out var awc))
            {
                if (!awc.RequireBoolean("/window/allow_window_corner", messages))
                    return false;
                loader.Window.AllowWindowCorner = awc!.GetValue<bool>();
            }
            if (window.TryGetPropertyValue("allow_title_bar_auto_hide", out var atbah))
            {
                if (!atbah.RequireBoolean("/window/allow_title_bar_auto_hide", messages))
                    return false;
                loader.Window.AllowTitleBarAutoHide = atbah!.GetValue<bool>();
            }
        }
        #endregion
        #region Graphics System
        if (root.TryGetPropertyValue("graphics_system", out var gsNode))
        {
            if (!gsNode.RequireObject("/graphics_system", messages))
                return false;
            var gs = gsNode!.AsObject();
            if (gs.TryGetPropertyValue("preferred_device_name", out var pdn))
            {
                if (!pdn.RequireString("/graphics_system/preferred_device_name", messages))
                    return false;
                loader.GraphicsSystem.PreferredDeviceName = pdn!.GetValue<string>();
            }
            if (gs.TryGetPropertyValue("width", out var width))
            {
                if (!width.RequireUnsignedInteger("/graphics_system/width", messages))
                    return false;
                var v = width!.GetValue<uint>();
                if (v == 0)
                {
                    messages.Add("[/graphics_system/width] width must be greater than zero");
                    return false;
                }
                loader.GraphicsSystem.Width = v;
            }
            if (gs.TryGetPropertyValue("height", out var height))
            {
                if (!height.RequireUnsignedInteger("/graphics_system/height", messages))
                    return false;
                var v = height!.GetValue<uint>();
                if (v == 0)
                {
                    messages.Add("[/graphics_system/height] height must be greater than zero");
                    return false;
                }
                loader.GraphicsSystem.Height = v;
            }
            if (gs.TryGetPropertyValue("fullscreen", out var fsFull))
            {
                if (!fsFull.RequireBoolean("/graphics_system/fullscreen", messages))
                    return false;
                loader.GraphicsSystem.Fullscreen = fsFull!.GetValue<bool>();
            }
            if (gs.TryGetPropertyValue("borderless", out var borderless))
            {
                if (!borderless.RequireBoolean("/graphics_system/borderless", messages))
                    return false;
                loader.GraphicsSystem.Borderless = borderless!.GetValue<bool>();
            }
            if (gs.TryGetPropertyValue("vsync", out var vsync))
            {
                if (!vsync.RequireBoolean("/graphics_system/vsync", messages))
                    return false;
                loader.GraphicsSystem.Vsync = vsync!.GetValue<bool>();
            }
            if (gs.TryGetPropertyValue("allow_software_device", out var asd))
            {
                if (!asd.RequireBoolean("/graphics_system/allow_software_device", messages))
                    return false;
                loader.GraphicsSystem.AllowSoftwareDevice = asd!.GetValue<bool>();
            }
            if (gs.TryGetPropertyValue("allow_exclusive_fullscreen", out var aef))
            {
                if (!aef.RequireBoolean("/graphics_system/allow_exclusive_fullscreen", messages))
                    return false;
                loader.GraphicsSystem.AllowExclusiveFullscreen = aef!.GetValue<bool>();
            }
            if (gs.TryGetPropertyValue("allow_modern_swap_chain", out var amsc))
            {
                if (!amsc.RequireBoolean("/graphics_system/allow_modern_swap_chain", messages))
                    return false;
                loader.GraphicsSystem.AllowModernSwapChain = amsc!.GetValue<bool>();
            }
            if (gs.TryGetPropertyValue("allow_direct_composition", out var adc))
            {
                if (!adc.RequireBoolean("/graphics_system/allow_direct_composition", messages))
                    return false;
                loader.GraphicsSystem.AllowDirectComposition = adc!.GetValue<bool>();
            }
            if (gs.TryGetPropertyValue("use_vulkan_backend", out var uvb))
            {
                if (!uvb.RequireBoolean("/graphics_system/use_vulkan_backend", messages))
                    return false;
                loader.GraphicsSystem.UseVulkanBackend = uvb!.GetValue<bool>();
            }
        }
        #endregion
        #region Audio System
        if (root.TryGetPropertyValue("audio_system", out var asNode))
        {
            if (!asNode.RequireObject("/audio_system", messages))
                return false;
            var audio = asNode!.AsObject();
            if (audio.TryGetPropertyValue("preferred_endpoint_name", out var pen))
            {
                if (!pen.RequireString("/audio_system/preferred_endpoint_name", messages))
                    return false;
                loader.AudioSystem.PreferredEndpointName = pen!.GetValue<string>();
            }
            if (audio.TryGetPropertyValue("sound_effect_volume", out var sev))
            {
                if (!sev.RequireNumber("/audio_system/sound_effect_volume", messages))
                    return false;
                var v = sev!.GetValue<float>();
                if (v < 0.0f || v > 1.0f)
                {
                    messages.Add("[/audio_system/sound_effect_volume] out of range [0.0, 1.0]");
                    return false;
                }
                loader.AudioSystem.SoundEffectVolume = v;
            }
            if (audio.TryGetPropertyValue("music_volume", out var mv))
            {
                if (!mv.RequireNumber("/audio_system/music_volume", messages))
                    return false;
                var v = mv!.GetValue<float>();
                if (v < 0.0f || v > 1.0f)
                {
                    messages.Add("[/audio_system/music_volume] out of range [0.0, 1.0]");
                    return false;
                }
                loader.AudioSystem.MusicVolume = v;
            }
        }
        #endregion
        #region Compatibility with older luastg
        if (root.TryGetPropertyValue("debug_track_window_focus", out var c1))
        {
            if (!c1.RequireBoolean("/debug_track_window_focus", messages))
                return false;
            loader.Debug.TrackWindowFocus = c1!.GetValue<bool>();
        }
        if (root.TryGetPropertyValue("single_application_instance", out var c2))
        {
            if (!c2.RequireBoolean("/single_application_instance", messages))
                return false;
            loader.Application.SingleInstance = c2!.GetValue<bool>();
        }
        if (root.TryGetPropertyValue("application_instance_id", out var c3))
        {
            if (!c3.RequireString("/application_instance_id", messages))
                return false;
            loader.Application.Uuid = c3!.GetValue<string>();
        }
        if (root.TryGetPropertyValue("log_file_enable", out var c4))
        {
            if (!c4.RequireBoolean("/log_file_enable", messages))
                return false;
            loader.Logging.File.Enable = c4!.GetValue<bool>();
        }
        if (root.TryGetPropertyValue("log_file_path", out var c5))
        {
            if (!c5.RequireString("/log_file_path", messages))
                return false;
            loader.Logging.File.Path = c5!.GetValue<string>();
        }
        if (root.TryGetPropertyValue("persistent_log_file_enable", out var c6))
        {
            if (!c6.RequireBoolean("/persistent_log_file_enable", messages))
                return false;
            loader.Logging.RollingFile.Enable = c6!.GetValue<bool>();
        }
        if (root.TryGetPropertyValue("persistent_log_file_directory", out var c7))
        {
            if (!c7.RequireString("/persistent_log_file_directory", messages))
                return false;
            loader.Logging.RollingFile.Path = c7!.GetValue<string>();
        }
        if (root.TryGetPropertyValue("persistent_log_file_max_count", out var c8))
        {
            if (!c8.RequireUnsignedInteger("/persistent_log_file_max_count", messages))
                return false;
            loader.Logging.RollingFile.MaxHistory = c8!.GetValue<uint>();
        }
        if (root.TryGetPropertyValue("engine_cache_directory", out var c9))
        {
            if (!c9.RequireString("/engine_cache_directory", messages))
                return false;
            loader.FileSystemConfig.User = c9!.GetValue<string>();
        }
        #endregion

        return true;
    }
}