using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.GameObjects;

public interface IGameObjectManagerCallbacks
{
    string GetCallbacksName();

    void OnCreate(GameObject obj);
    void OnDestroy(GameObject obj);

    void OnBeforeBatchDestroy();
    void OnAfterBatchDestroy();

    void OnBeforeBatchUpdate();
    void OnAfterBatchUpdate();

    void OnBeforeBatchRender();
    void OnAfterBatchRender();

    void OnBeforeBatchOutOfWorldBoundCheck();
    void OnAfterBatchOutOfWorldBoundCheck();

    void OnBeforeBatchIntersectDetect();
    void OnAfterBatchIntersectDetect();
}
