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
    /// ET 打开流程负责创建 Component 并以本适配器作为 Presenter 交给 FairyUIManager;
    /// 界面关闭后 Component 仍由 UIComponent 持有,可复用或随 owner 销毁。
    /// </summary>
    [global::ET.EnableClass]
    public sealed class FairyUIPresenterAdapter : IFairyUIPresenter
    {
        private readonly FairyUIFormComponent m_Component;

        public FairyUIPresenterAdapter(FairyUIFormComponent component)
        {
            m_Component = component ?? throw new ArgumentNullException(nameof(component));
        }

        public FairyUIFormComponent Component => m_Component;

        public void OnViewReady(GComponent view)
        {
            m_Component.View = view;
        }

        public void OnOpen(object userData)
        {
            m_Component.UserData = userData;
            FairyUIFormSystemDispatcher.FairyUIFormOnOpen(m_Component);
        }

        public void OnClose(bool isShutdown, object userData)
        {
            m_Component.UserData = userData;
            m_Component.IsShutdown = isShutdown;
            FairyUIFormSystemDispatcher.FairyUIFormOnClose(m_Component);
            m_Component.FairyForm = null;
            m_Component.View = null;
            m_Component.UserData = null;
            m_Component.IsShutdown = false;
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
