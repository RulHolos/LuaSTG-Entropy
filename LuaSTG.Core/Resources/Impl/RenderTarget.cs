using LuaSTG.Core.Window;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.Resources.Impl;

public unsafe class RenderTarget : IResource, IDisposable
{
    public string Name { get; set; }

    private readonly GL gl;

    public uint FramebufferHandle { get; private set; }
    public uint TextureHandle { get; private set; }
    public uint DepthBufferHandle { get; private set; }

    public uint Width { get; private set; }
    public uint Height { get; private set;  }

    public RenderTarget(uint width, uint height, bool hasDepth = true)
    {
        gl = WindowDevice.Instance.RenderEngine.GL;
        Width = width;
        Height = height;

        FramebufferHandle = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferHandle);

        TextureHandle = gl.GenTexture();
        TextureHandle = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, TextureHandle);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, TextureHandle, 0);

        if (hasDepth)
        {
            DepthBufferHandle = gl.GenRenderbuffer();
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, DepthBufferHandle);
            gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)width, (uint)height);
            gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, DepthBufferHandle);
        }

        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
            throw new Exception("Failed to complete Framebuffer initialization.");

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Resize(uint newWidth, uint newHeight)
    {
        if (Width == newWidth && Height == newHeight)
            return;

        Width = newWidth;
        Height = newHeight;

        gl.BindTexture(TextureTarget.Texture2D, TextureHandle);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, Width, Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        gl.BindTexture(TextureTarget.Texture2D, 0);

        if (DepthBufferHandle != 0)
        {
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, DepthBufferHandle);
            gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, Width, Height);
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        }
    }

    public void Bind() => gl.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferHandle);
    public void Unbind() => gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

    public void Dispose()
    {
        if (TextureHandle != 0)
            gl.DeleteTexture(TextureHandle);
        if (DepthBufferHandle != 0)
            gl.DeleteRenderbuffer(DepthBufferHandle);
        if (FramebufferHandle != 0)
            gl.DeleteFramebuffer(FramebufferHandle);
    }
}