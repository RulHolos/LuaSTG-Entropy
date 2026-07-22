using LuaSTG.Core.Debugger;
using LuaSTG.Core.FileSystem;
using LuaSTG.Core.Rendering;
using LuaSTG.Core.Resources.Impl;
using LuaSTG.Core.Window;
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

    public bool TryFindResource<T>(ResourceType type, string name, out T? resource) where T : class, IResource
    {
        resource = FindResource<T>(name);
        return resource is not null;
    }

    public T? FindResource<T>(string name) where T : class, IResource
    {
        switch (typeof(T))
        {
            case Type t when t == typeof(TextureResource):
                if (TexturePool.TryGetValue(name, out var tex))
                    return tex as T;
                break;
            case Type t when t == typeof(ImageResource):
                if (SpritePool.TryGetValue(name, out var img))
                    return img as T;
                break;
            case Type t when t == typeof(MusicResource):
                if (MusicPool.TryGetValue(name, out var bgm))
                    return bgm as T;
                break;
            case Type t when t == typeof(SoundEffectResource):
                if (SoundPool.TryGetValue(name, out var snd))
                    return snd as T;
                break;
            default:
                return default;
        }

        return default;
    }

    /// <summary>
    /// Use the generic type alternative instead. Unless it's from a lua binding.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="name"></param>
    /// <returns></returns>
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
        }

        return default;
    }

    #region Load

    public bool LoadTexture(string name, string path, bool pixelated = true, bool mipmaps = false)
    {
        if (TexturePool.TryGetValue(name, out var tex))
        {
            Logger.luastg.Warning($"LoadTexture: Texture '{name}' already exists; creation operation cancelled");
            return true;
        }

        if (!FileSystemManager.ReadFile(path, out byte[]? data))
        {
            Logger.luastg.Error($"LoadTexture: Unable to file file '{path}'");
            return false;
        }

        try
        {
            var res = TextureResource.FromMemory(data!, pixelated, mipmaps);
            res.Name = name;
            TexturePool.Add(name, res);
            return true;
        }
        catch (Exception ex)
        {
            Logger.luastg.Error($"LoadTexture: Unable to decode file '{path}'. Wrong format or file corrupted? Reason:\n{ex}");
            return false;
        }
    }

    public bool LoadSprite(string name, string tex_name, double? x, double? y, double? width, double? height, double? a, double? b, bool? rect)
    {
        if (SpritePool.TryGetValue(name, out var _))
        {
            Logger.luastg.Warning($"LoadSprite: Sprite '{name}' already exists; creation operation cancelled");
            return true;
        }

        if (!TexturePool.TryGetValue(tex_name, out var tex))
        {
            Logger.luastg.Error($"LoadSprite: Cannot find texture '{tex_name}'.");
            return false;
        }

        ImageResource img = null;
        if (x is null)
        {
            //Whole texture
            img = ImageResource.FromWholeTexture(tex);
        }
        else
        {
            //"UV" on texture
            //TODO: Allow double on images.
            img = new(tex, (int)x!, (int)y!, (int)width!, (int)height!);
        }

        if (img is null)
        {
            Logger.luastg.Error($"LoadSprite: Failed to create image '{name}' from texture '{tex_name}'.");
            return false;
        }

        img.Name = name;
        SpritePool.Add(name, img);

        return true;
    }

    public bool TryLoadMusic(string name, string path, double start, double end, bool once_decode, out MusicResource? music)
    {
        if (LoadMusic(name, path, start, end, once_decode))
        {
            music = FindResource<MusicResource>(name);
            return true;
        }
        else
        {
            music = null;
            return false;
        }
    }

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
            WindowDevice.Instance.AudioDevice.RegisterResource(res);
            MusicPool.Add(name, res);
            return true;
        }
        catch (Exception ex)
        {
            Logger.luastg.Error($"LoadMusic: Unable to decode file '{path}'. Wrong format or file corrupted? Reason:\n{ex}");
            return false;
        }
    }

    #endregion
    #region LoadResourcesAsync

    public ResourceLoadBatch LoadResourcesAsync(IReadOnlyList<ResourceLoadRequest> requests, int maxPara = -1)
    {
        ResourceLoadBatch batch = new(requests.Count);

        _ = Task.Run(() => RunBatchAsync(requests, batch, maxPara));

        return batch;
    }

    private async Task RunBatchAsync(IReadOnlyList<ResourceLoadRequest> requests, ResourceLoadBatch batch, int maxPara)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxPara > 0 ? maxPara : Environment.ProcessorCount
        };

        await Parallel.ForEachAsync(requests, options, async (request, ct) =>
        {
            try
            {
                await LoadOneAsync(request, batch, ct);
            }
            catch (Exception ex)
            {
                batch.ReportError(request.Name, ex);
            }
            finally
            {
                batch.MarkOneCompleted();
            }
        });
    }

    private async Task LoadOneAsync(ResourceLoadRequest request, ResourceLoadBatch batch, CancellationToken ct)
    {
        switch (request)
        {
            case MusicLoadRequest music:
                {
                    if (MusicPool.ContainsKey(music.Name))
                        return;

                    if (!FileSystemManager.ReadFile(music.Path, out byte[]? data) || data is null)
                        throw new FileNotFoundException($"Unable to find file '{music.Path}'");

                    var resource = await Task.Run(
                        () => new MusicResource(music.Name, music.Path, data, music.Start, music.End, music.OnceDecode),
                        ct);

                    batch.EnqueueFinalizer(() =>
                    {
                        WindowDevice.Instance.AudioDevice.RegisterResource(resource);
                        MusicPool[music.Name] = resource;
                    });
                    break;
                }

            default:
                throw new NotSupportedException($"LoadResourcesAsync: no loader implemented for {request.GetType().Name}");
        }
    }

    private static object DecodeImagePixels(byte[] fileBytes)
    {
        throw new NotImplementedException("TODO");
    }

    #endregion
}
