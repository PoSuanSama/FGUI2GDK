using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using MemoryPack;
using MongoDB.Bson.Serialization.Attributes;

namespace ET.Client
{
    /// <summary>
    /// 管理 Scene 持有的 FairyGUI 界面所有权。
    /// </summary>
    [ComponentOf]
    public class UIComponent : Entity, IAwake, IDestroy
    {
        [BsonIgnore]
        [MemoryPackIgnore]
        public Dictionary<long, CancellationTokenSource> PendingFairyUIOpens { get; set; }

        [BsonIgnore]
        [MemoryPackIgnore]
        public Dictionary<int, CancellationTokenSource> OwnedFairyUIForms { get; set; }

        [BsonIgnore]
        [MemoryPackIgnore]
        public long NextFairyUIOpenOperationId { get; set; }
    }

    /// <summary>
    /// ModelView 与 HotfixView UIComponent System 之间的行为契约。
    /// </summary>
    public static class UIComponentFairyUIBridge
    {
        [global::ET.StaticField]
        public static Func<UIComponent, int, object, UniTask<FairyUIForm>> Open;

        [global::ET.StaticField]
        public static Func<UIComponent, int, bool> Close;

        [global::ET.StaticField]
        public static Func<UIComponent, int, object, bool> Refocus;

        public static UniTask<FairyUIForm> OpenAsync(UIComponent owner, int uiId, object userData = null)
        {
            Func<UIComponent, int, object, UniTask<FairyUIForm>> open = Open;
            if (open == null)
            {
                throw new InvalidOperationException("ET FairyGUI UIComponent System is not initialized.");
            }

            return open(owner, uiId, userData);
        }

        public static bool CloseBySerialId(UIComponent owner, int serialId)
        {
            Func<UIComponent, int, bool> close = Close;
            if (close == null)
            {
                throw new InvalidOperationException("ET FairyGUI UIComponent System is not initialized.");
            }

            return close(owner, serialId);
        }

        public static bool RefocusBySerialId(UIComponent owner, int serialId, object userData = null)
        {
            Func<UIComponent, int, object, bool> refocus = Refocus;
            if (refocus == null)
            {
                throw new InvalidOperationException("ET FairyGUI UIComponent System is not initialized.");
            }

            return refocus(owner, serialId, userData);
        }
    }
}
