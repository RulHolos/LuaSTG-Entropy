using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.GameObjects;

public sealed class GameObjectPool
{
    public const int Capacity = 32768;
    public const int GroupCount = 16;

    public struct FrameStatistics
    {
        public ulong ObjectAlloc;
        public ulong ObjectFree;
        public ulong ObjectAlive;
        public ulong ObjectColliCheck;
        public ulong ObjectColliCallback;
    }

    public struct IntersectionDetectionGroupPair
    {
        public uint Group1;
        public uint Group2;
    }

    private readonly FixedObjectPool<GameObject> objectPool = new(Capacity);
    private ulong uid;

    private readonly IntrusiveLinkedList<GameObject> updateList;
    private readonly SortedSet<GameObject> renderList = new(GameObjectLayerComparer.Instance);
    private readonly IntrusiveLinkedList<GameObject>[] detectLists;
    private readonly List<IGameObjectManagerCallbacks> callbacks = [];

    private GameObject? lockObjectA;
    private GameObject? lockObjectB;

    private double boundLeft = -100.0;
    private double boundRight = 100.0;
    private double boundTop = 100.0;
    private double boundBottom = -100.0;

    private bool isRendering;
    private bool isDetectingIntersect;

    private readonly FrameStatistics[] statistics = new FrameStatistics[2];
    private int statisticsIndex;

    private struct IntersectionDetectionResult
    {
        public ulong Uid1;
        public ulong Uid2;
        public GameObject Object1;
        public GameObject Object2;
    }

    public GameObjectPool()
    {
        updateList = new IntrusiveLinkedList<GameObject>(
            o => o.UpdateListPrevious, (o, v) => o.UpdateListPrevious = v,
            o => o.UpdateListNext, (o, v) => o.UpdateListNext = v
        );

        detectLists = new IntrusiveLinkedList<GameObject>[GroupCount];
        for (var i = 0; i < GroupCount; i++)
        {
            detectLists[i] = new IntrusiveLinkedList<GameObject>(
                o => o.DetectListPrevious, (o, v) => o.DetectListPrevious = v,
                o => o.DetectListNext, (o, v) => o.DetectListNext = v
            );
        }

        ResetGameObjectLists();

        superPause = 0;
        nextSuperPause = 0;
    }

    private void ResetGameObjectLists()
    {
        updateList.Clear();
        renderList.Clear();
        foreach (var list in detectLists)
            list.Clear();
    }

    private bool ObjectBoundCheck(GameObject obj)
    {
        if (!obj.Bound)
            return true;
        return obj.IsInRect(boundLeft, boundRight, boundBottom, boundTop);
    }

    #region Callbacks

    public void AddCallbacks(IGameObjectManagerCallbacks callbacks)
    {
        if (!this.callbacks.Contains(callbacks)) this.callbacks.Add(callbacks);
    }
    public void RemoveCallbacks(IGameObjectManagerCallbacks callbacks) => this.callbacks.Remove(callbacks);

    public void DispatchOnCreate(GameObject obj) { foreach (var c in callbacks) c.OnCreate(obj); }
    public void DispatchOnDestroy(GameObject obj) { foreach (var c in callbacks) c.OnDestroy(obj); }
    public void DispatchOnBeforeBatchDestroy() { foreach (var c in callbacks) c.OnBeforeBatchDestroy(); }
    public void DispatchOnAfterBatchDestroy() { foreach (var c in callbacks) c.OnAfterBatchDestroy(); }
    public void DispatchOnBeforeBatchUpdate() { foreach (var c in callbacks) c.OnBeforeBatchUpdate(); }
    public void DispatchOnAfterBatchUpdate() { foreach (var c in callbacks) c.OnAfterBatchUpdate(); }
    public void DispatchOnBeforeBatchRender() { foreach (var c in callbacks) c.OnBeforeBatchRender(); }
    public void DispatchOnAfterBatchRender() { foreach (var c in callbacks) c.OnAfterBatchRender(); }
    public void DispatchOnBeforeBatchOutOfWorldBoundCheck() { foreach (var c in callbacks) c.OnBeforeBatchOutOfWorldBoundCheck(); }
    public void DispatchOnAfterBatchOutOfWorldBoundCheck() { foreach (var c in callbacks) c.OnAfterBatchOutOfWorldBoundCheck(); }
    public void DispatchOnBeforeBatchIntersectDetect() { foreach (var c in callbacks) c.OnBeforeBatchIntersectDetect(); }
    public void DispatchOnAfterBatchIntersectDetect() { foreach (var c in callbacks) c.OnAfterBatchIntersectDetect(); }

    #endregion

    #region Debug Stats

    public void DebugNextFrame()
    {
        statisticsIndex = (statisticsIndex + 1) % statistics.Length;
        statistics[statisticsIndex] = new() { ObjectAlive = (ulong)objectPool.Count };
    }

    public FrameStatistics DebugGetFrameStatistics()
    {
        var n = statistics.Length;
        var i = (statisticsIndex + n - 1) % n;
        return statistics[i];
    }

    #endregion

    #region Life cycle

    public void ResetPool()
    {
        DispatchOnBeforeBatchDestroy();
        for (var p = updateList.First; p is not null;)
        {
            p = FreeWithCallbacks(p);
        }
        DispatchOnAfterBatchDestroy();

        ResetGameObjectLists();
        objectPool.Clear();

        world = 0x00000001;
        activeWorldMask = 0xFFFFFFFF;

        lockObjectA = null;
        lockObjectB = null;
        superPause = 0;
        nextSuperPause = 0;
    }

    #endregion

    #region Movements

    public void UpdateMovementsLegacy()
    {
        DispatchOnBeforeBatchUpdate();
        var world = GetWorldFlag();

        var superPauseTime = UpdateSuperPause();
        for (var p = updateList.First; p is not null; p = p.UpdateListNext)
        {
            if (superPauseTime > 0 && !p.IgnoreSuperPause)
                continue;
            if (p.Features.HasCallbackUpdate)
                p.DispatchOnUpdate();
            p.Update();
        }
        DispatchOnAfterBatchUpdate();
    }

    public void UpdateMovements()
    {
        DispatchOnBeforeBatchUpdate();
        var world = GetWorldFlag();

        var superPauseTime = GetSuperPauseTime();
        for (var p = updateList.First; p is not null; p = p.UpdateListNext)
        {
            if (superPauseTime > 0 && !p.IgnoreSuperPause)
                continue;
            if (!CheckWorlds(p.World, world))
                continue;
            if (p.Features.HasCallbackUpdate)
                p.DispatchOnUpdate();
        }
        DispatchOnAfterBatchUpdate();

        for (var p = updateList.First; p is not null; p = p.UpdateListNext)
        {
            if (superPauseTime > 0 && !p.IgnoreSuperPause)
                continue;
            p.UpdateV2();
        }
    }

    public void PartialUpdateMovements(IReadOnlyList<int> groups, bool hasWorld, long world)
    {
        DispatchOnBeforeBatchUpdate();

        var filterGroup = groups.Count > 0;
        uint groupMask = 0;
        if (filterGroup)
        {
            foreach (var g in groups)
                if (g >= 0 && g < GroupCount) groupMask |= 1u << g;
        }

        var worldFlag = hasWorld ? world : GetWorldFlag();
        var superPauseTime = GetSuperPauseTime();

        for (var p = updateList.First; p is not null; p = p.UpdateListNext)
        {
            if (superPauseTime > 0 && !p.IgnoreSuperPause)
                continue;
            if (!CheckWorlds(p.World, worldFlag))
                continue;
            if (filterGroup)
            {
                var g = p.Group;
                if (g < 0 || g >= GroupCount || (groupMask & (1u << (int)g)) == 0)
                    continue;
            }
            if (p.Features.HasCallbackUpdate)
                p.DispatchOnUpdate();
        }

        DispatchOnAfterBatchUpdate();

        for (var p = updateList.First; p is not null; p = p.UpdateListNext)
        {
            if (superPauseTime > 0 && !p.IgnoreSuperPause)
                continue;
            if (!CheckWorlds(p.World, worldFlag))
                continue;
            if (filterGroup)
            {
                var g = p.Group;
                if (g < 0 || g >= GroupCount || (groupMask & (1u << (int)g)) == 0)
                    continue;
            }
            p.UpdateV2();
        }
    }

    #endregion

    #region Rendering

    public void Render()
    {
        var world = GetWorldFlag();

        isRendering = true;
        DispatchOnBeforeBatchRender();

        foreach (var p in renderList)
        {
            if (p.Hide)
                continue;
            if (!CheckWorlds(p.World, world))
                continue;
            if (p.Features.HasCallbackRender)
                p.DispatchOnRender();
            else
                p.Render();
        }

        DispatchOnAfterBatchRender();
        isRendering = false;
    }

    public void PartialRender(IReadOnlyList<int> groups, IReadOnlyList<double> layerRanges, bool hasWorld, long world)
    {
        var filterGroup = groups.Count > 0;
        uint groupMask = 0;
        if (filterGroup)
        {
            foreach (var g in groups)
                if (g >= 0 && g < GroupCount) groupMask |= 1u << g;
        }

        var filterLayers = layerRanges.Count > 0;
        var worldFlag = hasWorld ? world : GetWorldFlag();

        isRendering = true;
        DispatchOnBeforeBatchRender();

        foreach (var p in renderList)
        {
            if (p.Hide)
                continue;
            if (!CheckWorlds(p.World, worldFlag))
                continue;
            if (filterGroup)
            {
                var g = p.Group;
                if (g < 0 || g >= GroupCount || (groupMask & (1u << (int)g)) == 0)
                    continue;
            }
            if (filterLayers)
            {
                var layer = p.Layer;
                var inRange = false;
                for (var i = 0; i + 1 < layerRanges.Count; i += 2)
                {
                    if (layer >= layerRanges[i] && layer <= layerRanges[i + 1])
                    {
                        inRange = true;
                        break;
                    }
                }
                if (!inRange)
                    continue;
            }
            if (p.Features.HasCallbackRender)
                p.DispatchOnRender();
            else
                p.Render();
        }

        DispatchOnAfterBatchRender();
        isRendering = false;
    }

    #endregion

    #region Bounds

    public void SetBound(double l, double r, double b, double t)
    {
        if (r < l || t < b)
            throw new ArgumentException("Bound must satisfy r >= l and t >= b.");
        boundLeft = l;
        boundRight = r;
        boundBottom = b;
        boundTop = t;
    }

    public (double left, double right, double top, double bottom) GetBound() =>
        (boundLeft, boundRight, boundTop, boundBottom);

    public bool IsPointInBound(double x, double y) =>
        x >= boundLeft && x <= boundRight && y >= boundBottom && y <= boundTop;

    private const string QueueToDestroyReasonOutOfWorldBound = "luastg:leave_world_border";

    public void DetectOutOfWorldBoundLegacy()
    {
        DispatchOnBeforeBatchOutOfWorldBoundCheck();
        var world = GetWorldFlag();
        for (var p = updateList.First; p is not null; p = p.UpdateListNext)
        {
            if (!CheckWorlds(p.World, world))
                continue;
            if (ObjectBoundCheck(p))
                continue;
            p.Status = GameObjectStatus.Dead;
            if (p.Features.HasCallbackDestroy)
                p.DispatchOnQueueToDestroy(QueueToDestroyReasonOutOfWorldBound);
        }
        DispatchOnAfterBatchOutOfWorldBoundCheck();
    }

    public void DetectOutOfWorldBound()
    {
        DispatchOnBeforeBatchOutOfWorldBoundCheck();

        var cache = new List<GameObject>();
        var world = GetWorldFlag();
        for (var p = updateList.First; p is not null; p = p.UpdateListNext)
        {
            if (!CheckWorlds(p.World, world))
                continue;
            if (ObjectBoundCheck(p))
                continue;
            p.Status = GameObjectStatus.Dead;
            if (p.Features.HasCallbackDestroy)
                cache.Add(p);
        }

        foreach (var obj in cache)
        {
            obj.DispatchOnQueueToDestroy(QueueToDestroyReasonOutOfWorldBound);
        }

        DispatchOnAfterBatchOutOfWorldBoundCheck();
    }

    #endregion

    #region Framing ending

    public void UpdateXY()
    {
        var superPauseTime = GetSuperPauseTime();
        for (var p = updateList.First; p is not null; p = p.UpdateListNext)
        {
            if (superPauseTime > 0 && !p.IgnoreSuperPause)
                continue;
            p.UpdateLast();
        }
    }

    public void UpdateNextLegacy()
    {
        DispatchOnBeforeBatchDestroy();
        var world = GetWorldFlag();
        var superPauseTime = GetSuperPauseTime();
        for (var p = updateList.First; p is not null;)
        {
            if (superPauseTime > 0 && !p.IgnoreSuperPause)
            {
                p = p.UpdateListNext;
                continue;
            }
            if (!CheckWorlds(p.World, world))
            {
                p = p.UpdateListNext;
                continue;
            }
            if (p.Status != GameObjectStatus.Active)
            {
                p = FreeWithCallbacks(p);
                continue;
            }
            p.UpdateTimer();
            p = p.UpdateListNext;
        }
        DispatchOnAfterBatchDestroy();
    }

    public void UpdateNext()
    {
        DispatchOnBeforeBatchDestroy();
        var world = GetWorldFlag();
        var superPauseTime = UpdateSuperPause();
        for (var p = updateList.First; p is not null;)
        {
            if (superPauseTime > 0 && !p.IgnoreSuperPause)
            {
                p = p.UpdateListNext;
                continue;
            }
            if (!CheckWorlds(p.World, world))
            {
                p = p.UpdateListNext;
                continue;
            }
            if (p.Status != GameObjectStatus.Active)
            {
                p = FreeWithCallbacks(p);
                continue;
            }
            p.UpdateLastV2();
            p = p.UpdateListNext;
        }
        DispatchOnAfterBatchDestroy();
    }

    #endregion

    #region Collision Detect

    public void DetectIntersectionLegacy(uint group1, uint group2)
    {
        isDetectingIntersect = true;
        DispatchOnBeforeBatchIntersectDetect();
        ref var debugData = ref statistics[statisticsIndex];

        for (var ptrA = detectLists[group1].First; ptrA is not null;)
        {
            var pA = ptrA;
            ptrA = ptrA.DetectListNext;
            for (var ptrB = detectLists[group2].First; ptrB is not null;)
            {
                var pB = ptrB;
                ptrB = ptrB.DetectListNext;

                if (!pA.Features.HasCallbackTrigger)
                    continue;
                if (!CheckWorlds(pA.World, pB.World))
                    continue;
                debugData.ObjectColliCheck += 1;
                if (!GameObject.IsIntersect(pA, pB))
                    continue;

                debugData.ObjectColliCallback += 1;

                lockObjectA = pA;
                lockObjectB = pB;
                pA.DispatchOnTrigger(pB);
                lockObjectA = null;
                lockObjectB = null;
            }
        }

        DispatchOnAfterBatchIntersectDetect();
        isDetectingIntersect = false;
    }

    public void DetectIntersection(IReadOnlyList<IntersectionDetectionGroupPair> groupPairs)
    {
        isDetectingIntersect = true;
        DispatchOnBeforeBatchIntersectDetect();
        ref var debugData = ref statistics[statisticsIndex];

        var cache = new List<IntersectionDetectionResult>();
        foreach (var pair in groupPairs)
        {
            var group1 = pair.Group1;
            var group2 = pair.Group2;
            for (var object1 = detectLists[group1].First; object1 is not null; object1 = object1.DetectListNext)
            {
                for (var object2 = detectLists[group2].First; object2 is not null; object2 = object2.DetectListNext)
                {
                    if (!object1.Features.HasCallbackTrigger)
                        continue;
                    if (!CheckWorlds(object1.World, object2.World))
                        continue;
                    debugData.ObjectColliCheck += 1;
                    if (!GameObject.IsIntersect(object1, object2))
                        continue;

                    cache.Add(new IntersectionDetectionResult
                    {
                        Uid1 = object1.UniqueId,
                        Uid2 = object2.UniqueId,
                        Object1 = object1,
                        Object2 = object2,
                    });
                }
            }
        }

        foreach (var result in cache)
        {
            if (result.Object1.UniqueId != result.Uid1 || result.Object2.UniqueId != result.Uid2)
            {
                continue;
            }
            debugData.ObjectColliCallback += 1;
            lockObjectA = result.Object1;
            lockObjectB = result.Object2;
            result.Object1.DispatchOnTrigger(result.Object2);
            lockObjectA = null;
            lockObjectB = null;
        }

        DispatchOnAfterBatchIntersectDetect();
        isDetectingIntersect = false;
    }

    #endregion

    #region Groups and Layers

    public void DirtResetObject(GameObject p)
    {
        updateList.Remove(p);
        renderList.Remove(p);

        if (p == lockObjectA || p == lockObjectB)
            throw new InvalidOperationException("Cannot reset an object that is currently locked by intersection detection.");
        detectLists[p.Group].Remove(p);

        p.UniqueId = uid % GameObject.MaxUniqueId;
        uid++;

        updateList.Add(p);
        renderList.Add(p);
        detectLists[p.Group].Add(p);
    }

    public void SetGroup(GameObject obj, int group)
    {
        if (obj == lockObjectA || obj == lockObjectB)
            throw new InvalidOperationException("Cannot change the group of an object currently locked by intersection detection.");
        detectLists[obj.Group].Remove(obj);
        obj.Group = group;
        detectLists[obj.Group].Add(obj);
    }

    public void SetLayer(GameObject obj, double layer)
    {
        if (isRendering)
            throw new InvalidOperationException("Cannot change layer while rendering.");
        renderList.Remove(obj);
        obj.Layer = layer;
        renderList.Add(obj);
    }

    #endregion

    #region Alloc

    public int GetObjectCount() => objectPool.Count;
    public GameObject GetPooledObject(int i) => objectPool.Object(i);

    public GameObject? Allocate() => AllocateWithCallbacks(null);

    public GameObject? AllocateWithCallbacks(IGameObjectCallbacks? callbacks)
    {
        if (!objectPool.TryAlloc(out var id))
            return null;

        var p = objectPool.Object(id);
        p.Reset();
        p.Owner = this;
        p.World = GetWorldFlag();
        p.Status = GameObjectStatus.Active;
        p.Id = id;
        p.UniqueId = uid % GameObject.MaxUniqueId;
        uid++;

        updateList.Add(p);
        renderList.Add(p);
        detectLists[p.Group].Add(p);

        statistics[statisticsIndex].ObjectAlloc += 1;

        if (callbacks is not null)
            p.AddCallbacks(callbacks);
        DispatchOnCreate(p);
        return p;
    }

    public GameObject? FreeWithCallbacks(GameObject obj)
    {
        DispatchOnDestroy(obj);
        obj.RemoveAllCallbacks();
        obj.ReleaseResource();
        statistics[statisticsIndex].ObjectFree += 1;

        var next = updateList.Remove(obj);
        renderList.Remove(obj);
        detectLists[obj.Group].Remove(obj);

        obj.Status = GameObjectStatus.Free;
        obj.Owner = null;
        objectPool.Free(obj.Id);

        return next;
    }

    public bool QueueToFree(GameObject obj, bool legacyKillMode = false)
    {
        if (obj.Status != GameObjectStatus.Active)
            return false;

        obj.Status = legacyKillMode ? GameObjectStatus.Killed : GameObjectStatus.Dead;

        var hasCallbackDestroy = !legacyKillMode && obj.Features.HasCallbackDestroy;
        var hasCallbackLegacyKill = legacyKillMode && obj.Features.HasCallbackLegacyKill;

        return hasCallbackDestroy || hasCallbackLegacyKill;
    }

    public bool IsLockedByDetectIntersection(GameObject obj) => obj == lockObjectA || obj == lockObjectB;
    public bool IsRendering => isRendering;
    public bool IsDetectingIntersect => isDetectingIntersect;

    public GameObject? GetUpdateListFirst() => updateList.First;
    public GameObject? GetUpdateListNext(int id) => GetUpdateListNext(objectPool.Object(id));
    public GameObject? GetUpdateListNext(GameObject? obj) => obj?.UpdateListNext;

    public GameObject? GetDetectListFirst(int group) => detectLists[group].First;
    public GameObject? GetDetectListNext(int group, int id) => GetDetectListNext(group, objectPool.Object(id));
    public GameObject? GetDetectListNext(int group, GameObject? obj)
    {
        if (obj is null)
            return null;
        if (obj.Group != group)
            return null;
        return obj.DetectListNext;
    }

    private long world = 0x00000001;
    private long activeWorldMask = -1;

    public void SetWorldFlag(long worldMask) => world = worldMask;
    public long GetWorldFlag() => world;
    public void SetActiveWorlds(long activeMask) => activeWorldMask = activeMask;
    public long GetActiveWorlds() => activeWorldMask;

    public static bool CheckWorlds(long a, long b, long activeMask = -1)
        => (a & b & activeMask) != 0;

    public bool IsSameWorld(long otherMask) => CheckWorlds(world, otherMask, activeWorldMask);

    #endregion

    #region Super Pause

    private long superPause;
    private long nextSuperPause;

    public long GetSuperPauseTime() => superPause;
    public long GetNextFrameSuperPauseTime() => nextSuperPause;
    public void SetNextFrameSuperPauseTime(long time) => nextSuperPause = time;

    public long UpdateSuperPause()
    {
        superPause = nextSuperPause;
        if (nextSuperPause > 0)
            nextSuperPause -= 1;
        return superPause;
    }

    #endregion

    #region Debug overlay

    //TODO

    #endregion
}
