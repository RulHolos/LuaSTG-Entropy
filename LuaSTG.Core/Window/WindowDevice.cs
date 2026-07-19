using LuaSTG.Core.Configuration;
using LuaSTG.Core.Rendering;
using Silk.NET.Windowing;

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
        RenderEngine = new(this);

        Window.Closing += Dispose;

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

    public void RequestExit()
    {
        RenderEngine?.RequestExit = true;
    }
}
