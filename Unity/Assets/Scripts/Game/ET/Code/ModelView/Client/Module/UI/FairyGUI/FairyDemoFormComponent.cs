namespace ET.Client
{
    /// <summary>
    /// FairyDemoForm 的 ET Component 状态(骨架示例)。
    ///
    /// 状态必须在 ModelView:Hotfix 程序集不允许声明字段/属性(ET0004)。
    /// 行为在 HotfixView 的 <see cref="FairyDemoFormComponentSystem"/>。
    /// 完整迁移时,现有 FairyDemoPresenter 的状态字段移入此处,行为移入 System。
    /// </summary>
    public class FairyDemoFormComponent : FairyUIFormComponent,
        IFairyUIFormOnOpen,
        IFairyUIFormOnClose
    {
        /// <summary>
        /// 派发自检计数:由 HotfixView 的 FairyDemoFormComponentSystem 递增。
        /// </summary>
        public int OnOpenCount;

        public int OnCloseCount;
    }
}
