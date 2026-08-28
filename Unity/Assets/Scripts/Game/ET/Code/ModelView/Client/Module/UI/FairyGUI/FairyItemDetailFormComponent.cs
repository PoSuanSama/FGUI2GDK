using System.Collections.Generic;
using FairyGUI;

namespace ET.Client
{
    /// <summary>
    /// FairyItemDetailForm 的 ET Component 状态(原 FairyItemDetailPresenter 的状态迁入)。
    /// 三实例界面:每个详情窗一个 Component 实例,由 AddChild 生成唯一 Id。
    /// 行为在 HotfixView 的 FairyItemDetailFormComponentSystem。
    /// </summary>
    [ChildOf(typeof(UIComponent))]
    public class FairyItemDetailFormComponent : FairyUIFormComponent,
        IAwake,
        IFairyUIFormOnViewReady,
        IFairyUIFormOnOpen,
        IFairyUIFormOnClose,
        IFairyUIFormOnPause,
        IFairyUIFormOnResume,
        IFairyUIFormOnCover,
        IFairyUIFormOnReveal,
        IFairyUIFormOnRefocus
    {
        public FairyItemDetailOpenData OpenData;

        /// <summary>
        /// 随窗框拖动的部件集合(除 WindowFrame 外)。
        /// </summary>
        public List<GObject> WindowParts;

        public float LastDragX;
        public float LastDragY;

        public EventCallback1 DragStart;
        public EventCallback1 DragMove;
        public EventCallback1 DragEnd;
        public EventCallback1 WindowClick;
        public EventCallback1 OpenOverlayClick;
        public EventCallback1 CloseClick;

        public int PauseCount;
        public int ResumeCount;
        public int CoverCount;
        public int RevealCount;
        public int RefocusCount;
    }
}
