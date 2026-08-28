using System;
using AgentBridge;
using Cysharp.Threading.Tasks;
using ET.Client;
using FairyGUI;
using Game;
using UnityEditor;
using UnityEngine;

namespace ET
{
    /// <summary>
    /// FairyGUI 安全区桥冒烟(阶段 D,design §10.3):
    /// 断言组内双容器存在、窗体默认挂安全区容器、像素->GRoot 换算正确
    /// (Y 轴翻转 + contentScaleFactor 缩放)、幂等重算。
    /// 用 ApplySafeAreaRect 注入任意像素矩形验证换算,不依赖真实设备安全区。
    /// </summary>
    public static class FairySafeAreaSmokeTest
    {
        [AgentCallable("FairyGUI 安全区桥冒烟:双容器、换算与幂等重算。", 60)]
        public static async UniTask RunFairySafeAreaSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("ET FairyGUI safe area smoke test requires PlayMode.");
            }

            await FairyGUIBootstrap.InitializeAsync();

            FairyUIManager uiManager = FairyUIManager.Instance;
            FairyUIForm demoForm = uiManager.GetUIForm("Assets/Res/UI/FairyGUI/FairyDemoForm.json");
            if (demoForm == null)
            {
                throw new InvalidOperationException(
                    "ET FairyGUI demo form is not open; the entry flow should have opened it.");
            }

            FairyUIGroupHelper groupHelper = demoForm.UIGroup.Helper as FairyUIGroupHelper;
            if (groupHelper == null || groupHelper.SafeAreaContainer == null)
            {
                throw new InvalidOperationException(
                    "FairyGUI UI group does not provide a safe area container.");
            }

            // 窗体默认挂安全区容器。
            GObject view = demoForm.View;
            if (view == null || view.displayObject == null ||
                !ReferenceEquals(view.displayObject.parent, groupHelper.SafeAreaContainer.displayObject))
            {
                throw new InvalidOperationException(
                    "FairyGUI form was not attached to the safe area container by default.");
            }

            // 换算验证:注入 1280x720 屏幕中 (0,0,320,80) 像素安全区
            // (顶部 inset 80px,高度 640)。GRoot 设计分辨率为 1280x720,
            // contentScaleFactor = 屏幕高度/720(假设 MatchWidthOrHeight 按高匹配)。
            float scaleFactor = GRoot.contentScaleFactor;
            if (scaleFactor <= 0f)
            {
                throw new InvalidOperationException("GRoot content scale factor is not positive.");
            }

            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            Rect injected = new Rect(0f, screenHeight - 80f * scaleFactor, screenWidth, 80f * scaleFactor);
            groupHelper.ApplySafeAreaRect(injected);

            float expectedX = injected.xMin / scaleFactor;
            float expectedY = (screenHeight - injected.yMax) / scaleFactor;
            float expectedWidth = injected.width / scaleFactor;
            float expectedHeight = injected.height / scaleFactor;

            // 顶部 80 设计像素的安全区:容器应在顶部,y=0 高 80。
            if (Mathf.Abs(expectedY) > 0.01f || Mathf.Abs(expectedHeight - 80f) > 0.5f)
            {
                throw new InvalidOperationException(
                    $"Safe area conversion mismatch: expected y={expectedY} h={expectedHeight}.");
            }

            GComponent safeAreaContainer = groupHelper.SafeAreaContainer;
            if (Mathf.Abs(safeAreaContainer.y - expectedY) > 0.5f ||
                Mathf.Abs(safeAreaContainer.height - expectedHeight) > 0.5f ||
                Mathf.Abs(safeAreaContainer.x - expectedX) > 0.5f ||
                Mathf.Abs(safeAreaContainer.width - expectedWidth) > 0.5f)
            {
                throw new InvalidOperationException(
                    $"Safe area container mismatch: pos=({safeAreaContainer.x},{safeAreaContainer.y}) " +
                    $"size=({safeAreaContainer.width},{safeAreaContainer.height}) " +
                    $"expected=({expectedX},{expectedY},{expectedWidth},{expectedHeight}).");
            }

            // 幂等:同矩形重算不改变容器。
            float appliedY = safeAreaContainer.y;
            groupHelper.ApplySafeAreaRect(injected);
            if (Mathf.Abs(safeAreaContainer.y - appliedY) > 0.001f)
            {
                throw new InvalidOperationException("Safe area re-apply was not idempotent.");
            }

            // 恢复真实屏幕安全区,让后续冒烟在正常布局下运行。
            groupHelper.ApplySafeAreaRect(Screen.safeArea);
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
    }
}
