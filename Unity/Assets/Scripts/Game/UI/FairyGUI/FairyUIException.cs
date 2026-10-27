using System;
using GameFramework;

namespace Game
{
    /// <summary>
    /// FairyGUI 接入层的失败原因。业务按此枚举分支处理，
    /// 不再依赖异常消息字符串匹配。
    /// </summary>
    public enum FairyUIError
    {
        Unknown = 0,

        /// <summary>管理器尚未 Initialize 或已 Shutdown。</summary>
        ManagerNotInitialized,

        /// <summary>Luban UI 表在限定帧内未就绪。</summary>
        UIFormTableNotReady,

        /// <summary>UI ID 在 Luban 表中不存在。</summary>
        UIFormNotConfigured,

        /// <summary>单实例界面已加载或正在加载。</summary>
        FormAlreadyOpen,

        /// <summary>GF 打开流程失败（OnInit/OnOpen/资产加载）。</summary>
        FormOpenFailed,

        /// <summary>descriptor 资源加载失败。</summary>
        DescriptorLoadFailed,

        /// <summary>descriptor 内容非法或与 Luban 配置漂移。</summary>
        DescriptorInvalid,

        /// <summary>生成的视图类型与 descriptor 声明不一致。</summary>
        BindingTypeMismatch,

        /// <summary>找不到该界面的 Presenter。</summary>
        PresenterMissing,

        /// <summary>目标 UI 组未注册或未使用 FairyUIGroupHelper。</summary>
        UIGroupMissing,

        /// <summary>包清单缺失或格式错误。</summary>
        PackageManifestInvalid,

        /// <summary>包加载失败（描述符缺失、注册失败）。</summary>
        PackageLoadFailed,

        /// <summary>包未到达 Ready 状态。</summary>
        PackageNotReady,

        /// <summary>底层资源组件不可用或加载失败。</summary>
        ResourceLoadFailed,

        /// <summary>本地化字符串资源缺失。</summary>
        LocalizationMissing,

        /// <summary>本地化字符串资源内容非法。</summary>
        LocalizationInvalid,

        /// <summary>在非 Unity 主线程调用了仅限主线程的接口。</summary>
        ThreadViolation,
    }

    /// <summary>
    /// FairyGUI 接入层异常。携带 <see cref="FairyUIError"/> 供调用方分类处理。
    /// 取消与生命周期失效仍抛 <see cref="OperationCanceledException"/>，不在此列。
    /// </summary>
    public sealed class FairyUIException : GameFrameworkException
    {
        public FairyUIException(FairyUIError error, string message)
            : base(message)
        {
            Error = error;
        }

        public FairyUIException(FairyUIError error, string message, Exception innerException)
            : base(message, innerException)
        {
            Error = error;
        }

        public FairyUIError Error { get; }

        public override string Message => $"[{Error}] {base.Message}";
    }
}
