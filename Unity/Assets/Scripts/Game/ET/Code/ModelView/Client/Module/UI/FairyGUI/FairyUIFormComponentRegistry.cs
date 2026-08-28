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
