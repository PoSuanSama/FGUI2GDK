namespace Game
{
    /// <summary>
    /// 界面打开流程所处的阶段。
    /// </summary>
    public enum FairyUIOpenStage
    {
        /// <summary>读取并校验 descriptor。</summary>
        Descriptor = 0,

        /// <summary>加载 FairyGUI 包及其依赖。</summary>
        Package,

        /// <summary>应用本地化字符串。</summary>
        Localization,

        /// <summary>创建视图并校验绑定类型。</summary>
        View,

        /// <summary>GF 加载窗体资产与依赖资源。</summary>
        Asset,

        /// <summary>打开完成。</summary>
        Completed,
    }

    /// <summary>
    /// 一次界面打开的进度快照。Value 单调递增到 1.0；
    /// 取消与失败不会推送 <see cref="FairyUIOpenStage.Completed"/>。
    /// </summary>
    public readonly struct FairyUIOpenProgress
    {
        public FairyUIOpenProgress(FairyUIOpenStage stage, float value, string packageName)
        {
            Stage = stage;
            Value = value;
            PackageName = packageName;
        }

        public FairyUIOpenStage Stage { get; }

        /// <summary>0 到 1 的单调进度。</summary>
        public float Value { get; }

        /// <summary>当前阶段涉及的包名；descriptor 阶段为 null。</summary>
        public string PackageName { get; }
    }
}
