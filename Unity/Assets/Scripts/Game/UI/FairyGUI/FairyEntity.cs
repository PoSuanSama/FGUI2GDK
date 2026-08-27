using FairyGUI;

namespace Game
{
    public abstract class FairyEntity : IFairyEntity
    {
        public GObject View { get; private set; }

        public bool Available { get; private set; }

        public bool Visible
        {
            get => View != null && View.visible;
            set
            {
                if (View != null)
                {
                    View.visible = value;
                }
            }
        }

        public IFairyEntity Parent { get; private set; }

        public void SetView(GObject view)
        {
            View = view;
        }

        public virtual void OnShow(object userData)
        {
            Available = true;
            Visible = true;
        }

        public virtual void OnHide(bool isShutdown, object userData)
        {
            Available = false;
            Parent = null;
        }

        public virtual void OnRecycle()
        {
            Available = false;
            Parent = null;
            View = null;
        }

        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        public virtual void AttachTo(IFairyEntity parent)
        {
            if (parent == null || View == null || parent.View == null)
            {
                return;
            }

            Parent = parent;
            if (parent.View is GComponent parentComponent)
            {
                parentComponent.AddChild(View);
            }
        }

        public virtual void DetachFromParent()
        {
            Parent = null;
        }
    }
}
