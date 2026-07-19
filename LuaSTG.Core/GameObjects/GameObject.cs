using LuaSTG.Core.LuaBindings;
using LuaSTG.Core.Rendering;
using LuaSTG.Core.Resources;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace LuaSTG.Core.GameObjects;

public sealed class GameObjectLayerComparer : IComparer<GameObject>
{
    public static readonly GameObjectLayerComparer Instance = new();

    public int Compare(GameObject? x, GameObject? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (x is null)
            return -1;
        if (y is null)
            return 1;
        var byLayer = x.Layer.CompareTo(y.Layer);
        return byLayer != 0 ? byLayer : x.UniqueId.CompareTo(y.UniqueId);
    }
}

public struct GameObjectFeatures
{
    public bool IsClass;
    public bool IsRenderClass;
    public bool HasCallbackCreate;
    public bool HasCallbackDestroy;
    public bool HasCallbackUpdate;
    public bool HasCallbackRender;
    public bool HasCallbackTrigger;
    public bool HasCallbackLegacyKill;

    public void Reset() => this = default;
}

public enum GameObjectStatus : byte
{
    Free = 0,
    Active = 1,
    Dead = 2,
    Killed = 4,
}

public sealed class GameObject
{
    public const int MaxId = 0xFFFF;
    public const ulong MaxUniqueId = 0xFFFF_FFFF_FFFFUL;
    public const int UnhandledSetGroup = 1;
    public const int UnhandledSetLayer = 2;

    public static ResourceManager Resources { get; set; }
    public static RenderEngine Renderer { get; set; }

    public static bool ScaleColliderShapeByGlobalImageScale { get; set; }

    //TODO
    //public LuaObjectDebugInfo LuaDebug { get; } = new();

    public string Name = "Unknown";

    private List<IGameObjectCallbacks>? callbacks;

    internal GameObject? UpdateListPrevious;
    internal GameObject? UpdateListNext;
    internal GameObject? DetectListPrevious;
    internal GameObject? DetectListNext;

    internal GameObjectPool? Owner;
    internal int LuaSelfRef = -1;
    internal int LuaClassRef = -1;

    public int Id;
    public ulong UniqueId;
    public long World;

    public double LastX;
    public double LastY;
    public double X;
    public double Y;
    public double Dx { get; private set; }
    public double Dy { get; private set; }

    public double Vx;
    public double Vy;
    public double Ax;
    public double Ay;
    public double MaxVx;
    public double MaxVy;
    public double MaxV;
    public double Ag;

    public long Group { get; internal set; }
    public double A;
    public double B;
    public double ColR { get; private set; }

    public double Layer { get; internal set; }
    public double HScale;
    public double VScale;
    public double Rot;
    public double Omega;
    public long AniTimer { get; private set; }
    public IResource? Res;
    //public IParticle Ps;

    public long Timer;
    public long Pause;

    //TODO
    public NativeColor VertexColor;
    //public BlendMode BlendMode;
    public GameObjectFeatures Features;
    public GameObjectStatus Status;

    public bool Bound;
    public bool Colli;
    public bool Rect;
    public bool Hide;
    public bool Navi;
    public bool ResolveMove;
    public bool IgnoreSuperPause;
    public bool LastXyTouched { get; private set; }

    public GameObject() { }

    public void Reset()
    {
        UpdateListPrevious = UpdateListNext = null;
        DetectListPrevious = DetectListNext = null;

        Status = GameObjectStatus.Free;
        Id = MaxId;
        UniqueId = 0;
        Features.Reset();

        X = Y = 0.0;
        LastX = LastY = 0.0;
        Dx = Dy = 0.0;
        Rot = Omega = 0.0;
        Vx = Vy = 0.0;
        Ax = Ay = 0.0;
        Layer = 0.0;
        HScale = VScale = 1.0;
        MaxV = double.MaxValue * 0.5;
        MaxVx = MaxVy = double.MaxValue;
        Ag = 0.0;

        Colli = Bound = true;
        Hide = Navi = false;

        Group = 0;
        Timer = AniTimer = 0;

        Res = null;
        //Ps = null;

        ResolveMove = false;
        Pause = 0;
        IgnoreSuperPause = false;
        LastXyTouched = false;

        World = 0xFFFF;

        Rect = false;
        A = B = 0.0;
        ColR = 0.0;

        //BlendMode = BlendMode.MulAlpha;
        //VertexColor = Color4B.White();

        Name = "Unknown";
        callbacks?.Clear();
    }

    public void DirtReset()
    {
        Status = GameObjectStatus.Active;

        X = Y = 0.0;
        LastX = LastY = 0.0;
        Dx = Dy = 0.0;
        Rot = Omega = 0.0;
        Vx = Vy = 0.0;
        Ax = Ay = 0.0;
        Layer = 0.0;
        HScale = VScale = 1.0;
        MaxV = double.MaxValue * 0.5;
        MaxVx = MaxVy = double.MaxValue;
        Ag = 0.0;

        Colli = Bound = true;
        Hide = Navi = false;

        Group = 0;
        Timer = AniTimer = 0;

        ReleaseResource();

        ResolveMove = false;
        Pause = 0;
        IgnoreSuperPause = false;
        LastXyTouched = false;

        World = 0xFFFF;

        Rect = false;
        A = B = 0.0;
        ColR = 0.0;

        //BlendMode = BlendMode.MulAlpha;
        //VertexColor = Color4B.White();
    }

    public void UpdateCollisionCircleRadius()
    {
        if (Rect)
        {
            ColR = Math.Sqrt(A * A + B * B); //hypot(a, b)
        }
        else if (A != B)
        {
            ColR = A > B ? A : B; // ellipse
        }
        else
        {
            ColR = A; // circle
        }
    }

    public bool ChangeResource(string resName)
    {
        throw new NotImplementedException("TODO");
    }

    public void ReleaseResource()
    {
        throw new NotImplementedException("TODO");
    }

    public void Update()
    {
        if (Pause > 0)
        {
            Pause -= 1;
            return;
        }
        if (ResolveMove)
        {
            if (LastXyTouched)
            {
                Vx = X - LastX;
                Vy = Y - LastY;
            }
            else
            {
                Vx = 0.0;
                Vy = 0.0;
            }
            IntegrateRotationAndParticles();
            return;
        }
        IntegrateVelocityAndPosition();
        IntegrateRotationAndParticles();
    }

    public void UpdateV2()
    {
        if (Pause > 0)
        {
            Pause -= 1;
            return;
        }
        if (ResolveMove)
        {
            if (LastXyTouched)
            {
                Vx = X - LastX;
                Vy = Y - LastY;
            }
            else
            {
                Vx = 0.0;
                Vy = 0.0;
            }
        }
        else
        {
            IntegrateVelocityAndPosition();
        }

        Rot += Omega;

        if (Navi && LastXyTouched)
        {
            var dx = X - LastX;
            var dy = Y - LastY;
            if (Math.Abs(dx) > double.Epsilon || Math.Abs(dy) > double.Epsilon)
            {
                Rot = Math.Atan2(dy, dx);
            }
        }

        UpdateParticleSystem();
    }

    private void IntegrateVelocityAndPosition()
    {
        Vx += Ax;
        Vy += Ay;
        Vy -= Ag;
        if (MaxV <= double.Epsilon)
        {
            Vx = 0.0;
            Vy = 0.0;
        }
        else
        {
            var speed = Math.Sqrt(Vx * Vx + Vy * Vy);
            if (MaxV < speed && speed > double.Epsilon)
            {
                var scale = MaxV / speed;
                Vx *= scale;
                Vy *= scale;
            }
        }
        Vx = Math.Clamp(Vx, -MaxVx, MaxVx);
        Vy = Math.Clamp(Vy, -MaxVy, MaxVy);
        X += Vx;
        Y += Vy;
    }

    private void IntegrateRotationAndParticles()
    {
        Rot += Omega;
        //UpdateParticleSystem();
    }

    private void UpdateParticleSystem()
    {
        //TODO
    }

    public void UpdateLast()
    {
        RefreshDelta();
        if (Navi && (Math.Abs(Dx) > double.Epsilon || Math.Abs(Dy) > double.Epsilon))
            Rot = Math.Atan2(Dy, Dx);
    }

    public void UpdateLastV2()
    {
        RefreshDelta();
        Timer++;
        AniTimer++;
    }

    private void RefreshDelta()
    {
        if (LastXyTouched)
        {
            Dx = X - LastX;
            Dy = Y - LastY;
        }
        else
        {
            Dx = 0.0;
            Dy = 0.0;
        }
        LastX = X;
        LastY = Y;
        LastXyTouched = true;
    }

    public void UpdateTimer()
    {
        Timer++;
        AniTimer++;
    }

    public void Render()
    {
        //TODO
    }

    #region Callback set

    public bool ContainsCallback(IGameObjectCallbacks c) => callbacks?.Contains(c) ?? false;

    public void AddCallbacks(IGameObjectCallbacks c)
    {
        callbacks ??= new(1);
        if (!callbacks.Contains(c))
            callbacks.Add(c);
    }

    public void RemoveCallbacks(IGameObjectCallbacks c) => callbacks?.Remove(c);

    public void RemoveAllCallbacks() => callbacks?.Clear();

    public void DispatchOnQueueToDestroy(string reason)
    {
        if (callbacks is null) return;
        for (var i = 0; i < callbacks.Count; i++) callbacks[i].OnQueueToDestroy(this, reason);
    }
    public void DispatchOnUpdate()
    {
        if (callbacks is null) return;
        for (var i = 0; i < callbacks.Count; i++) callbacks[i].OnUpdate(this);
    }
    public void DispatchOnLateUpdate()
    {
        if (callbacks is null) return;
        for (var i = 0; i < callbacks.Count; i++) callbacks[i].OnLateUpdate(this);
    }
    public void DispatchOnRender()
    {
        if (callbacks is null) return;
        for (var i = 0; i < callbacks.Count; i++) callbacks[i].OnRender(this);
    }
    public void DispatchOnTrigger(GameObject other)
    {
        if (callbacks is null) return;
        for (var i = 0; i < callbacks.Count; i++) callbacks[i].OnTrigger(this, other);
    }

    #endregion
    #region Misc

    public bool HasRenderResource => Res is not null;
    //public bool HasParticlePool
    public string GetRenderResourceName() => Res?.Name ?? string.Empty;

    public double CalculateSpeed() => Math.Sqrt(Vx * Vx * Vy * Vy);

    public double CalculateSpeedDirection()
    {
        if (Math.Abs(Vx) > double.Epsilon && Math.Abs(Vy) > double.Epsilon)
            return Math.Atan2(Vy, Vx);
        return Rot;
    }

    public void SetSpeed(double speed)
    {
        var current = CalculateSpeed();
        if (current > double.Epsilon)
        {
            var scale = speed / current;
            Vx *= scale;
            Vy *= scale;
        }
        else
        {
            Vx = Math.Cos(Rot) * speed;
            Vy = Math.Sin(Rot) * speed;
        }
    }

    public void SetSpeedDirection(double direction)
    {
        var speed = CalculateSpeed();
        if (speed > double.Epsilon)
        {
            Vx = speed * Math.Cos(direction);
            Vy = speed * Math.Sin(direction);
        }
        else
        {
            Rot = direction;
        }
    }

    public void SetGroup(long newGroup) => Owner?.SetGroup(this, (int)newGroup);
    public void SetLayer(long newLayer) => Owner?.SetLayer(this, (int)newLayer);

    public bool IsInRect(double left, double right, double bottom, double top) =>
        X >= left && X <= right && Y >= bottom && Y <= top;

    public bool IsIntersect(GameObject other) => IsIntersect(this, other);

    /*public void SetResourceRenderState(BlendMode blenc, Color color)
    {

    }*/

    public static bool IsIntersect(GameObject p1, GameObject p2)
    {
        //TODO: Implement collision detection.
        return false;
    }

    #endregion
}
