using LuaSTG.Core.Configuration;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.Window;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public SpriteRenderer SpriteRenderer;

    public event Action? OnFrame;
    public event Action? OnRender;

    internal bool RequestExit = false;

    public RenderEngine(WindowDevice device)
    {
        Owner = device;
        Initialize();
    }

    public void Initialize()
    {
        _gl = GL.GetApi(Owner.Window);

        SpriteRenderer = new(_gl, Owner.Window.Size.X, Owner.Window.Size.Y);

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
                Render();

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
        _gl?.Dispose();
    }

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
}
