using System;
using System.Collections.Generic;

namespace ET.Client
{
    /// <summary>
    /// UI ID -> Component 工厂的映射注册表(ModelView,字段允许)。
    ///
    /// ET 打开链:UIComponentSystem.OpenFairyUIFormAsync 查本表,命中则以工厂创建
    /// Component 子 Entity 并包装成 FairyUIPresenterAdapter 作为 Presenter;
    /// 未命中则回退到 FairyUIPresenterRegistry 的类 Presenter(过渡期库存界面仍走旧路径)。
    ///
    /// 工厂必须用泛型 AddChild&lt;T&gt; 创建子实体:ET0001 分析器无法解析
    /// 非泛型 AddChild(Type 变量) 的 ChildOf 约束,泛型调用是框架认可的创建路径。
    ///
    /// 登记点:FairyGUIBootstrap.InitializeAsync。全部界面迁移完成后,
    /// 反射扫描的类 Presenter 注册表随之移除。
    /// </summary>
    public static class FairyUIFormComponentRegistry
    {
        [global::ET.StaticField]
        private static readonly Dictionary<int, Func<UIComponent, FairyUIFormComponent>> s_Factories =
            new Dictionary<int, Func<UIComponent, FairyUIFormComponent>>();

        public static void Register(int uiId, Func<UIComponent, FairyUIFormComponent> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (s_Factories.TryGetValue(uiId, out Func<UIComponent, FairyUIFormComponent> existing))
            {
                // 同一注册点(同一 Method)重复登记视为幂等:启动竞态下 bootstrap 首次
                // 调用可能在 Tables 检查处失败,后续重试会重新走注册;只有不同来源的
                // 工厂争夺同一 UI ID 才是真正的配置冲突。
                if (existing.Method == factory.Method)
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Duplicate FairyGUI UI component factory for UI ID '{uiId}': {existing.Method.Name} vs {factory.Method.Name}.");
            }

            s_Factories[uiId] = factory;
        }

        public static bool TryGet(int uiId, out Func<UIComponent, FairyUIFormComponent> factory)
        {
            return s_Factories.TryGetValue(uiId, out factory);
        }
    }
}
