using FairyGUI;

namespace ET.Client
{
    /// <summary>
    /// FairyInventoryForm 的 ET Component 状态(原 FairyInventoryPresenter 的状态迁入)。
    /// 行为在 HotfixView 的 FairyInventoryFormComponentSystem。
    /// </summary>
    [ChildOf(typeof(UIComponent))]
    public class FairyInventoryFormComponent : FairyUIFormComponent,
        IAwake,
        IFairyUIFormOnViewReady,
        IFairyUIFormOnOpen,
        IFairyUIFormOnClose,
        IFairyUIFormOnPause,
        IFairyUIFormOnResume,
        IFairyUIFormOnCover,
        IFairyUIFormOnReveal
    {
        public FairyInventoryOpenData OpenData;

        public EventCallback1 AllClick;
        public EventCallback1 EquipmentClick;
        public EventCallback1 ConsumableClick;
        public EventCallback1 QuestClick;
        public EventCallback1 ItemClick;
        public EventCallback1 OpenOverlayClick;
        public EventCallback1 CloseClick;

        /// <summary>
        /// FairyInventoryFlow.DetailCountChanged 的订阅委托:OnViewReady 订阅,OnClose 对称移除。
        /// </summary>
        public System.Action<int> DetailCountChangedHandler;

        public int PauseCount;
        public int ResumeCount;
        public int CoverCount;
        public int RevealCount;
    }
}
