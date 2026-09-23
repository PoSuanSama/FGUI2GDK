using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AgentBridge;
using Game;
using UnityEditor;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Game.Hot.Editor
{
    public static class FairyUIPresenterRegistryCodeGenerator
    {
        private const string OutputAssetPath =
            "Assets/Scripts/Game/Hot/Code/Generate/FairyGUI/FairyUIPresenterRegistry.Generated.cs";

        [MenuItem("GameHot/FairyGUI/Generate Presenter Registry")]
        [AgentCallable("Generate the GameHot FairyGUI presenter factory table from compiled presenter attributes.", 60)]
        public static void GenerateFairyUIPresenterRegistry()
        {
            string source = BuildSource();
            string outputPath = GetOutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            if (File.Exists(outputPath) &&
                string.Equals(File.ReadAllText(outputPath), source, StringComparison.Ordinal))
            {
                Log.Info("FairyGUI presenter registry is already up to date: {0}", OutputAssetPath);
                return;
            }

            File.WriteAllText(outputPath, source, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(OutputAssetPath, ImportAssetOptions.ForceUpdate);
            Log.Info("Generated FairyGUI presenter registry: {0}", OutputAssetPath);
        }

        [MenuItem("GameHot/FairyGUI/Validate Presenter Registry")]
        [AgentCallable("Verify the generated GameHot FairyGUI presenter source and factories match presenter attributes.", 60)]
        public static void ValidateFairyUIPresenterRegistry()
        {
            string expectedSource = BuildSource();
            string outputPath = GetOutputPath();
            if (!File.Exists(outputPath))
            {
                throw new FileNotFoundException(
                    $"Generated FairyGUI presenter registry is missing: {OutputAssetPath}",
                    outputPath);
            }

            string actualSource = File.ReadAllText(outputPath);
            if (!string.Equals(actualSource, expectedSource, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Generated FairyGUI presenter registry is stale: {OutputAssetPath}. " +
                    "Run Generate Presenter Registry and wait for Unity compilation.");
            }

            IReadOnlyDictionary<int, Func<IFairyUIPresenter>> reflectedFactories =
                FairyUIPresenterRegistryBuilder.Build(typeof(HotEntry).Assembly);
            IReadOnlyDictionary<int, Func<IFairyUIPresenter>> generatedFactories =
                CreateGeneratedFactories();
            if (reflectedFactories.Count != generatedFactories.Count)
            {
                throw new InvalidOperationException(
                    $"FairyGUI presenter factory count mismatch: expected {reflectedFactories.Count}, " +
                    $"found {generatedFactories.Count}.");
            }

            foreach (KeyValuePair<int, Func<IFairyUIPresenter>> entry in reflectedFactories)
            {
                if (!generatedFactories.TryGetValue(entry.Key, out Func<IFairyUIPresenter> generatedFactory))
                {
                    throw new InvalidOperationException(
                        $"Generated FairyGUI presenter registry is missing UIFormId '{entry.Key}'.");
                }

                Type expectedType = entry.Value().GetType();
                Type actualType = generatedFactory()?.GetType();
                if (actualType != expectedType)
                {
                    throw new InvalidOperationException(
                        $"Generated FairyGUI presenter type mismatch for UIFormId '{entry.Key}': " +
                        $"expected '{expectedType.FullName}', found '{actualType?.FullName ?? "<null>"}'.");
                }
            }

            Log.Info(
                "Validated {0} generated FairyGUI presenter factories against compiled attributes.",
                generatedFactories.Count);
        }

        private static IReadOnlyDictionary<int, Func<IFairyUIPresenter>> CreateGeneratedFactories()
        {
            Assembly assembly = typeof(HotEntry).Assembly;
            Type registryType = assembly.GetType(
                "Game.Hot.FairyUIPresenterRegistryGenerated",
                throwOnError: true);
            MethodInfo createMethod = registryType.GetMethod(
                "CreateFactories",
                BindingFlags.Public | BindingFlags.Static);
            if (createMethod == null)
            {
                throw new MissingMethodException(registryType.FullName, "CreateFactories");
            }

            object result = createMethod.Invoke(null, null);
            if (result is not IReadOnlyDictionary<int, Func<IFairyUIPresenter>> factories)
            {
                throw new InvalidOperationException(
                    $"Generated FairyGUI presenter registry returned an unexpected value: " +
                    $"'{result?.GetType().FullName ?? "<null>"}'.");
            }

            return factories;
        }

        private static string BuildSource()
        {
            Assembly assembly = typeof(HotEntry).Assembly;
            IReadOnlyDictionary<int, Func<IFairyUIPresenter>> validatedFactories =
                FairyUIPresenterRegistryBuilder.Build(assembly);
            Dictionary<int, string> uiFormIdNames = GetUIFormIdNames(validatedFactories.Keys);
            List<PresenterRegistration> registrations = new List<PresenterRegistration>();

            foreach (Type type in assembly.GetTypes())
            {
                FairyUIPresenterAttribute attribute =
                    type.GetCustomAttribute<FairyUIPresenterAttribute>();
                if (attribute == null)
                {
                    continue;
                }

                if (!type.IsPublic ||
                    type.IsNested ||
                    type.IsGenericType ||
                    string.IsNullOrEmpty(type.FullName))
                {
                    throw new InvalidOperationException(
                        $"FairyGUI presenter '{type.FullName}' must be a public, top-level, " +
                        "non-generic type for static registry generation.");
                }

                registrations.Add(new PresenterRegistration(
                    attribute.UiFormId,
                    uiFormIdNames[attribute.UiFormId],
                    type.FullName));
            }

            registrations.Sort((left, right) => left.UiFormId.CompareTo(right.UiFormId));
            if (registrations.Count != validatedFactories.Count)
            {
                throw new InvalidOperationException(
                    $"FairyGUI presenter registry source count changed during generation: " +
                    $"expected {validatedFactories.Count}, found {registrations.Count}.");
            }

            StringBuilder source = new StringBuilder();
            source.AppendLine("// <auto-generated />");
            source.AppendLine("// Generated by FairyUIPresenterRegistryCodeGenerator. Do not edit.");
            source.AppendLine();
            source.AppendLine("using System;");
            source.AppendLine("using System.Collections.Generic;");
            source.AppendLine("using Game;");
            source.AppendLine();
            source.AppendLine("namespace Game.Hot");
            source.AppendLine("{");
            source.AppendLine("    internal static class FairyUIPresenterRegistryGenerated");
            source.AppendLine("    {");
            source.AppendLine("        public static IReadOnlyDictionary<int, Func<IFairyUIPresenter>> CreateFactories()");
            source.AppendLine("        {");
            source.AppendLine("            return new Dictionary<int, Func<IFairyUIPresenter>>");
            source.AppendLine("            {");

            foreach (PresenterRegistration registration in registrations)
            {
                source.Append("                { UIFormId.");
                source.Append(registration.UiFormIdName);
                source.Append(", static () => new global::");
                source.Append(registration.PresenterTypeName);
                source.AppendLine("() },");
            }

            source.AppendLine("            };");
            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine("}");

            return source.ToString().Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static Dictionary<int, string> GetUIFormIdNames(IEnumerable<int> uiFormIds)
        {
            HashSet<int> requestedIds = new HashSet<int>(uiFormIds);
            Dictionary<int, string> names = new Dictionary<int, string>();
            FieldInfo[] fields = typeof(UIFormId).GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType != typeof(int) || !field.IsLiteral)
                {
                    continue;
                }

                int value = (int)field.GetRawConstantValue();
                if (!requestedIds.Contains(value))
                {
                    continue;
                }

                if (!names.TryAdd(value, field.Name))
                {
                    throw new InvalidOperationException(
                        $"UIFormId '{value}' has multiple generated constant names; " +
                        "FairyGUI presenter registry generation requires unique IDs.");
                }
            }

            foreach (int uiFormId in requestedIds)
            {
                if (!names.ContainsKey(uiFormId))
                {
                    throw new InvalidOperationException(
                        $"FairyGUI presenter UIFormId '{uiFormId}' has no generated UIFormId constant.");
                }
            }

            return names;
        }

        private static string GetOutputPath()
        {
            string relativePath = OutputAssetPath.Substring("Assets/".Length)
                .Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.dataPath, relativePath);
        }

        private sealed class PresenterRegistration
        {
            public PresenterRegistration(int uiFormId, string uiFormIdName, string presenterTypeName)
            {
                UiFormId = uiFormId;
                UiFormIdName = uiFormIdName;
                PresenterTypeName = presenterTypeName;
            }

            public int UiFormId { get; }
            public string UiFormIdName { get; }
            public string PresenterTypeName { get; }
        }
    }
}
