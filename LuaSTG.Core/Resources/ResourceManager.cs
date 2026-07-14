using LuaSTG.Core.Debugger;
using LuaSTG.Core.Resources.Impl;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.Resources;

public class ResourceManager
{
    private HashSet<ResourcePool> Pools { get; set; } = [];
    private ResourcePool? CurrentPool;

    public static ResourceManager Instance { get; } = new();

    public ResourceManager()
    {
        Pools.Add(new("global"));
        Pools.Add(new("stage"));

        CurrentPool = Pools.First();
    }

    public bool CreatePool(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        //Reserved names
        if (name.Equals("global", StringComparison.OrdinalIgnoreCase) || name.Equals("stage", StringComparison.OrdinalIgnoreCase))
            return false;

        //Already exists, skip
        if (Pools.Any(x => x.poolName == name))
            return false;

        Pools.Add(new(name));
        Logger.luastg.Information($"Resource Pool '{name}' created");

        return true;
    }

    public ResourcePool? GetPool(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        return Pools.FirstOrDefault(x => x.poolName == name);
    }

    public ResourcePool? GetCurrentResourcePool() => CurrentPool;

    public IEnumerable<ResourcePool> EnumPools() => Pools;

    /// <summary>
    /// Tries to get a resource in all pools. Will prioritize the current active pool. Will then search through all pools in parallel if not found.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    /// <returns></returns>
    public IResource? FindResourceInAllPools(ResourceType type, string name)
    {
        if (CurrentPool != null)
        {
            var resource = CurrentPool.FindResource(type, name);
            if (resource != null)
                return resource;
        }

        IResource? foundResource = null;
        object lockObject = new();

        Parallel.ForEach(Pools, (pool, state) =>
        {
            if (foundResource != null)
            {
                state.Stop();
                return;
            }

            var resource = pool.FindResource(type, name);
            if (resource != null)
            {
                lock (lockObject)
                    foundResource ??= resource;
                state.Stop();
            }
        });

        return foundResource;
    }

    public TextureResource? FindTexture(string name) => (TextureResource?)FindResourceInAllPools(ResourceType.Texture, name);
    public ImageResource? FindSprite(string name) => (ImageResource?)FindResourceInAllPools(ResourceType.Sprite, name);
    public AudioResource? FindMusic(string name) => (AudioResource?)FindResourceInAllPools(ResourceType.Music, name);
    public SoundEffectResource? FindSound(string name) => (SoundEffectResource?)FindResourceInAllPools(ResourceType.SoundEffect, name);
}
