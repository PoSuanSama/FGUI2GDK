using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FairyGUI;
using FairyGUI.Utils;
using GameFramework.Localization;
using UnityEngine;
using UnityGameFramework.Extension;

namespace Game
{
    /// <summary>
    /// FairyGUI 多语言桥(阶段 D 本地化批):
    /// 把 Luban 导出的 GDK 多语言字典经生成端产出的 FairyGUI strings XML
    /// (Assets/Res/UI/FairyGUI/Package1_strings_&lt;Language&gt;.xml)应用到运行时。
    ///
    /// FairyGUI 的翻译表在首次构造组件时读取并缓存(translated 标记),
    /// 因此必须在包加载后、任何组件创建前设置;
    /// 语言切换按 design MVP:仅支持设置保存后重启语义,运行中切换不重建已打开视图。
    /// </summary>
    public static class FairyLocalization
    {
        /// <summary>
        /// FairyGUI 的 strings source 是全局状态，只记录当前实际应用的 package+language。
        /// </summary>
        private static readonly object s_Gate = new object();
        private static UniTask s_ApplyTail;
        private static bool s_HasApplyTail;
        private static string s_ActivePackageName;
        private static Language s_ActiveLanguage = Language.Unspecified;

        public static Language CurrentLanguage
        {
            get
            {
                if (GameEntry.Localization != null)
                {
                    return GameEntry.Localization.Language;
                }

                return Language.Unspecified;
            }
        }

        /// <summary>
        /// 包加载完成后、创建组件前调用(打开链的 AcquireAsync 与 CreateObject 之间)。
        /// FairyGUI 只有一个全局 strings source，因此不同 package 的应用必须串行；
        /// 当前 source 不是目标 package+language 时重新加载，不能按 package 永久缓存。
        /// </summary>
        public static async UniTask ApplyAsync(
            string packageName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                throw new ArgumentNullException(nameof(packageName));
            }

            UniTask previous;
            bool hasPrevious;
            UniTaskCompletionSource<bool> completion = new UniTaskCompletionSource<bool>();
            lock (s_Gate)
            {
                previous = s_ApplyTail;
                hasPrevious = s_HasApplyTail;
                s_ApplyTail = completion.Task;
                s_HasApplyTail = true;
            }

            try
            {
                if (hasPrevious && cancellationToken.CanBeCanceled)
                {
                    await previous.AttachCancellation(cancellationToken);
                }
                else if (hasPrevious)
                {
                    await previous;
                }

                cancellationToken.ThrowIfCancellationRequested();
                Language language = CurrentLanguage;
                if (language == Language.Unspecified)
                {
                    // 本地化组件未挂载:使用组件 XML 内置文本,不应用翻译表。
                    return;
                }

                lock (s_Gate)
                {
                    if (string.Equals(s_ActivePackageName, packageName, StringComparison.Ordinal) &&
                        s_ActiveLanguage == language)
                    {
                        return;
                    }
                }

                await ApplyStringsAsync(packageName, GetStringsAssetName(packageName, language), cancellationToken);
                lock (s_Gate)
                {
                    s_ActivePackageName = packageName;
                    s_ActiveLanguage = language;
                }
            }
            finally
            {
                completion.TrySetResult(true);
            }
        }

        public static string GetStringsAssetName(string packageName, Language language)
        {
            return $"Assets/Res/UI/FairyGUI/{packageName}_strings_{language}.xml";
        }

        internal static void Reset()
        {
            lock (s_Gate)
            {
                s_ActivePackageName = null;
                s_ActiveLanguage = Language.Unspecified;
            }
        }

        /// <summary>
        /// 加载字符串 XML 并设置全局翻译表;XML 缺失时抛稳定错误
        /// (生成端未运行或语言未导出),不静默回退。
        /// </summary>
        private static async UniTask ApplyStringsAsync(
            string packageName,
            string assetName,
            CancellationToken cancellationToken)
        {
            if (GameEntry.Resource == null)
            {
                throw new InvalidOperationException(
                    "FairyGUI localization requires the GDK resource component before package load.");
            }

            TextAsset stringsAsset = await GameEntry.Resource.LoadAssetAsync<TextAsset>(
                assetName,
                cancellationToken: cancellationToken);
            try
            {
                if (stringsAsset == null || stringsAsset.text == null)
                {
                    throw new InvalidOperationException(
                        $"FairyGUI localization strings asset is missing: {assetName}. Run the FairyGUI localization generator first.");
                }

                FairyPackageManager.VerifyAssetHash(
                    packageName,
                    assetPath: assetName,
                    bytes: stringsAsset.bytes);
                UIPackage.SetStringsSource(new XML(stringsAsset.text));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"FairyGUI localization strings asset is invalid: {assetName}", exception);
            }
            finally
            {
                // SetStringsSource 会立即把 XML 解析成内存字典,文本资产可以马上释放。
                GameEntry.Resource.UnloadAsset(stringsAsset);
            }
        }

    }
}
