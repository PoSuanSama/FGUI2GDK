using FairyGUI;
using GameFramework.Event;

namespace Game
{
    public abstract class FairyEntity : IFairyEntity
    {
        private EventContainer m_Events;
        private ResourceContainer m_Resources;
        private FairyEntityContainer m_Children;

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

        public FairyEntityContainer Children => m_Children;

        public void SetView(GObject view)
        {
            View = view;
        }

        public virtual void OnInit(object userData)
        {
            m_Events = EventContainer.Create(this);
            m_Resources = ResourceContainer.Create(this);
            m_Children = FairyEntityContainer.Create(this);
        }

        public virtual void OnShow(object userData)
        {
            Available = true;
            Visible = true;
        }

        public virtual void OnHide(bool isShutdown, object userData)
        {
            Available = false;
            m_Children?.HideAllEntities(isShutdown, userData);
            Parent = null;
        }

        public virtual void OnRecycle()
        {
            Available = false;
            m_Children?.RecycleAllEntities();
            m_Children?.Dispose();
            m_Children = null;
            m_Events?.UnsubscribeAll(false);
            m_Events?.Clear();
            m_Events = null;
            m_Resources?.UnloadAllAssets(false);
            m_Resources?.Clear();
            m_Resources = null;
            if (View != null)
            {
                View.Dispose();
                View = null;
            }
        }

        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            m_Children?.UpdateAllEntities(elapseSeconds, realElapseSeconds);
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

            if (parent is FairyEntity parentEntity)
            {
                parentEntity.Children?.AddEntity(this);
            }
        }

        public virtual void DetachFromParent()
        {
            Parent = null;
        }

        public void Subscribe(int id, System.EventHandler<GameEventArgs> handler)
        {
            m_Events?.Subscribe(id, handler);
        }

        public void Unsubscribe(int id, System.EventHandler<GameEventArgs> handler)
        {
            m_Events?.Unsubscribe(id, handler);
        }

        public void UnloadAsset(UnityEngine.Object asset)
        {
            m_Resources?.UnloadAsset(asset);
        }
    }
}
