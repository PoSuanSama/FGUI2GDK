using FairyGUI;

namespace ET.Client
{
    /// <summary>
    /// FairyRuntimeInspectorForm 的 ET Component 状态(原 FairyRuntimeInspectorPresenter 的状态迁入)。
    /// 行为在 HotfixView 的 FairyRuntimeInspectorFormComponentSystem。
    /// </summary>
    [ChildOf(typeof(UIComponent))]
    public class FairyRuntimeInspectorFormComponent : FairyUIFormComponent,
        IAwake,
        IFairyUIFormOnViewReady,
        IFairyUIFormOnOpen,
        IFairyUIFormOnClose,
        IFairyUIFormOnUpdate
    {
        public EntityRef<UIComponent> Owner;

        public GTextField InfoText;

        public EventCallback1 CloseClick;
    }
}
