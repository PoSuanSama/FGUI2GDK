using System;
using FairyGUI;
using Game;

namespace ET.Client
{
    /// <summary>
    /// 把共享层 <see cref="IFairyUIPresenter"/> 生命周期适配到 ET Component/System:
    /// 先把数据写入 <see cref="FairyUIFormComponent"/> 状态,再经
    /// <see cref="FairyUIFormSystemDispatcher"/> 派发到 HotfixView 的 Entity System。
    ///
    /// 与原 AETMonoUGFUIForm -> UGFSystemSingleton 的转发同构(8b39d6cc 删除前)。
    /// ET 打开流程负责创建 per-open Component 并以本适配器作为 Presenter 交给 FairyUIManager;
    /// 正常关闭时销毁 Component,owner 销毁则由 child Destroy System 提前回滚业务清理。
    /// </summary>
    [global::ET.EnableClass]
    public sealed class FairyUIPresenterAdapter : IFairyUIPresenter
    {
        private readonly FairyUIFormComponent m_Component;
#if UNITY_EDITOR
        private readonly Action<FairyUIFormComponent> m_AfterViewReadyForTesting;
#endif

        public FairyUIPresenterAdapter(FairyUIFormComponent component)
        {
            m_Component = component ?? throw new ArgumentNullException(nameof(component));
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor 回归测试专用的单次打开钩子。回调发生在业务 OnViewReady 完成后、
        /// FairyUIManager 向 GF 请求 serial 前,且只随当前 Adapter 实例存活。
        /// </summary>
        public FairyUIPresenterAdapter(
            FairyUIFormComponent component,
            Action<FairyUIFormComponent> afterViewReadyForTesting)
            : this(component)
        {
            m_AfterViewReadyForTesting = afterViewReadyForTesting
                ?? throw new ArgumentNullException(nameof(afterViewReadyForTesting));
        }
#endif

        public FairyUIFormComponent Component => m_Component;

        public void OnViewReady(FairyUIFormContext context)
        {
            m_Component.Context = context;
            m_Component.View = context?.View;
            FairyUIFormSystemDispatcher.FairyUIFormOnViewReady(m_Component);
#if UNITY_EDITOR
            m_AfterViewReadyForTesting?.Invoke(m_Component);
#endif
        }

        public void OnOpen(object userData)
        {
            m_Component.FairyForm = m_Component.Context?.Form;
            m_Component.UserData = userData;
            FairyUIFormSystemDispatcher.FairyUIFormOnOpen(m_Component);
        }

        public void OnClose(bool isShutdown, object userData)
        {
            m_Component.UserData = userData;
            m_Component.IsShutdown = isShutdown;
            try
            {
                FairyUIFormSystemDispatcher.FairyUIFormOnClose(m_Component);
            }
            finally
            {
                // 宿主随后清理上下文(Widget/事件/资源);这里先摘除 Component 引用。
                m_Component.FairyForm = null;
                m_Component.Context = null;
                m_Component.View = null;
                m_Component.UserData = null;
                m_Component.IsShutdown = false;
                // Component 是 per-open 实例:关闭后销毁,由 UIComponent owner 的 Destroy 级联兜底。
                m_Component.Dispose();
            }
        }

        public void OnPause()
        {
            FairyUIFormSystemDispatcher.FairyUIFormOnPause(m_Component);
        }

        public void OnResume()
        {
            FairyUIFormSystemDispatcher.FairyUIFormOnResume(m_Component);
        }

        public void OnCover()
        {
            FairyUIFormSystemDispatcher.FairyUIFormOnCover(m_Component);
        }

        public void OnReveal()
        {
            FairyUIFormSystemDispatcher.FairyUIFormOnReveal(m_Component);
        }

        public void OnRefocus(object userData)
        {
            m_Component.UserData = userData;
            FairyUIFormSystemDispatcher.FairyUIFormOnRefocus(m_Component);
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            FairyUIFormSystemDispatcher.FairyUIFormOnUpdate(m_Component, elapseSeconds, realElapseSeconds);
        }
    }
}
