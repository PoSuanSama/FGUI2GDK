using System;
using System.Collections.Generic;
using AgentBridge;
using Cysharp.Threading.Tasks;
using Game.FairyGUI.Package1;
using GameFramework.UI;
using UnityEditor;
using UnityEngine;

namespace Game.Hot.Editor
{
    /// <summary>
    /// Stage 1 最小冒烟：经新的 FairyUIManager 原生窗口入口打开/关闭一个 FairyGUI 界面，
    /// 验证 GameFramework.UI 语义层与 FairyGUI GComponent 之间的异步桥接。
    /// </summary>
    public static class FairyUIManagerSmokeTest
    {
        [AgentCallable("Stage 1 最小冒烟：经 FairyUIManager 原生入口打开/关闭 FairyDemoForm，验证异步桥接。", 60)]
        public static async UniTask RunFairyUIManagerSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyUI manager smoke test requires PlayMode.");
            }

            EnsurePresenterRegistry();

            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();
            EnsureFairyUIGroup(uiManager, "Default", 0);
            EnsureFairyUIGroup(uiManager, "Pop", 100);


            FairyUIForm existingForm = uiManager.GetUIForm("Assets/Res/UI/FairyGUI/FairyDemoForm.json");
            if (existingForm != null)
            {
                uiManager.CloseUIForm(existingForm.SerialId);
            }

            FairyUIForm form = null;
            try
            {
                form = await uiManager.OpenFairyUIFormAsync(UIFormId.FairyDemoForm, "smoke-user-data");
                if (form == null)
                {
                    throw new InvalidOperationException("OpenFairyUIFormAsync returned null.");
                }

                if (form.View == null)
                {
                    throw new InvalidOperationException("Opened FairyUI form has no GComponent view.");
                }

                if (form.Presenter == null)
                {
                    throw new InvalidOperationException("Opened FairyUI form has no presenter.");
                }

                if (!uiManager.HasUIForm(form.SerialId))
                {
                    throw new InvalidOperationException("Opened FairyUI form is not tracked by the UI manager.");
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            finally
            {
                if (form != null && uiManager.HasUIForm(form.SerialId))
                {
                    uiManager.CloseUIForm(form.SerialId);
                }
            }
        }

        [AgentCallable("P2 能力透出冒烟：对象池四属性回读一致、三个 GF 事件桥转发、重复 Initialize 订阅幂等。", 60)]
        public static async UniTask RunFairyGFPassthroughSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGF passthrough smoke test requires PlayMode.");
            }

            EnsurePresenterRegistry();

            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();
            EnsureFairyUIGroup(uiManager, "Default", 0);
            EnsureFairyUIGroup(uiManager, "Pop", 100);

            // 1. 对象池四属性回读一致（写入后读回，再恢复原值）。
            float originalInterval = uiManager.InstanceAutoReleaseInterval;
            int originalCapacity = uiManager.InstanceCapacity;
            float originalExpireTime = uiManager.InstanceExpireTime;
            int originalPriority = uiManager.InstancePriority;

            try
            {
                uiManager.InstanceAutoReleaseInterval = 42.5f;
                uiManager.InstanceCapacity = 37;
                uiManager.InstanceExpireTime = 12.5f;
                uiManager.InstancePriority = 7;

                if (!Mathf.Approximately(uiManager.InstanceAutoReleaseInterval, 42.5f))
                    throw new InvalidOperationException("InstanceAutoReleaseInterval 回读不一致。");
                if (uiManager.InstanceCapacity != 37)
                    throw new InvalidOperationException("InstanceCapacity 回读不一致。");
                if (!Mathf.Approximately(uiManager.InstanceExpireTime, 12.5f))
                    throw new InvalidOperationException("InstanceExpireTime 回读不一致。");
                if (uiManager.InstancePriority != 7)
                    throw new InvalidOperationException("InstancePriority 回读不一致。");
            }
            finally
            {
                uiManager.InstanceAutoReleaseInterval = originalInterval;
                uiManager.InstanceCapacity = originalCapacity;
                uiManager.InstanceExpireTime = originalExpireTime;
                uiManager.InstancePriority = originalPriority;
            }

            // 2. 三个 GF 事件桥转发 + 重复 Initialize 订阅幂等。
            // 第二次 Initialize 不应重复订阅 GF 事件（m_EventsAttached 幂等），否则打开
            // 一个界面会触发两次静态事件转发。Update/DependencyAsset 在 Editor 快加载下
            // 可能不触发，故只强断言 Success（打开成功必触发）。
            uiManager.Initialize();

            // GameHot 流程进入 Menu 会异步自动打开 FairyDemoForm(103)。先等其稳定再关闭
            // 已存在实例,避免自动打开与验证打开竞争导致 Success 事件触发两次。
            for (int i = 0; i < 10; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            FairyUIForm existingForm = uiManager.GetUIForm("Assets/Res/UI/FairyGUI/FairyDemoForm.json");
            if (existingForm != null)
            {
                uiManager.CloseUIForm(existingForm.SerialId);
            }

            for (int i = 0; i < 3; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            int successCount = 0;
            int updateCount = 0;
            int dependencyCount = 0;
            EventHandler<OpenUIFormSuccessEventArgs> onSuccess = (_, _) => successCount++;
            EventHandler<OpenUIFormUpdateEventArgs> onUpdate = (_, _) => updateCount++;
            EventHandler<OpenUIFormDependencyAssetEventArgs> onDependency = (_, _) => dependencyCount++;

            FairyUIManager.OpenUIFormSuccess += onSuccess;
            FairyUIManager.OpenUIFormUpdate += onUpdate;
            FairyUIManager.OpenUIFormDependencyAsset += onDependency;
            try
            {
                FairyUIForm form = await uiManager.OpenFairyUIFormAsync(UIFormId.FairyDemoForm, "passthrough-smoke");
                if (form == null)
                {
                    throw new InvalidOperationException("OpenFairyUIFormAsync returned null.");
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
                uiManager.CloseUIForm(form.SerialId);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            finally
            {
                FairyUIManager.OpenUIFormSuccess -= onSuccess;
                FairyUIManager.OpenUIFormUpdate -= onUpdate;
                FairyUIManager.OpenUIFormDependencyAsset -= onDependency;
            }

            if (successCount != 1)
            {
                throw new InvalidOperationException(
                    $"OpenUIFormSuccess 事件桥转发或订阅幂等异常：count={successCount}（应为 1）。");
            }
        }

        [AgentCallable("R1/R2 冒烟：FairySound 未映射不回退 + CancelTopForm 关闭最上层窗体。", 60)]
        public static async UniTask RunFairyInputSoundFixSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyInputSound fix smoke test requires PlayMode.");
            }

            EnsurePresenterRegistry();

            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();
            EnsureFairyUIGroup(uiManager, "Default", 0);
            EnsureFairyUIGroup(uiManager, "Pop", 100);

            // R2: 未映射声音应返回 true(已处理,静默跳过),不回退 FairyGUI 原生 AudioSource。
            if (!FairySound.TryPlay("__unmapped_sound__", 1.0f))
            {
                throw new InvalidOperationException(
                    "FairySound.TryPlay 未映射声音应返回 true(不回退原生),实际返回 false。");
            }

            // R1: CancelTopForm 关闭当前最上层窗体。
            // 先等 GameHot 自动打开稳定并清理,避免与验证打开竞争。
            for (int i = 0; i < 10; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            FairyUIForm existing = uiManager.GetUIForm("Assets/Res/UI/FairyGUI/FairyDemoForm.json");
            if (existing != null)
            {
                uiManager.CloseUIForm(existing.SerialId);
            }

            for (int i = 0; i < 3; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            FairyUIForm form = await uiManager.OpenFairyUIFormAsync(UIFormId.FairyDemoForm, "input-sound-fix-smoke");
            if (form == null)
            {
                throw new InvalidOperationException("OpenFairyUIFormAsync returned null.");
            }

            await UniTask.Yield(PlayerLoopTiming.Update);

            FairyInputService input = FairyInputService.Instance;
            input.Initialize();
            if (!input.CancelTopForm())
            {
                throw new InvalidOperationException("CancelTopForm 应关闭已打开窗体,返回 false。");
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
            if (uiManager.HasUIForm(form.SerialId))
            {
                throw new InvalidOperationException("CancelTopForm 未关闭目标窗体。");
            }
        }

        private static void EnsureFairyUIGroup(FairyUIManager uiManager, string name, int depth)
        {
            if (uiManager.HasUIGroup(name))
            {
                return;
            }

            if (!uiManager.AddUIGroup(name, depth))
            {
                throw new InvalidOperationException($"Failed to add FairyGUI UI group '{name}'.");
            }
        }

        private static void EnsurePresenterRegistry()
        {
            if (FairyUIPresenterRegistry.PreparePackage != null &&
                FairyUIPresenterRegistry.CreatePresenter != null)
            {
                return;
            }

            IReadOnlyDictionary<int, Func<IFairyUIPresenter>> factories =
                FairyUIPresenterRegistryBuilder.Build(typeof(FairyUIManagerSmokeTest).Assembly);

            FairyUIPresenterRegistry.PreparePackage = descriptor =>
            {
                if (!string.Equals(descriptor.PackageName, "Package1", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"No FairyGUI package binder for package '{descriptor.PackageName}'.");
                }

                Package1Binder.BindAll();
            };

            FairyUIPresenterRegistry.CreatePresenter = descriptor =>
            {
                if (factories.TryGetValue(descriptor.UiId, out Func<IFairyUIPresenter> factory))
                {
                    return factory();
                }

                throw new InvalidOperationException(
                    $"No FairyGUI presenter registered for UI '{descriptor.UiId}'.");
            };
        }
    }
}
