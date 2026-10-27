using FairyGUI;
using Game;
using MemoryPack;
using MongoDB.Bson.Serialization.Attributes;

namespace ET.Client
{
    /// <summary>
    /// ET 侧 FairyGUI 界面的 Entity 状态基类。
    ///
    /// 与原 UGFUIForm(8b39d6cc 删除前)同构:状态保存在非热更的 ModelView,
    /// 行为由 HotfixView 的 static System 提供,经 <see cref="FairyUIFormSystemDispatcher"/>
    /// 运行时派发。Hotfix 程序集不允许声明字段/属性(ET0004),因此状态必须留在此处。
    ///
    /// 直接实现 <see cref="IFairyUIPresenter"/>,不再经 Adapter 中转:
    /// 生命周期回调先把数据写入本组件,再派发到 HotfixView 的 Entity System。
    /// 实例作为 UIComponent 的子 Entity 持有,per-open 创建,关闭后自行销毁。
    /// </summary>
    [EnableMethod]
    public class FairyUIFormComponent : Entity, IDestroy, IFairyUIPresenter
    {
        [BsonIgnore]
        [MemoryPackIgnore]
        public FairyUIForm FairyForm { get; set; }

        /// <summary>
        /// 窗体级上下文所有者(Widget/事件/资源容器),由 FairyUIPresenterAdapter 注入;
        /// 宿主关闭时先清 Component 引用再清上下文。
        /// </summary>
        [BsonIgnore]
        [MemoryPackIgnore]
        public FairyUIFormContext Context { get; set; }

        [BsonIgnore]
        [MemoryPackIgnore]
        public GComponent View { get; set; }

        [BsonIgnore]
        [MemoryPackIgnore]
        public object UserData { get; set; }

        [BsonIgnore]
        [MemoryPackIgnore]
        public bool IsShutdown { get; set; }

        /// <summary>
        /// 当前窗体的 GF serial ID;尚未打开时为 0。
        /// </summary>
        public int SerialId => FairyForm?.SerialId ?? 0;

        public bool Available => FairyForm != null && !IsDisposed;

        public void OnViewReady(FairyUIFormContext context)
        {
            Context = context;
            View = context?.View;
            FairyUIFormSystemDispatcher.FairyUIFormOnViewReady(this);
        }

        public void OnOpen(object userData)
        {
            FairyForm = Context?.Form;
            UserData = userData;
            FairyUIFormSystemDispatcher.FairyUIFormOnOpen(this);
        }

        public void OnClose(bool isShutdown, object userData)
        {
            UserData = userData;
            IsShutdown = isShutdown;
            try
            {
                FairyUIFormSystemDispatcher.FairyUIFormOnClose(this);
            }
            finally
            {
                // 宿主随后清理上下文(Widget/事件/资源);这里先摘除组件引用。
                FairyForm = null;
                Context = null;
                View = null;
                UserData = null;
                IsShutdown = false;
                // per-open 实例:关闭后销毁,由 UIComponent owner 的 Destroy 级联兜底。
                Dispose();
            }
        }

        public void OnPause()
        {
            FairyUIFormSystemDispatcher.FairyUIFormOnPause(this);
        }

        public void OnResume()
        {
            FairyUIFormSystemDispatcher.FairyUIFormOnResume(this);
        }

        public void OnCover()
        {
            FairyUIFormSystemDispatcher.FairyUIFormOnCover(this);
        }

        public void OnReveal()
        {
            FairyUIFormSystemDispatcher.FairyUIFormOnReveal(this);
        }

        public void OnRefocus(object userData)
        {
            UserData = userData;
            FairyUIFormSystemDispatcher.FairyUIFormOnRefocus(this);
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            FairyUIFormSystemDispatcher.FairyUIFormOnUpdate(this, elapseSeconds, realElapseSeconds);
        }
    }
}
