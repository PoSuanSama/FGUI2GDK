using System;
using System.Collections.Generic;
using GameFramework;
using Newtonsoft.Json;

namespace Game
{
    /// <summary>
    /// Immutable runtime representation of a versioned FairyGUI UIForm descriptor.
    /// </summary>
    public sealed class FairyUIFormDescriptor
    {
        private const int SupportedSchemaVersion = 1;

        public int SchemaVersion { get; private set; }
        public int UiId { get; private set; }
        public string CsName { get; private set; }
        public string UiAssetName { get; private set; }
        public string UiGroupName { get; private set; }
        public bool AllowMultiInstance { get; private set; }
        public bool PauseCoveredUIForm { get; private set; }
        public string PackageId { get; private set; }
        public string PackageName { get; private set; }
        public string ComponentId { get; private set; }
        public string ComponentName { get; private set; }
        public string BindingType { get; private set; }
        public string PresenterType { get; private set; }
        public IReadOnlyList<string> Dependencies { get; private set; }
        public bool Bootstrap { get; private set; }
        public string Preload { get; private set; }

        public static FairyUIFormDescriptor Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new GameFrameworkException("FairyGUI UI form descriptor is empty.");
            }

            DescriptorData data;
            try
            {
                data = JsonConvert.DeserializeObject<DescriptorData>(json);
            }
            catch (Exception exception)
            {
                throw new GameFrameworkException("FairyGUI UI form descriptor is not valid JSON.", exception);
            }

            if (data == null)
            {
                throw new GameFrameworkException("FairyGUI UI form descriptor is not valid JSON.");
            }

            if (data.SchemaVersion != SupportedSchemaVersion)
            {
                throw new GameFrameworkException(
                    $"FairyGUI UI form descriptor schema must be {SupportedSchemaVersion}, found {data.SchemaVersion}.");
            }

            if (data.UiId <= 0)
            {
                throw new GameFrameworkException("FairyGUI UI form descriptor uiId must be positive.");
            }

            if (string.IsNullOrWhiteSpace(data.CsName) ||
                string.IsNullOrWhiteSpace(data.UiAssetName) ||
                string.IsNullOrWhiteSpace(data.UiGroupName) ||
                string.IsNullOrWhiteSpace(data.PackageName) ||
                string.IsNullOrWhiteSpace(data.ComponentName) ||
                string.IsNullOrWhiteSpace(data.BindingType) ||
                string.IsNullOrWhiteSpace(data.PresenterType))
            {
                throw new GameFrameworkException(
                    "FairyGUI UI form descriptor is missing a required field.");
            }

            return new FairyUIFormDescriptor
            {
                SchemaVersion = data.SchemaVersion,
                UiId = data.UiId,
                CsName = data.CsName,
                UiAssetName = data.UiAssetName,
                UiGroupName = data.UiGroupName,
                AllowMultiInstance = data.AllowMultiInstance,
                PauseCoveredUIForm = data.PauseCoveredUIForm,
                PackageId = data.PackageId ?? string.Empty,
                PackageName = data.PackageName,
                ComponentId = data.ComponentId ?? string.Empty,
                ComponentName = data.ComponentName,
                BindingType = data.BindingType,
                PresenterType = data.PresenterType ?? string.Empty,
                Dependencies = data.Dependencies ?? new List<string>(),
                Bootstrap = data.Bootstrap,
                Preload = data.Preload ?? "onOpen"
            };
        }

        private sealed class DescriptorData
        {
            [JsonProperty("schemaVersion")] public int SchemaVersion;
            [JsonProperty("uiId")] public int UiId;
            [JsonProperty("csName")] public string CsName;
            [JsonProperty("uiAssetName")] public string UiAssetName;
            [JsonProperty("uiGroupName")] public string UiGroupName;
            [JsonProperty("allowMultiInstance")] public bool AllowMultiInstance;
            [JsonProperty("pauseCoveredUIForm")] public bool PauseCoveredUIForm;
            [JsonProperty("packageId")] public string PackageId;
            [JsonProperty("packageName")] public string PackageName;
            [JsonProperty("componentId")] public string ComponentId;
            [JsonProperty("componentName")] public string ComponentName;
            [JsonProperty("bindingType")] public string BindingType;
            [JsonProperty("presenterType")] public string PresenterType;
            [JsonProperty("dependencies")] public List<string> Dependencies;
            [JsonProperty("bootstrap")] public bool Bootstrap;
            [JsonProperty("preload")] public string Preload;
        }
    }
}
