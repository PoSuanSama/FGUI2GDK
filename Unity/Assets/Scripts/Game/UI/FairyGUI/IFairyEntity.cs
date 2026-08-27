using FairyGUI;

namespace Game
{
    public interface IFairyEntity
    {
        GObject View { get; }

        bool Available { get; }

        bool Visible { get; set; }

        void OnInit(object userData);

        void OnShow(object userData);

        void OnHide(bool isShutdown, object userData);

        void OnRecycle();

        void OnUpdate(float elapseSeconds, float realElapseSeconds);

        void AttachTo(IFairyEntity parent);

        void DetachFromParent();
    }
}
