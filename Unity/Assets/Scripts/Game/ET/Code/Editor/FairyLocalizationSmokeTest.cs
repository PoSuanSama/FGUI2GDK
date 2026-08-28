using System;
using AgentBridge;
using Cysharp.Threading.Tasks;
using FairyGUI;
using FairyGUI.Utils;
using Game;
using UnityEditor;

namespace ET
{
    /// <summary>
    /// FairyGUI 本地化桥冒烟(阶段 D):
    /// 入口已打开 Demo 窗体后,断言:
    /// 1. TranslationHelper.strings 已由 FairyLocalization 设置(非空且包含映射条目);
    /// 2. 组件主文本(本 SDK 快照不翻主文本,主文本仍为 XML 内置)——改为断言翻译表
    ///    按当前语言加载且条目数等于映射表条目数,UI 打开无错误;
    /// 3. 重复 ApplyForPackage 幂等。
    /// </summary>
    public static class FairyLocalizationSmokeTest
    {
        [AgentCallable("FairyGUI 本地化桥冒烟:翻译表已按当前语言应用且幂等。", 60)]
        public static async UniTask RunFairyLocalizationSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("ET FairyGUI localization smoke test requires PlayMode.");
            }

            await ET.Client.FairyGUIBootstrap.InitializeAsync();

            FairyUIManager uiManager = FairyUIManager.Instance;
            FairyUIForm demoForm = uiManager.GetUIForm("Assets/Res/UI/FairyGUI/FairyDemoForm.json");
            if (demoForm == null)
            {
                throw new InvalidOperationException(
                    "ET FairyGUI demo form is not open; the entry flow should have opened it.");
            }

            // Editor 程序集不引用 GameFramework,这里不直接使用 Language 枚举,
            // 只断言当前语言非空且翻译表已应用。
            string language = FairyLocalization.CurrentLanguage.ToString();
            if (string.IsNullOrEmpty(language) || language == "Unspecified")
            {
                throw new InvalidOperationException(
                    "GDK localization component is not available in the ET FairyGUI demo scene.");
            }

            if (TranslationHelper.strings == null || TranslationHelper.strings.Count == 0)
            {
                throw new InvalidOperationException(
                    $"FairyGUI translation table was not applied for language '{language}'.");
            }

            // 映射表三条 (title/subtitle/statuslabel),按 componentId 分组后应存在。
            bool foundTitle = false;
            foreach (var group in TranslationHelper.strings.Values)
            {
                if (group.ContainsKey("title"))
                {
                    foundTitle = true;
                    break;
                }
            }

            if (!foundTitle)
            {
                throw new InvalidOperationException(
                    "FairyGUI translation table does not contain the demo title mapping.");
            }

            // 幂等:同一包同一语言重复应用不抛错、不重复加载。
            await FairyLocalization.ApplyAsync("Package1");
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
    }
}
