using System;
using System.Collections.Generic;
using System.Reflection;
using GameFramework;

namespace Game
{
    public static class FairyUIPresenterRegistryBuilder
    {
        public static IReadOnlyDictionary<int, Func<IFairyUIPresenter>> Build(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            Dictionary<int, Func<IFairyUIPresenter>> factories =
                new Dictionary<int, Func<IFairyUIPresenter>>();

            foreach (Type type in assembly.GetTypes())
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

                if (factories.ContainsKey(attribute.UiFormId))
                {
                    throw new GameFrameworkException(
                        $"Duplicate FairyGUI presenter registered for UIFormId '{attribute.UiFormId}'.");
                }

                int uiFormId = attribute.UiFormId;
                factories.Add(uiFormId, () => (IFairyUIPresenter)constructor.Invoke(null));
            }

            return factories;
        }
    }
}
