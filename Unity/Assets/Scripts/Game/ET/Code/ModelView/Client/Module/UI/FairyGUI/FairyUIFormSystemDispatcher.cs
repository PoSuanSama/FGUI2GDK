using System;
using System.Collections.Generic;

namespace ET.Client
{
    /// <summary>
    /// 把 FairyGUI 界面生命周期转发给 HotfixView 的 Entity System。
    ///
    /// 与原 UGFSystemSingleton(8b39d6cc 删除前)的派发同构,但不再维护第二套系统注册表:
    /// HotfixView 中 [EntitySystem] 方法生成的 System 类由 EntitySystemSingleton.Awake
    /// 统一注册进 TypeSystems,这里只做运行时查表与异常隔离。
    ///
    /// ModelView 与共享 Game 层都不静态引用 HotfixView,因此不存在反向引用问题。
    /// </summary>
    public static class FairyUIFormSystemDispatcher
    {
        public static void FairyUIFormOnOpen(FairyUIFormComponent formComponent)
        {
            Dispatch<IFairyUIFormOnOpen, IFairyUIFormOnOpenSystem>(
                formComponent, static (system, component) => system.Run(component));
        }

        public static void FairyUIFormOnClose(FairyUIFormComponent formComponent)
        {
            Dispatch<IFairyUIFormOnClose, IFairyUIFormOnCloseSystem>(
                formComponent, static (system, component) => system.Run(component));
        }

        public static void FairyUIFormOnPause(FairyUIFormComponent formComponent)
        {
            Dispatch<IFairyUIFormOnPause, IFairyUIFormOnPauseSystem>(
                formComponent, static (system, component) => system.Run(component));
        }

        public static void FairyUIFormOnResume(FairyUIFormComponent formComponent)
        {
            Dispatch<IFairyUIFormOnResume, IFairyUIFormOnResumeSystem>(
                formComponent, static (system, component) => system.Run(component));
        }

        public static void FairyUIFormOnCover(FairyUIFormComponent formComponent)
        {
            Dispatch<IFairyUIFormOnCover, IFairyUIFormOnCoverSystem>(
                formComponent, static (system, component) => system.Run(component));
        }

        public static void FairyUIFormOnReveal(FairyUIFormComponent formComponent)
        {
            Dispatch<IFairyUIFormOnReveal, IFairyUIFormOnRevealSystem>(
                formComponent, static (system, component) => system.Run(component));
        }

        public static void FairyUIFormOnRefocus(FairyUIFormComponent formComponent)
        {
            Dispatch<IFairyUIFormOnRefocus, IFairyUIFormOnRefocusSystem>(
                formComponent, static (system, component) => system.Run(component));
        }

        public static void FairyUIFormOnUpdate(
            FairyUIFormComponent formComponent,
            float elapseSeconds,
            float realElapseSeconds)
        {
            Dispatch<IFairyUIFormOnUpdate, IFairyUIFormOnUpdateSystem>(
                formComponent,
                static (system, component, elapse, real) => system.Run(component, elapse, real),
                elapseSeconds,
                realElapseSeconds);
        }

        private static void Dispatch<TMarker, TSystem>(
            FairyUIFormComponent formComponent,
            Action<TSystem, FairyUIFormComponent> run)
            where TMarker : class
            where TSystem : class
        {
            DispatchCore<TMarker, TSystem>(
                formComponent,
                (system, component, _, _) => run(system, component),
                0f,
                0f);
        }

        private static void Dispatch<TMarker, TSystem>(
            FairyUIFormComponent formComponent,
            Action<TSystem, FairyUIFormComponent, float, float> run,
            float elapseSeconds,
            float realElapseSeconds)
            where TMarker : class
            where TSystem : class
        {
            DispatchCore<TMarker, TSystem>(formComponent, run, elapseSeconds, realElapseSeconds);
        }

        private static void DispatchCore<TMarker, TSystem>(
            FairyUIFormComponent formComponent,
            Action<TSystem, FairyUIFormComponent, float, float> run,
            float elapseSeconds,
            float realElapseSeconds)
            where TMarker : class
            where TSystem : class
        {
            if (formComponent == null || formComponent.IsDisposed)
            {
                return;
            }

            if (formComponent is not TMarker)
            {
                return;
            }

            TypeSystems typeSystems = EntitySystemSingleton.Instance?.TypeSystems;
            if (typeSystems == null)
            {
                return;
            }

            List<SystemObject> systems = typeSystems.GetSystems(formComponent.GetType(), typeof(TSystem));
            if (systems == null)
            {
                return;
            }

            foreach (SystemObject system in systems)
            {
                if (system is not TSystem typedSystem)
                {
                    continue;
                }

                try
                {
                    run(typedSystem, formComponent, elapseSeconds, realElapseSeconds);
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
                }
            }
        }
    }
}
