using System;

namespace ET.Client
{
    /// <summary>
    /// FairyGUI 界面生命周期契约与 System 基类。
    ///
    /// 与原 GDK 的 UGFUIForm 生命周期(8b39d6cc 删除前)同构:
    /// - ModelView 定义 marker 接口与抽象 System 基类(状态与契约);
    /// - HotfixView 用 [EntitySystem] 静态方法实现行为,源生成器按方法名生成具体 System;
    /// - 生成类携带 [EntitySystem],由 EntitySystemSingleton.Awake 自动注册进 TypeSystems;
    /// - FairyUIFormSystemDispatcher 在运行时查表派发,非热更层无需静态引用 HotfixView。
    ///
    /// 生命周期数据( userData / isShutdown )先写入 FairyUIFormComponent 状态,
    /// 再由适配器派发;System 方法只接收 Component,不携带额外数据参数,
    /// 以避免接口 Run 参数与生成类泛型参数不一致(原 UGFUIForm 同样不在派发中传递数据)。
    /// </summary>

    public interface IFairyUIFormOnViewReady
    {
    }

    public interface IFairyUIFormOnViewReadySystem : ISystemType
    {
        void Run(FairyUIFormComponent o);
    }

    [EntitySystem]
    public abstract class FairyUIFormOnViewReadySystem<T> : SystemObject, IFairyUIFormOnViewReadySystem
        where T : FairyUIFormComponent, IFairyUIFormOnViewReady
    {
        Type ISystemType.Type()
        {
            return typeof(T);
        }

        Type ISystemType.SystemType()
        {
            return typeof(IFairyUIFormOnViewReadySystem);
        }

        int ISystemType.GetInstanceQueueIndex()
        {
            return InstanceQueueIndex.None;
        }

        void IFairyUIFormOnViewReadySystem.Run(FairyUIFormComponent o)
        {
            this.FairyUIFormOnViewReady((T)o);
        }

        protected abstract void FairyUIFormOnViewReady(T self);
    }

    public interface IFairyUIFormOnOpen
    {
    }

    public interface IFairyUIFormOnOpenSystem : ISystemType
    {
        void Run(FairyUIFormComponent o);
    }

    [EntitySystem]
    public abstract class FairyUIFormOnOpenSystem<T> : SystemObject, IFairyUIFormOnOpenSystem
        where T : FairyUIFormComponent, IFairyUIFormOnOpen
    {
        Type ISystemType.Type()
        {
            return typeof(T);
        }

        Type ISystemType.SystemType()
        {
            return typeof(IFairyUIFormOnOpenSystem);
        }

        int ISystemType.GetInstanceQueueIndex()
        {
            return InstanceQueueIndex.None;
        }

        void IFairyUIFormOnOpenSystem.Run(FairyUIFormComponent o)
        {
            this.FairyUIFormOnOpen((T)o);
        }

        protected abstract void FairyUIFormOnOpen(T self);
    }

    public interface IFairyUIFormOnClose
    {
    }

    public interface IFairyUIFormOnCloseSystem : ISystemType
    {
        void Run(FairyUIFormComponent o);
    }

    [EntitySystem]
    public abstract class FairyUIFormOnCloseSystem<T> : SystemObject, IFairyUIFormOnCloseSystem
        where T : FairyUIFormComponent, IFairyUIFormOnClose
    {
        Type ISystemType.Type()
        {
            return typeof(T);
        }

        Type ISystemType.SystemType()
        {
            return typeof(IFairyUIFormOnCloseSystem);
        }

        int ISystemType.GetInstanceQueueIndex()
        {
            return InstanceQueueIndex.None;
        }

        void IFairyUIFormOnCloseSystem.Run(FairyUIFormComponent o)
        {
            this.FairyUIFormOnClose((T)o);
        }

        protected abstract void FairyUIFormOnClose(T self);
    }

    public interface IFairyUIFormOnPause
    {
    }

    public interface IFairyUIFormOnPauseSystem : ISystemType
    {
        void Run(FairyUIFormComponent o);
    }

    [EntitySystem]
    public abstract class FairyUIFormOnPauseSystem<T> : SystemObject, IFairyUIFormOnPauseSystem
        where T : FairyUIFormComponent, IFairyUIFormOnPause
    {
        Type ISystemType.Type()
        {
            return typeof(T);
        }

        Type ISystemType.SystemType()
        {
            return typeof(IFairyUIFormOnPauseSystem);
        }

        int ISystemType.GetInstanceQueueIndex()
        {
            return InstanceQueueIndex.None;
        }

        void IFairyUIFormOnPauseSystem.Run(FairyUIFormComponent o)
        {
            this.FairyUIFormOnPause((T)o);
        }

        protected abstract void FairyUIFormOnPause(T self);
    }

    public interface IFairyUIFormOnResume
    {
    }

    public interface IFairyUIFormOnResumeSystem : ISystemType
    {
        void Run(FairyUIFormComponent o);
    }

    [EntitySystem]
    public abstract class FairyUIFormOnResumeSystem<T> : SystemObject, IFairyUIFormOnResumeSystem
        where T : FairyUIFormComponent, IFairyUIFormOnResume
    {
        Type ISystemType.Type()
        {
            return typeof(T);
        }

        Type ISystemType.SystemType()
        {
            return typeof(IFairyUIFormOnResumeSystem);
        }

        int ISystemType.GetInstanceQueueIndex()
        {
            return InstanceQueueIndex.None;
        }

        void IFairyUIFormOnResumeSystem.Run(FairyUIFormComponent o)
        {
            this.FairyUIFormOnResume((T)o);
        }

        protected abstract void FairyUIFormOnResume(T self);
    }

    public interface IFairyUIFormOnCover
    {
    }

    public interface IFairyUIFormOnCoverSystem : ISystemType
    {
        void Run(FairyUIFormComponent o);
    }

    [EntitySystem]
    public abstract class FairyUIFormOnCoverSystem<T> : SystemObject, IFairyUIFormOnCoverSystem
        where T : FairyUIFormComponent, IFairyUIFormOnCover
    {
        Type ISystemType.Type()
        {
            return typeof(T);
        }

        Type ISystemType.SystemType()
        {
            return typeof(IFairyUIFormOnCoverSystem);
        }

        int ISystemType.GetInstanceQueueIndex()
        {
            return InstanceQueueIndex.None;
        }

        void IFairyUIFormOnCoverSystem.Run(FairyUIFormComponent o)
        {
            this.FairyUIFormOnCover((T)o);
        }

        protected abstract void FairyUIFormOnCover(T self);
    }

    public interface IFairyUIFormOnReveal
    {
    }

    public interface IFairyUIFormOnRevealSystem : ISystemType
    {
        void Run(FairyUIFormComponent o);
    }

    [EntitySystem]
    public abstract class FairyUIFormOnRevealSystem<T> : SystemObject, IFairyUIFormOnRevealSystem
        where T : FairyUIFormComponent, IFairyUIFormOnReveal
    {
        Type ISystemType.Type()
        {
            return typeof(T);
        }

        Type ISystemType.SystemType()
        {
            return typeof(IFairyUIFormOnRevealSystem);
        }

        int ISystemType.GetInstanceQueueIndex()
        {
            return InstanceQueueIndex.None;
        }

        void IFairyUIFormOnRevealSystem.Run(FairyUIFormComponent o)
        {
            this.FairyUIFormOnReveal((T)o);
        }

        protected abstract void FairyUIFormOnReveal(T self);
    }

    public interface IFairyUIFormOnRefocus
    {
    }

    public interface IFairyUIFormOnRefocusSystem : ISystemType
    {
        void Run(FairyUIFormComponent o);
    }

    [EntitySystem]
    public abstract class FairyUIFormOnRefocusSystem<T> : SystemObject, IFairyUIFormOnRefocusSystem
        where T : FairyUIFormComponent, IFairyUIFormOnRefocus
    {
        Type ISystemType.Type()
        {
            return typeof(T);
        }

        Type ISystemType.SystemType()
        {
            return typeof(IFairyUIFormOnRefocusSystem);
        }

        int ISystemType.GetInstanceQueueIndex()
        {
            return InstanceQueueIndex.None;
        }

        void IFairyUIFormOnRefocusSystem.Run(FairyUIFormComponent o)
        {
            this.FairyUIFormOnRefocus((T)o);
        }

        protected abstract void FairyUIFormOnRefocus(T self);
    }

    public interface IFairyUIFormOnUpdate
    {
    }

    public interface IFairyUIFormOnUpdateSystem : ISystemType
    {
        void Run(FairyUIFormComponent o, float elapseSeconds, float realElapseSeconds);
    }

    /// <summary>
    /// ETSystemGenerator 的 EntitySystem 模板把所有参数(含 self)塞进基类泛型实参,
    /// 因此 OnUpdate 基类必须带两个幽灵泛型参数 P1/P2 匹配模板生成的
    /// FairyUIFormOnUpdateSystem&lt;T, float, float&gt;。抽象方法用具体 float。
    /// </summary>
    [EntitySystem]
    public abstract class FairyUIFormOnUpdateSystem<T, P1, P2> : SystemObject, IFairyUIFormOnUpdateSystem
        where T : FairyUIFormComponent, IFairyUIFormOnUpdate
    {
        Type ISystemType.Type()
        {
            return typeof(T);
        }

        Type ISystemType.SystemType()
        {
            return typeof(IFairyUIFormOnUpdateSystem);
        }

        int ISystemType.GetInstanceQueueIndex()
        {
            return InstanceQueueIndex.None;
        }

        void IFairyUIFormOnUpdateSystem.Run(FairyUIFormComponent o, float elapseSeconds, float realElapseSeconds)
        {
            this.FairyUIFormOnUpdate((T)o, elapseSeconds, realElapseSeconds);
        }

        protected abstract void FairyUIFormOnUpdate(T self, float elapseSeconds, float realElapseSeconds);
    }
}
