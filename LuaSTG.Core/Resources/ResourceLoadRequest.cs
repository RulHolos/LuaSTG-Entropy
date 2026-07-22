using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.Resources;

public abstract record ResourceLoadRequest(ResourceType Type, string Name);

public sealed record MusicLoadRequest(
    string Name,
    string Path,
    double Start,
    double End,
    bool OnceDecode
) : ResourceLoadRequest(ResourceType.Music, Name);