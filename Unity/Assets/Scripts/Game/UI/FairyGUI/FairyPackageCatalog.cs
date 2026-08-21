using System;
using System.Collections.Generic;
using GameFramework;
using Newtonsoft.Json;

namespace Game
{
    internal sealed class FairyPackageCatalog
    {
        internal const int SupportedSchemaVersion = 1;

        private readonly Dictionary<string, PackageDefinition> m_PackagesByName;

        private FairyPackageCatalog(Dictionary<string, PackageDefinition> packagesByName)
        {
            m_PackagesByName = packagesByName;
        }

        internal static FairyPackageCatalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new GameFrameworkException("FairyGUI runtime manifest is empty.");
            }

            ManifestData manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<ManifestData>(json);
            }
            catch (Exception exception)
            {
                throw new GameFrameworkException("FairyGUI runtime manifest is invalid JSON.", exception);
            }

            if (manifest == null || manifest.SchemaVersion != SupportedSchemaVersion)
            {
                throw new GameFrameworkException(
                    $"FairyGUI runtime manifest schema must be {SupportedSchemaVersion}.");
            }

            if (manifest.Packages == null || manifest.Packages.Length == 0)
            {
                throw new GameFrameworkException("FairyGUI runtime manifest contains no packages.");
            }

            Dictionary<string, PackageDefinition> packagesById =
                new Dictionary<string, PackageDefinition>(StringComparer.Ordinal);
            Dictionary<string, PackageDefinition> packagesByName =
                new Dictionary<string, PackageDefinition>(StringComparer.Ordinal);

            foreach (PackageData packageData in manifest.Packages)
            {
                if (packageData == null || string.IsNullOrWhiteSpace(packageData.Id))
                {
                    throw new GameFrameworkException("FairyGUI runtime manifest contains a package without an id.");
                }

                if (string.IsNullOrWhiteSpace(packageData.Name))
                {
                    throw new GameFrameworkException(
                        $"FairyGUI runtime manifest package '{packageData.Id}' has no name.");
                }

                PackageDefinition definition = new PackageDefinition(packageData.Id, packageData.Name);
                if (!packagesById.TryAdd(definition.Id, definition))
                {
                    throw new GameFrameworkException(
                        $"FairyGUI runtime manifest contains duplicate package id '{definition.Id}'.");
                }

                if (!packagesByName.TryAdd(definition.Name, definition))
                {
                    throw new GameFrameworkException(
                        $"FairyGUI runtime manifest contains duplicate package name '{definition.Name}'.");
                }
            }

            for (int i = 0; i < manifest.Packages.Length; i++)
            {
                PackageData packageData = manifest.Packages[i];
                PackageDefinition definition = packagesById[packageData.Id];
                HashSet<string> dependencyIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string dependencyId in packageData.Dependencies ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(dependencyId))
                    {
                        throw new GameFrameworkException(
                            $"FairyGUI package '{definition.Name}' contains an empty dependency id.");
                    }

                    if (!dependencyIds.Add(dependencyId))
                    {
                        continue;
                    }

                    if (!packagesById.TryGetValue(dependencyId, out PackageDefinition dependency))
                    {
                        throw new GameFrameworkException(
                            $"FairyGUI package '{definition.Name}' references unknown dependency '{dependencyId}'.");
                    }

                    definition.Dependencies.Add(dependency);
                }
            }

            FairyPackageCatalog catalog = new FairyPackageCatalog(packagesByName);
            catalog.ValidateAcyclic();
            return catalog;
        }

        internal IReadOnlyList<PackageDefinition> GetLoadOrder(string packageName)
        {
            if (!m_PackagesByName.TryGetValue(packageName, out PackageDefinition root))
            {
                throw new GameFrameworkException(
                    $"FairyGUI package '{packageName}' is not declared in the runtime manifest.");
            }

            List<PackageDefinition> loadOrder = new List<PackageDefinition>();
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            AppendLoadOrder(root, visited, loadOrder);
            return loadOrder;
        }

        private void ValidateAcyclic()
        {
            Dictionary<string, VisitState> visitStates =
                new Dictionary<string, VisitState>(StringComparer.Ordinal);
            List<PackageDefinition> stack = new List<PackageDefinition>();
            foreach (PackageDefinition package in m_PackagesByName.Values)
            {
                Visit(package, visitStates, stack);
            }
        }

        private static void Visit(
            PackageDefinition package,
            Dictionary<string, VisitState> visitStates,
            List<PackageDefinition> stack)
        {
            visitStates.TryGetValue(package.Id, out VisitState state);
            if (state == VisitState.Visited)
            {
                return;
            }

            if (state == VisitState.Visiting)
            {
                int cycleStart = stack.FindIndex(candidate => candidate.Id == package.Id);
                List<string> cycle = new List<string>();
                for (int i = cycleStart; i < stack.Count; i++)
                {
                    cycle.Add(stack[i].Name);
                }

                cycle.Add(package.Name);
                throw new GameFrameworkException(
                    $"FairyGUI package dependency cycle: {string.Join(" -> ", cycle)}.");
            }

            visitStates[package.Id] = VisitState.Visiting;
            stack.Add(package);
            foreach (PackageDefinition dependency in package.Dependencies)
            {
                Visit(dependency, visitStates, stack);
            }

            stack.RemoveAt(stack.Count - 1);
            visitStates[package.Id] = VisitState.Visited;
        }

        private static void AppendLoadOrder(
            PackageDefinition package,
            HashSet<string> visited,
            List<PackageDefinition> loadOrder)
        {
            if (!visited.Add(package.Id))
            {
                return;
            }

            foreach (PackageDefinition dependency in package.Dependencies)
            {
                AppendLoadOrder(dependency, visited, loadOrder);
            }

            loadOrder.Add(package);
        }

        internal sealed class PackageDefinition
        {
            internal readonly string Id;
            internal readonly string Name;
            internal readonly List<PackageDefinition> Dependencies = new List<PackageDefinition>();

            internal PackageDefinition(string id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        private enum VisitState
        {
            Unvisited,
            Visiting,
            Visited,
        }

        private sealed class ManifestData
        {
            [JsonProperty("schemaVersion")]
            public int SchemaVersion;

            [JsonProperty("packages")]
            public PackageData[] Packages;
        }

        private sealed class PackageData
        {
            [JsonProperty("id")]
            public string Id;

            [JsonProperty("name")]
            public string Name;

            [JsonProperty("dependencies")]
            public string[] Dependencies;
        }
    }
}
