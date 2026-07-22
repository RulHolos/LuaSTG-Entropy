using LuaSTG.Core.Configuration;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.Window;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace LuaSTG.Core.Rendering;

public sealed class RenderEngine : IDisposable
{
    public WindowDevice Owner { get; set; }
    private GL _gl;
    public GL GL => _gl;
    private bool isRunning = false;

    //fixed 60fps = ~16.6667ms = 1/60sec
    private double TargetTime = 1.0 / 60.0;

    private double currentFps = 0.0;
    private int frameCount = 0;
    private double fpsAccumulator = 0.0;

    public Matrix4x4 ProjectionMatrix { get; private set; }
    public Matrix4x4 ViewMatrix { get; private set; } = Matrix4x4.Identity;

    public SpriteRenderer SpriteRenderer;
    public RenderTargetManager RenderTargetManager;

    public event Action? OnFrame;
    public event Action? OnRender;

    internal bool RequestExit = false;

    public RenderEngine(WindowDevice device)
    {
        Owner = device;
    }

    public void Initialize()
    {
        _gl = GL.GetApi(Owner.Window);

        SpriteRenderer = new(_gl, Owner.Window.Size.X, Owner.Window.Size.Y);
        RenderTargetManager = new();

        isRunning = true;
    }

    public void Run()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        double previousTime = stopwatch.Elapsed.TotalSeconds;
        double accumulator = 0.0;

        while (isRunning && !Owner.Window.IsClosing)
        {
            double currentTime = stopwatch.Elapsed.TotalSeconds;
            double elapsedTime = currentTime - previousTime;
            previousTime = currentTime;

            if (elapsedTime > 0.25)
                elapsedTime = 0.25;

            accumulator += elapsedTime;
            fpsAccumulator += elapsedTime;

            while (accumulator >= TargetTime)
            {
                Owner.Window.DoEvents();

                //Framing
                Frame();

                //Rendering
                BeginScene();
                Render();
                EndScene();

                Owner.Window.SwapBuffers();

                accumulator -= TargetTime;
                frameCount++;
            }

            if (fpsAccumulator >= 1.0)
            {
                currentFps = frameCount / fpsAccumulator;
                frameCount = 0;
                fpsAccumulator = 0.0;
            }

            //I hate igpus
            double timeNextTick = previousTime + (TargetTime - accumulator);
            double timeLeft = timeNextTick - stopwatch.Elapsed.TotalSeconds;

            if (timeLeft > 0.002)
                Thread.Sleep(1);
            else if (timeLeft > 0.0005)
                Thread.Sleep(0);

            if (RequestExit)
            {
                isRunning = false;
                break;
            }
        }
    }

    public void Frame()
    {
        OnFrame?.Invoke();
    }

    public void Render()
    {
        OnRender?.Invoke();
    }

    public void Dispose()
    {
        RenderTargetManager.Dispose();
        _gl?.Dispose();
    }

    #region Misc

    public double GetCurrentFPS()
    {
        return currentFps;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="maxFps"></param>
    /// <returns>Returns false if not passing check</returns>
    public bool SetFPS(double maxFps)
    {
        if (maxFps <= 0.0)
            return false;

        TargetTime = 1.0 / maxFps;
        return true;
    }

    public void BeginScene()
    {
        RenderTargetManager.BeginRenderTargetStack();

        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        GL.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
    }

    public void EndScene()
    {
        RenderTargetManager.EndRenderTargetStack();

        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        GL.Clear((uint)ClearBufferMask.ColorBufferBit);

        var (vpX, vpY, vpWidth, vpHeight) =
            CalculateAspectViewport(Owner.Window.Size.X, Owner.Window.Size.Y, (int)RenderTargetManager.TargetWidth, (int)RenderTargetManager.TargetHeight);

        RenderTargetManager.BlitBlitMotherfucker(vpX, vpY, vpWidth, vpHeight);
    }

    public void SetViewport(int x, int y, uint width, uint height)
    {
        GL.Viewport(x, y, width, height);
    }

    public void SetScissorRect(int x, int y, uint width, uint height, bool enable)
    {
        if (enable)
        {
            GL.Enable(EnableCap.ScissorTest);
            GL.Scissor(x, y, width, height);
        }
        else
        {
            GL.Disable(EnableCap.ScissorTest);
        }
    }

    public void SetOrtho(float left, float right, float bottom, float top, float zNear = -1.0f, float zFar = 1.0f)
    {
        ProjectionMatrix = Matrix4x4.CreateOrthographicOffCenter(left, right, bottom, top, zNear, zFar);
        ViewMatrix = Matrix4x4.Identity;

        GL.Disable(EnableCap.DepthTest);
    }

    public void SetPerspective(float fovDeg, float aspect, float zNear, float zFar, Vector3 eye, Vector3 target, Vector3 up)
    {
        float fovRad = fovDeg * (MathF.PI / 180.0f);

        ProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(fovRad, aspect, zNear, zFar);
        ViewMatrix = Matrix4x4.CreateLookAt(eye, target, up);

        GL.Enable(EnableCap.DepthTest);
    }

    private (int x, int y, int width, int height) CalculateAspectViewport(int winWidth, int winHeight, int targetWidth, int targetHeight)
    {
        float targetAspect = (float)targetWidth / targetHeight;
        float windowAspect = (float)winWidth / winHeight;

        if (windowAspect > targetAspect)
        {
            int vpHeight = winHeight;
            int vpWidth = (int)(winHeight * targetAspect);
            int vpX = (winWidth - vpWidth) / 2;

            return (vpX, 0, vpWidth, vpHeight);
        }
        else
        {
            int vpWidth = winWidth;
            int vpHeight = (int)(winWidth / targetAspect);
            int vpY = (winHeight - vpHeight) / 2;

            return (0, vpY, vpWidth, vpHeight);
        }
    }

    #endregion
}
