using System;
using System.Collections.Generic;
using System.Reflection;
using GameFramework;

namespace Game.Hot
{
    /// <summary>
    /// 通过 FairyUIPresenterAttribute 反射扫描并构建 CSName 到 Presenter 工厂的映射。
    /// </summary>
    public static class FairyUIPresenterRegistryBuilder
    {
        public static IReadOnlyDictionary<string, Func<IFairyUIPresenter>> Build()
        {
            Dictionary<string, Func<IFairyUIPresenter>> factories =
                new Dictionary<string, Func<IFairyUIPresenter>>(StringComparer.Ordinal);

            Type[] types = typeof(FairyUIPresenterRegistryBuilder).Assembly.GetTypes();
            foreach (Type type in types)
            {
                FairyUIPresenterAttribute attribute = type.GetCustomAttribute<FairyUIPresenterAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                if (type.IsAbstract || !typeof(IFairyUIPresenter).IsAssignableFrom(type))
                {
                    throw new GameFrameworkException(
                        $"FairyGUI presenter '{type.FullName}' must be a concrete IFairyUIPresenter.");
                }

                ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
                if (constructor == null)
                {
                    throw new GameFrameworkException(
                        $"FairyGUI presenter '{type.FullName}' requires a parameterless constructor.");
                }

                if (factories.ContainsKey(attribute.CsName))
                {
                    throw new GameFrameworkException(
                        $"Duplicate FairyGUI presenter registered for CSName '{attribute.CsName}'.");
                }

                string csName = attribute.CsName;
                factories.Add(csName, () => (IFairyUIPresenter)constructor.Invoke(null));
            }

            return factories;
        }
    }
}