using LuaSTG.Core.Debugger;
using LuaSTG.Core.FileSystem;
using LuaSTG.Core.Rendering;
using LuaSTG.Core.Resources.Impl;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.Resources;

public interface IResource
{
    public string Name { get; set; }
}

public enum ResourceType
{
    Texture,
    Sprite,
    Animation,
    Music,
    SoundEffect,
    Particle,
    SpriteFront,
    TrueTypeFont,
    FX,
    Model,
    Video,
}

public sealed class ResourcePool : IDisposable
{
    public string poolName { get; private set; } = string.Empty;

    private Dictionary<string, TextureResource> TexturePool = [];
    private Dictionary<string, ImageResource> SpritePool = [];
    private Dictionary<string, AudioResource> MusicPool = [];
    private Dictionary<string, AudioResource> SoundPool = [];

    public ResourcePool(string name)
    {
        poolName = name;
    }

    public void Dispose() => Clear();

    public void Clear()
    {
        //TODO: Clear unmanaged resources

        TexturePool.Clear();
        SpritePool.Clear();
        MusicPool.Clear();
        SoundPool.Clear();

        Logger.luastg.Information($"Resource pool '{poolName}' cleared");
    }

    public void RemoveResource(ResourceType type, string name)
    {

    }

    public IResource? FindResource(ResourceType type, string name)
    {
        switch (type)
        {
            case ResourceType.Texture:
                if (TexturePool.TryGetValue(name, out var tex))
                    return tex;
                break;
            case ResourceType.Sprite:
                if (SpritePool.TryGetValue(name, out var img))
                    return img;
                break;
            case ResourceType.Music:
                if (MusicPool.TryGetValue(name, out var bgm))
                    return bgm;
                break;
            case ResourceType.SoundEffect:
                if (SoundPool.TryGetValue(name, out var snd))
                    return snd;
                break;
            default:
                return default;
                break;
        }

        return default;
    }

    #region Load

    public bool LoadMusic(string name, string path, double start, double end, bool once_decode)
    {
        if (MusicPool.TryGetValue(name, out var bgm))
        {

            Logger.luastg.Warning($"LoadMusic: Music '{name}' already exists; creation operation cancelled");
            return true;
        }

        if (!FileSystemManager.ReadFile(path, out byte[]? data))
        {
            Logger.luastg.Error($"LoadMusic: Unable to find file '{path}'");
            return false;
        }

        try
        {
            var res = new MusicResource(name, path, data!, start, end, once_decode);
            RenderEngine.Instance.Device.AudioDevice.RegisterResource(res);
            MusicPool.Add(name, res);
        }
        catch (Exception ex)
        {
            Logger.luastg.Error($"LoadMusic: Unable to decode file '{path}'. Wrong format or file corrupted? Reason:\n{ex}");
            return false;
        }

        return true;
    }

    #endregion
}
