using FairyGUI;

namespace Game
{
    public interface IFairyUIWidget
    {
        GComponent View { get; }

        bool Opened { get; }

        void OnInit(object userData);

        void OnOpen(object userData);

        void OnClose(bool isShutdown, object userData);

        void OnRecycle();

        void OnPause();

        void OnResume();

        void OnCover();

        void OnReveal();

        void OnRefocus(object userData);

        void OnUpdate(float elapseSeconds, float realElapseSeconds);

        void OnDepthChanged(int uiGroupDepth, int depthInUIGroup);
    }
}
