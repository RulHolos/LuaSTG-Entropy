using LuaSTG.Core.Configuration;
using Silk.NET.Windowing;

namespace LuaSTG.Core.Rendering;

public sealed class WindowDevice
{
    private WindowOptions options;
    public IWindow Window { get; private set; }
    public AudioDevice AudioDevice { get; private set; }

    public WindowDevice()
    {
        var cfgloader = ConfigurationLoader.Instance;
        //Bypass vulkan for now.
        /*var useVulkan = cfgloader.GraphicsSystem.UseVulkanBackend;
        options = useVulkan ? WindowOptions.DefaultVulkan : WindowOptions.Default;*/

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
        Window.Closing += () => AudioDevice.Shutdown();

        //if (ConfigurationLoader.Instance.GraphicsSystem.UseVulkanBackend && Window.VkSurface is null)
        //    throw new NotSupportedException("Vulkan is not supported on the current platform.");

        return true;
    }
}
