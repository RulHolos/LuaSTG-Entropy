using LuaSTG.Core.Configuration;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.Rendering;
using Silk.NET.Core;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using System.Reflection;
using System.Text;

namespace LuaSTG.Core.Window;

public sealed class WindowDevice : IDisposable
{
    private WindowOptions options;
    public IWindow Window { get => field; private set { field = value; } }

    public static WindowDevice Instance { get; private set; }

    #region Subsystems

    public AudioDevice AudioDevice { get; private set; }
    public InputDevice InputDevice { get; private set; }
    public RenderEngine RenderEngine { get; private set; }

    #endregion

    public WindowDevice()
    {
        Instance = this;

        var cfgloader = ConfigurationLoader.Instance;

        options = WindowOptions.Default;

        options.Title = string.IsNullOrEmpty(cfgloader.Window.Title) ? LUASTG_INFO : cfgloader.Window.Title;
        options.API = new(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));
        options.Size = new((int)cfgloader.GraphicsSystem.Width, (int)cfgloader.GraphicsSystem.Height);

        options.VSync = cfgloader.GraphicsSystem.Vsync;
        options.ShouldSwapAutomatically = false;
    }

    public bool Initialize()
    {
        //Window
        Window = Silk.NET.Windowing.Window.Create(options);
        Window.Initialize();

        AudioDevice = new();
        InputDevice = new(Window);
        {
            RenderEngine = new(this);
            RenderEngine.Initialize();
        }

        //SetIcon();

        return true;
    }

    public void Run()
    {
        RenderEngine?.Run();
    }

    public void Dispose()
    {
        AudioDevice?.Dispose();
        //InputDevice?.Dispose();
        RenderEngine?.Dispose();
        Window?.Dispose();
    }

    public void SetIcon()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var i in assembly.GetManifestResourceNames())
            Console.WriteLine(i);
        var file = assembly.GetManifestResourceNames().FirstOrDefault(x => x == "LuaSTG.Core.app.ico");

        using var stream = assembly.GetManifestResourceStream(file);
        if (stream == null)
        {
            Logger.core.Error("Icon 'LuaSTG.Core.app.ico' not found in embedded resources.");
            return;
        }

        byte[] rawBytes = new byte[stream.Length];
        stream.ReadExactly(rawBytes, 0, rawBytes.Length);

        //TODO: Size mismatch fix.
        RawImage icon = new(128, 128, rawBytes);
        ReadOnlySpan<RawImage> icons = [icon];
        Window.SetWindowIcon(icons);
    }

    public void RequestExit()
    {
        RenderEngine?.RequestExit = true;
    }

    public void SetTitle(string title)
    {
        Window.Title = title;
    }

    public void SetResolution(int width, int height)
    {
        Window.Size = new(width, height);
    }

    public void SetVSync(bool enable)
    {
        Window.VSync = enable;
    }

    public void SetSplash(bool enable)
    {
        InputDevice.ShowCursor(enable);
    }

    private static string WrapMsgBoxText(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var words = text.Split(' ');
        var sb = new StringBuilder();
        int currentLineLength = 0;

        foreach (var word in words)
        {
            if (currentLineLength + word.Length + 1 > maxChars)
            {
                sb.AppendLine();
                currentLineLength = 0;
            }

            if (currentLineLength > 0)
            {
                sb.Append(' ');
                currentLineLength++;
            }

            sb.Append(word);
            currentLineLength += word.Length;
        }

        return sb.ToString();
    }

    public static unsafe int MessageBox(string title, string text, MessageBoxFlags flags)
    {
        var sdl = Sdl.GetApi();
        //text = WrapMsgBoxText(text, 80);

        int result = sdl.ShowSimpleMessageBox((uint)flags, title, text, null);
        return result;
    }
}
