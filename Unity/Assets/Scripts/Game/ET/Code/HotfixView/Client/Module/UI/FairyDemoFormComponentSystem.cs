using System;
using Cysharp.Threading.Tasks;
using FairyGUI;
using Game;
using Game.FairyGUI.Package1;

namespace ET.Client
{
    /// <summary>
    /// FairyDemoForm 的 HotfixView 行为 System(原 FairyDemoPresenter 行为迁入)。
    ///
    /// 静态类 + 纯方法:不声明任何字段/属性,不触发 ET0004;
    /// 状态(FairyDemoFormComponent)在 ModelView,经 dispatcher 运行时派发。
    /// Widget 由宿主上下文容器持有并自动级联,这里只创建/打开;
    /// 按钮订阅用闭包捕获 EntityRef,避免静态 System 持有实例状态。
    /// </summary>
    [EntitySystemOf(typeof(FairyDemoFormComponent))]
    [FriendOf(typeof(FairyDemoFormComponent))]
    [FriendOf(typeof(FairyUIFormComponent))]
    public static partial class FairyDemoFormComponentSystem
    {
        [EntitySystem]
        private static void FairyUIFormOnViewReady(this FairyDemoFormComponent self)
        {
            UIMainView view = self.View as UIMainView;
            if (view == null)
            {
                Log.Error("FairyDemoForm OnViewReady: view is not ready.");
                return;
            }

            self.CheckCount = 0;
            FairyUIWidgetContainer widgetContainer = self.Context.Widgets;
            FairyInventoryItemWidget itemWidget = FairyInventoryItemWidget.Create();
            self.ItemWidget = itemWidget;
            widgetContainer.AddWidget(itemWidget);
            widgetContainer.OpenWidget(itemWidget);

            EntityRef<FairyDemoFormComponent> selfRef = self;
            UIComponent owner = self.GetParent<UIComponent>();
            EntityRef<UIComponent> ownerRef = owner;
            self.OpenInventoryClick = context =>
            {
                FairyDemoFormComponent component = selfRef;
                if (component == null)
                {
                    return;
                }

                OpenInventoryAsync(component, ownerRef).Forget();
            };
            self.RefreshClick = context =>
            {
                FairyDemoFormComponent component = selfRef;
                if (component == null)
                {
                    return;
                }

                ++component.CheckCount;
                UpdateStatus(component, $"ET 生命周期检查通过 {DateTime.Now:HH:mm:ss}");
                Log.Info($"ET FairyGUI refresh interaction handled. Count: {component.CheckCount}.");
            };

            view.OpenInventoryButton.onClick.Add(self.OpenInventoryClick);
            view.RefreshButton.onClick.Add(self.RefreshClick);
            UpdateStatus(self, "FairyGUI 资源包已就绪");
        }

        [EntitySystem]
        private static void FairyUIFormOnOpen(this FairyDemoFormComponent self)
        {
            ++self.OnOpenCount; // 派发计数:骨架自检与冒烟断言沿用。
            Log.Info("ET FairyGUI demo form opened through the Component/System chain.");
        }

        [EntitySystem]
        private static void FairyUIFormOnClose(this FairyDemoFormComponent self)
        {
            ++self.OnCloseCount;
            UIMainView view = self.View as UIMainView;
            if (view != null)
            {
                view.OpenInventoryButton.onClick.Remove(self.OpenInventoryClick);
                view.RefreshButton.onClick.Remove(self.RefreshClick);
                UpdateStatus(self, string.Empty);
            }

            self.OpenInventoryClick = null;
            self.RefreshClick = null;
            self.ItemWidget = null;
        }

        private static async UniTaskVoid OpenInventoryAsync(
            FairyDemoFormComponent self,
            EntityRef<UIComponent> ownerRef)
        {
            try
            {
                UIComponent owner = ownerRef;
                if (owner == null)
                {
                    return;
                }

                await FairyInventoryFlow.OpenInventoryAsync(owner);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }

        private static void UpdateStatus(FairyDemoFormComponent self, string status)
        {
            UIMainView view = self.View as UIMainView;
            if (view == null)
            {
                return;
            }

            view.StatusText.text = status;
            view.CheckCountText.text = self.CheckCount.ToString();
        }
    }
}
