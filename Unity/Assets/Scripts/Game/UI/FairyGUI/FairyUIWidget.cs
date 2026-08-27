using FairyGUI;

namespace Game
{
    public abstract class FairyUIWidget : IFairyUIWidget
    {
        public GComponent View { get; private set; }

        public bool Opened { get; private set; }

        public void SetView(GComponent view)
        {
            View = view;
        }

        public virtual void OnOpen(object userData)
        {
            Opened = true;
            if (View != null)
            {
                View.visible = true;
            }
        }

        public virtual void OnClose(bool isShutdown, object userData)
        {
            Opened = false;
        }

        public virtual void OnPause()
        {
        }

        public virtual void OnResume()
        {
        }

        public virtual void OnCover()
        {
        }

        public virtual void OnReveal()
        {
        }

        public virtual void OnRefocus(object userData)
        {
        }

        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }
    }
}
