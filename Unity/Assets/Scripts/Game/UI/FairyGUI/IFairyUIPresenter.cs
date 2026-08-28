using FairyGUI;

namespace Game
{
    /// <summary>
    /// FairyGUI presenter contract. Implemented by GameHot/ET view code.
    ///
    /// <see cref="OnViewReady"/> 收到的上下文是窗体级所有者(design.md §8):
    /// Widget/Event/Resource 容器随窗体自动级联与清理,Presenter 不再自行持有容器。
    /// </summary>
    public interface IFairyUIPresenter
    {
        void OnViewReady(FairyUIFormContext context);
        void OnOpen(object userData);
        void OnClose(bool isShutdown, object userData);
        void OnPause();
        void OnResume();
        void OnCover();
        void OnReveal();
        void OnRefocus(object userData);
        void OnUpdate(float elapseSeconds, float realElapseSeconds);
    }
}