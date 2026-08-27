using FairyGUI;

namespace Game
{
    public interface IFairyUIWidget
    {
        GComponent View { get; }

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
