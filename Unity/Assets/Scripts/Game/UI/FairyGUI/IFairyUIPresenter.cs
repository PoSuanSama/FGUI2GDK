using FairyGUI;

namespace Game
{
    /// <summary>
    /// FairyGUI presenter contract. Implemented by GameHot/ET view code.
    /// </summary>
    public interface IFairyUIPresenter
    {
        void OnViewReady(GComponent view);
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