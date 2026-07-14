using LuaSTG.Core.Rendering;
using Silk.NET.OpenGL;
using System;
using System.IO;

namespace LuaSTG.Core.Resources.Impl;

public sealed class TextureResource : IResource, IDisposable
{
    public string Name { get; set; }
    public uint Handle { get; }
    public int Width { get; }
    public int Height { get; }

    private readonly GL _gl;
    private bool disposed;
    private ImageResource? fullImage;

    public ImageResource FullImage => fullImage ??= ImageResource.FromWholeTexture(this);

    private TextureResource(GL gl, uint handle, int width, int height)
    {
        _gl = gl;
        Handle = handle;
        Width = width;
        Height = height;
    }

    public static TextureResource FromPixels(ReadOnlySpan<byte> rgba, int width, int height, bool pixelated = true)
    {
        return FromPixels(RenderEngine.Instance.GL, rgba, width, height, pixelated);
    }

    public static unsafe TextureResource FromPixels(GL gl, ReadOnlySpan<byte> rgba, int width, int height, bool pixelated = true, bool mipmaps = false)
    {
        if (rgba.Length != width * height * 4)
            throw new ArgumentException($"Expected {width * height * 4} bytes for a {width}x{height} RGBA image, got {rgba.Length}.");

        uint handle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, handle);

        GLEnum minFilter = (pixelated, mipmaps) switch
        {
            (true, false) => GLEnum.Nearest,
            (true, true) => GLEnum.NearestMipmapNearest,
            (false, false) => GLEnum.Linear,
            (false, true) => GLEnum.LinearMipmapLinear,
        };
        GLEnum magFilter = pixelated ? GLEnum.Nearest : GLEnum.Linear;

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)minFilter);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        fixed (byte* ptr = rgba)
        {
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        if (mipmaps)
            gl.GenerateMipmap(TextureTarget.Texture2D);

        gl.BindTexture(TextureTarget.Texture2D, 0);
        return new TextureResource(gl, handle, width, height);
    }

    public static TextureResource FromMemory(byte[] encodedData, bool pixelated = true, bool mipmaps = false)
    {
        var image = StbImageSharp.ImageResult.FromMemory(encodedData, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        return FromPixels(RenderEngine.Instance.GL, image.Data, image.Width, image.Height, pixelated, mipmaps);
    }

    public void Bind(uint unit = 0)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + (int)unit);
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        _gl.DeleteTexture(Handle);
        disposed = true;
    }
}
