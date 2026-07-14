using Silk.NET.OpenGL;
using System;
using System.IO;

namespace LuaSTG.Core.Resources.Impl;

public sealed class ImageResource : IResource
{
    public TextureResource Source { get; }

    public string Name { get; set; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    public ImageResource(TextureResource source, int x, int y, int width, int height)
    {
        Source = source;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public static ImageResource FromWholeTexture(TextureResource source)
        => new(source, 0, 0, source.Width, source.Height);

    public float U0 => X / (float)Source.Width;
    public float V0 => Y / (float)Source.Height;
    public float U1 => (X + Width) / (float)Source.Width;
    public float V1 => (Y + Height) / (float)Source.Height;
}
