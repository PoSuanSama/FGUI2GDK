using Cysharp.Threading.Tasks;
using Game;

namespace ET.Client
{
    [Event(SceneType.Main)]
    public class EntryEvent3_InitClient: AEvent<Scene, EntryEvent3>
    {
        protected override async UniTask Run(Scene root, EntryEvent3 args)
        {
            //Test
            root.AddComponent<TestComponent>();
            root.AddComponent<UGFComponent>();
            UIComponent uiComponent = root.AddComponent<UIComponent>();
            EntityRef<UIComponent> uiComponentRef = uiComponent;
            
            GlobalComponent globalComponent = root.AddComponent<GlobalComponent>();
            await FairyGUIBootstrap.InitializeAsync();
            uiComponent = uiComponentRef;
            if (uiComponent == null)
            {
                throw new System.OperationCanceledException(
                    "The ET UI owner was destroyed during FairyGUI initialization.");
            }

            await uiComponent.OpenFairyUIFormAsync(UGFUIFormId.FairyDemoForm, uiComponent);
            root.AddComponent<PlayerComponent>();
            root.AddComponent<CurrentScenesComponent>();
            
            // 根据配置修改掉Main Fiber的SceneType
            SceneType sceneType = EnumHelper.FromString<SceneType>(globalComponent.AppType.ToString());
            root.SceneType = sceneType;
            
            await EventSystem.Instance.PublishAsync(root, new AppStartInitFinish());
        }
    }
}
