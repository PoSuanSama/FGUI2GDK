using FairyGUI;
using Game;

namespace ET.Client
{
    /// <summary>
    /// FairyDemoForm 的 ET Component 状态(原 FairyDemoPresenter 的状态迁入)。
    ///
    /// 状态在 ModelView(Hotfix 程序集不允许声明字段/属性,ET0004);
    /// 行为在 HotfixView 的 <see cref="FairyDemoFormComponentSystem"/>。
    /// 实例由打开链创建为 UIComponent 的子 Entity,关闭时由 Adapter 销毁。
    /// </summary>
    [ChildOf(typeof(UIComponent))]
    public class FairyDemoFormComponent : FairyUIFormComponent,
        IAwake,
        IFairyUIFormOnViewReady,
        IFairyUIFormOnOpen,
        IFairyUIFormOnClose
    {
        public int CheckCount;

        /// <summary>
        /// 视图按钮订阅的委托:OnViewReady 创建并订阅,OnClose 对称移除。
        /// 类型必须是 FairyGUI.EventCallback1(与 onClick.Add/Remove 一致)。
        /// </summary>
        public EventCallback1 OpenInventoryClick;

        public EventCallback1 RefreshClick;

        /// <summary>
        /// 派发自检/冒烟断言计数:由 HotfixView System 递增。
        /// </summary>
        public int OnOpenCount;

        public int OnCloseCount;

        /// <summary>
        /// 示例 Widget 实例(宿主上下文容器持有,关闭时统一回收);
        /// 冒烟测试用它断言 parent destroy 级联:owner 销毁后 Widget View 必须已释放。
        /// </summary>
        public FairyInventoryItemWidget ItemWidget;
    }
}
