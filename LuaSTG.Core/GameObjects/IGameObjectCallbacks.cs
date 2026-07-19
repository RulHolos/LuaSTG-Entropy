using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.GameObjects;

public interface IGameObjectCallbacks
{
    string GetCallbacksName(GameObject self);
    void OnQueueToDestroy(GameObject self, string reason);
    void OnUpdate(GameObject self);
    void OnLateUpdate(GameObject self);
    void OnRender(GameObject self);
    void OnTrigger(GameObject self, GameObject other);
}
