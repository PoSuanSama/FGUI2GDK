using FairyGUI;

namespace ET.Client
{
    /// <summary>
    /// FairyInventoryOverlayForm 的 ET Component 状态(原 FairyInventoryOverlayPresenter 的状态迁入)。
    /// 行为在 HotfixView 的 FairyInventoryOverlayFormComponentSystem。
    /// </summary>
    [ChildOf(typeof(UIComponent))]
    public class FairyInventoryOverlayFormComponent : FairyUIFormComponent,
        IAwake,
        IFairyUIFormOnViewReady,
        IFairyUIFormOnOpen,
        IFairyUIFormOnClose
    {
        public FairyInventoryOverlayOpenData OpenData;

        public EventCallback1 CloseClick;
    }
}
