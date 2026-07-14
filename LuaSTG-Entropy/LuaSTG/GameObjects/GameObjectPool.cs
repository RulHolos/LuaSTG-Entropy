using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.LuaSTG.GameObjects;

public class GameObjectPool
{
    public const int LOBJPOOL_SIZE = 32768;

    public static GameObjectPool Instance { get; } = new();

    private GameObjectPool()
    {

    }
}
