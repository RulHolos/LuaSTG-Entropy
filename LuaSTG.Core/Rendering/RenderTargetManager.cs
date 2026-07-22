using LuaSTG.Core.Resources.Impl;
using LuaSTG.Core.Window;
using Miniaudio;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.Rendering;

public class RenderTargetManager : IDisposable
{
    private readonly GL gl;

    private readonly Stack<RenderTarget> stack = [];
    private readonly HashSet<RenderTarget> autoSizeTargets = [];

    //TODO: Replace with dynamic window size on configurationloader
    public uint TargetWidth { get; private set; } = 640;
    public uint TargetHeight { get; private set; } = 480;

    public RenderTarget MainTarget { get; private set; }
    public RenderTarget? ActiveTarget { get; private set; }

    public Vector2D<uint> AutoSize { get; private set; }

    public RenderTargetManager()
    {
        gl = WindowDevice.Instance.RenderEngine.GL;
        AutoSize = new(TargetWidth, TargetHeight);

        MainTarget = new(TargetWidth, TargetHeight);

        WindowDevice.Instance.Window.Resize += (vec) =>
        {
            ResizeAutoSizeRenderTarget((Vector2D<uint>)vec);
        };
    }

    public bool BeginRenderTargetStack()
    {
        stack.Clear();

        return PushRenderTarget(MainTarget);
    }

    public bool EndRenderTargetStack()
    {
        stack.Clear();

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Viewport(0, 0, (uint)WindowDevice.Instance.Window.Size.X, (uint)WindowDevice.Instance.Window.Size.Y);

        return true;
    }

    public bool PushRenderTarget(RenderTarget rt)
    {
        if (rt == null)
            return false;

        if (CheckRenderTargetInUse(rt))
            return false;

        stack.Push(rt);

        rt.Bind();
        gl.Viewport(0, 0, rt.Width, rt.Height);

        return true;
    }

    public bool PopRenderTarget()
    {
        if (stack.Count == 0)
            return false;

        stack.Pop();

        if (stack.Count > 0)
        {
            var current = stack.Peek();
            current.Bind();
            gl.Viewport(0, 0, current.Width, current.Height);
        }
        else
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.Viewport(0, 0, (uint)WindowDevice.Instance.Window.Size.X, (uint)WindowDevice.Instance.Window.Size.Y);
        }

        return true;
    }

    public bool IsRenderTargetStackEmpty() => stack.Count == 0;

    public bool CheckRenderTargetInUse(RenderTarget rt)
        => stack.Contains(rt);

    public Vector2D<uint> GetTopRenderTargetSize()
    {
        if (stack.TryPeek(out var top))
            return new Vector2D<uint>(top.Width, top.Height);
        return new Vector2D<uint>((uint)WindowDevice.Instance.Window.Size.X, (uint)WindowDevice.Instance.Window.Size.Y);
    }

    public void AddAutoSizeRenderTarget(RenderTarget rt)
    {
        if (rt == null)
            return;

        autoSizeTargets.Add(rt);

        rt.Resize(AutoSize.X, AutoSize.Y);
    }

    public void RemoveAutoSizeRenderTarget(RenderTarget rt)
    {
        if (rt != null)
            autoSizeTargets.Remove(rt);
    }

    public Vector2D<uint> GetAutoSizeRenderTargetSize() => AutoSize;

    public bool ResizeAutoSizeRenderTarget(Vector2D<uint> size)
    {
        if (size.X == 0 || size.Y == 0)
            return false;

        AutoSize = size;

        foreach (var rt in autoSizeTargets)
            rt.Resize(size.X, size.Y);

        return true;
    }

    public void BlitBlitMotherfucker(int x, int y, int width, int height)
    {
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, MainTarget.FramebufferHandle);
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

        gl.BlitFramebuffer(
            0, 0, (int)TargetWidth, (int)TargetHeight,
            x, y, x + width, y + height,
            ClearBufferMask.ColorBufferBit,
            BlitFramebufferFilter.Linear
        );

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Dispose()
    {
        MainTarget?.Dispose();
    }
}
