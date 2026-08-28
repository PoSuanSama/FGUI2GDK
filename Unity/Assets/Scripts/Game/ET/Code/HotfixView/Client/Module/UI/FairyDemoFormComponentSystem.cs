using FairyGUI;
using Game.FairyGUI.Package1;

namespace ET.Client
{
    /// <summary>
    /// FairyDemoForm 的 HotfixView 行为 System(骨架示例)。
    ///
    /// 静态类 + 纯方法:不声明任何字段/属性,不触发 ET0004。
    /// [EntitySystem] 方法名与 ModelView 的 FairyUIFormXxxSystem 基类约定一致,
    /// ETSystemGenerator 据此生成具体 System 类并由 EntitySystemSingleton 注册;
    /// 非热更层经 FairyUIFormSystemDispatcher 运行时派发,无静态反向引用。
    /// </summary>
    [EntitySystemOf(typeof(FairyDemoFormComponent))]
    [FriendOf(typeof(FairyDemoFormComponent))]
    [FriendOf(typeof(FairyUIFormComponent))]
    public static partial class FairyDemoFormComponentSystem
    {
        [EntitySystem]
        private static void FairyUIFormOnOpen(this FairyDemoFormComponent self)
        {
            ++self.OnOpenCount;
            UIMainView view = self.View as UIMainView;
            if (view == null)
            {
                Log.Error("FairyDemoForm OnOpen: view is not ready.");
                return;
            }

            view.StatusText.text = "FairyGUI 资源包已就绪";
        }

        [EntitySystem]
        private static void FairyUIFormOnClose(this FairyDemoFormComponent self)
        {
            ++self.OnCloseCount;
            UIMainView view = self.View as UIMainView;
            if (view != null)
            {
                view.StatusText.text = string.Empty;
            }
        }
    }
}
