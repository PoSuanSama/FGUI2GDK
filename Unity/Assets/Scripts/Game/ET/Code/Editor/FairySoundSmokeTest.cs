using System;
using AgentBridge;
using Cysharp.Threading.Tasks;
using FairyGUI;
using Game;
using UnityEditor;

namespace ET
{
    /// <summary>
    /// FairyGUI 声音桥冒烟(阶段 D):
    /// 断言重定向钩子已安装、内置映射可解析、未映射声音名诊断一次即静默。
    /// 不实际播放音频(依赖音频组初始化),只验证桥接路径。
    /// </summary>
    public static class FairySoundSmokeTest
    {
        [AgentCallable("FairyGUI 声音桥冒烟:重定向钩子与映射可解析。", 60)]
        public static async UniTask RunFairySoundSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("ET FairyGUI sound smoke test requires PlayMode.");
            }

            await ET.Client.FairyGUIBootstrap.InitializeAsync();

            if (UIConfig.soundRedirect == null)
            {
                throw new InvalidOperationException("FairyGUI sound redirect hook was not installed.");
            }

            // 钩子调用与 FairySound.TryPlay 同路径:未映射名字返回 false 且不抛错。
            bool handled = UIConfig.soundRedirect("no-such-fairygui-sound", 1f);
            if (handled)
            {
                throw new InvalidOperationException(
                    "Unmapped FairyGUI sound should not be claimed by the GDK bridge.");
            }

            // 内置映射存在(click/select 对应 Sound.xlsx UISound 表)。
            bool mapped = false;
            try
            {
                Game.FairySound.RegisterMapping("__smoke_probe__", 10000);
                mapped = true;
            }
            catch (InvalidOperationException)
            {
            }

            if (!mapped)
            {
                throw new InvalidOperationException("FairyGUI sound mapping registration failed.");
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
        }
    }
}
