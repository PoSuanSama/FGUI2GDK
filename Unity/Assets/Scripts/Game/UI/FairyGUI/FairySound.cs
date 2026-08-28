using System;
using System.Collections.Generic;
using FairyGUI;
using GameFramework;
using UnityGameFramework.Runtime;

namespace Game
{
    /// <summary>
    /// FairyGUI 声音桥(阶段 D 声音批,design.md §10.2):
    /// FairyGUI 的按钮/transition 声音原本走 Stage 的 AudioSource 直接播放,
    /// 会绕过 GDK 声音组与音量/静音设置。Stage.PlayOneShotSound 的补丁
    /// 优先把声音请求交给本服务,命中映射则由 GDK Sound 组播放。
    ///
    /// 映射:soundName(资源名,如 "click")-> UISound ID(Luban Sound.xlsx)。
    /// 内置默认映射与表一致:click=10001、select=10000;
    /// 设计期新增声音时经 <see cref="RegisterMapping"/> 登记(或后续生成器产出)。
    /// 未命中映射时记录一次诊断并返回 false,调用方(Stage 补丁)回退原 AudioSource 路径。
    /// </summary>
    public static class FairySound
    {
        private static readonly Dictionary<string, int> s_Mappings =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "click", 10001 },
                { "select", 10000 },
            };

        private static readonly HashSet<string> s_LoggedMisses = new HashSet<string>(StringComparer.Ordinal);

        public static void RegisterMapping(string soundName, int uiSoundId)
        {
            if (string.IsNullOrEmpty(soundName))
            {
                throw new ArgumentNullException(nameof(soundName));
            }

            if (s_Mappings.TryGetValue(soundName, out int existing) && existing != uiSoundId)
            {
                throw new InvalidOperationException(
                    $"FairyGUI sound '{soundName}' is already mapped to UISound '{existing}', cannot remap to '{uiSoundId}'.");
            }

            s_Mappings[soundName] = uiSoundId;
        }

        /// <summary>
        /// 安装重定向钩子:FairyGUI SDK 的按钮/transition 播放统一进入本服务。
        /// bootstrap 在初始化 FairyUIManager 后调用一次。
        /// </summary>
        public static void Initialize()
        {
            if (UIConfig.soundRedirect == null)
            {
                UIConfig.soundRedirect = TryPlay;
            }
        }

        /// <summary>
        /// 把 FairyGUI 声音请求重定向到 GDK Sound 组。返回 true 表示已被 GDK 处理;
        /// false 表示未命中映射(调用方可回退原路径)。
        /// </summary>
        public static bool TryPlay(string soundName, float volumeScale)
        {
            if (string.IsNullOrEmpty(soundName))
            {
                return false;
            }

            if (!s_Mappings.TryGetValue(soundName, out int uiSoundId))
            {
                if (s_LoggedMisses.Add(soundName))
                {
                    GameFrameworkLog.Warning(
                        "FairyGUI sound '{0}' has no GDK UISound mapping; it would fall back to the raw FairyGUI audio path.",
                        soundName);
                }

                return false;
            }

            if (GameEntry.Sound == null)
            {
                GameFrameworkLog.Warning(
                    "FairyGUI sound '{0}' cannot play because the GDK sound component is not ready.",
                    soundName);
                return false;
            }

            // GDK UISound 的优先级/音量由 Luban DRUISound 与 Setting 驱动;
            // FairyGUI 的 volumeScale 是 transition 内缩放,与 GDK 音量语义不同,
            // 由声音组统一控制,这里不叠加。
            int? serialId = GameEntry.Sound.PlayUISound(uiSoundId);
            return serialId.HasValue;
        }
    }
}
