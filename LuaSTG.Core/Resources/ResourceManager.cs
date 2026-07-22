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
        {
            Logger.luastg.Warning($"Tried create Resource Pool of name '{name}', which is a reserved name.");
            return false;
        }

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
    public T? FindResourceInAllPools<T>(string name) where T : class, IResource
    {
        if (CurrentPool != null)
        {
            var resource = CurrentPool.FindResource<T>(name);
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

            var resource = pool.FindResource<T>(name);
            if (resource != null)
            {
                lock (lockObject)
                    foundResource ??= resource;
                state.Stop();
            }
        });

        return foundResource as T;
    }
}
