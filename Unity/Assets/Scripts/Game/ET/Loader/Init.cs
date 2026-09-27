using System;
using CommandLine;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;
using UnityGameFramework.Extension;

namespace ET
{
    [DisallowMultipleComponent]
    public class Init : MonoBehaviour
    {
        public static Init Instance { get; private set; }

        private class Runner : MonoBehaviour
        {
            private Init m_Owner;

            private void Update()
            {
                if (TimeInfo.Instance == null || FiberManager.Instance == null)
                {
                    return;
                }

                TimeInfo.Instance.Update();
                FiberManager.Instance.Update();
            }

            private void LateUpdate()
            {
                if (FiberManager.Instance == null)
                {
                    return;
                }

                FiberManager.Instance.LateUpdate();
            }

            private void OnDestroy()
            {
                this.m_Owner?.ShutdownRuntime();
            }

            internal void SetOwner(Init owner)
            {
                this.m_Owner = owner;
            }

            private void OnApplicationPause(bool pauseStatus)
            {
                EventSystem.Instance?.Invoke(new OnApplicationPause(pauseStatus));
            }

            private void OnApplicationFocus(bool hasFocus)
            {
                EventSystem.Instance?.Invoke(new OnApplicationFocus(hasFocus));
            }
        }

        private Runner m_RunnerComponent;
        private bool m_UnhandledExceptionSubscribed;
        private bool m_Destroyed;
        private bool m_WorldOwned;
        private bool m_RuntimeShutdown;

        private void Awake()
        {
            Instance = this;
#if UNITY_ET_VIEW && UNITY_EDITOR
            Entity.SetRootView(this.transform);
#endif
        }

        private void Start()
        {
            StartAsync().Forget();
        }

        private void OnDestroy()
        {
            this.m_Destroyed = true;
            this.UnsubscribeUnhandledException();
            try
            {
                if (this.m_RunnerComponent != null)
                {
                    this.ShutdownRuntime();
                    this.m_RunnerComponent = null;
                }
                else if (this.m_WorldOwned)
                {
                    this.ShutdownRuntime();
                }
            }
            finally
            {
                if (ReferenceEquals(Instance, this))
                {
                    Instance = null;
                }
            }
        }

        private async UniTaskVoid StartAsync()
        {
            // GameEntry 的 Awake 先于场景对象 Start:GF 组件在 GameEntry.Start 后才可用。
            // ET 与 GameHot 并存时(Standalone 双符号冒烟),这里的 Start 可能在 GameEntry.Start
            // 之前执行,直接访问 GameEntry.CodeRunner 会空引用。有界等待 GameEntry 就绪,
            // 失败时记录诊断而不是抛出(启动链不应被自身初始化顺序打断)。
            int waitFrames = 0;
            while (waitFrames < 120 &&
                   (GameEntry.Base == null || GameEntry.CodeRunner == null))
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (!this.IsCurrentInstance())
                {
                    return;
                }
                waitFrames++;
            }

            if (!this.IsCurrentInstance())
            {
                return;
            }

            if (GameEntry.Base == null || GameEntry.CodeRunner == null)
            {
                LogWarningFallback(
                    "ET Init: GameEntry components are not ready within 120 frames; continue with degraded init.");
            }

            // Awaitable 扩展要求先订阅 GF 事件;双符号模式下 GameHot 的 ProcedureLaunch
            // 可能晚于 ET 初始化执行,这里按幂等调用,避免重复订阅 handler。
            if (!UnityGameFramework.Extension.Awaitable.IsValid)
            {
                UnityGameFramework.Extension.Awaitable.SubscribeEvent();
            }

            if (!this.IsCurrentInstance())
            {
                return;
            }

            // 命令行参数
            string[] args = "".Split(" ");
            this.m_WorldOwned = true;
            Parser.Default.ParseArguments<Options>(args)
                    .WithNotParsed(error => throw new Exception($"命令行格式错误! {error}"))
                    .WithParsed((o) => World.Instance.AddSingleton(o));
            Options.Instance.StartConfig = "Localhost";

            World.Instance.AddSingleton<Logger, ILog>(new UnityLogger());
            this.SubscribeUnhandledException();

            if (!this.IsCurrentInstance())
            {
                return;
            }

            World.Instance.AddSingleton<TimeInfo, ITimeNow>(new UnityTimeNow());
            World.Instance.AddSingleton<FiberManager>();
            World.Instance.AddSingleton<ConfigComponent, IConfigReader>(new ConfigReader());
            World.Instance.AddSingleton<CodeLoaderComponent, ICodeLoader>(new CodeLoader());

            await CodeLoaderComponent.Instance.StartAsync();
            if (!this.IsCurrentInstance())
            {
                return;
            }

            Runner runner = this.gameObject.AddComponent<Runner>();
            runner.SetOwner(this);
            this.m_RunnerComponent = runner;
        }

        private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
        {
            string message = eventArgs.ExceptionObject?.ToString() ?? "Unknown unhandled exception.";
            try
            {
                if (Logger.Instance?.Log != null)
                {
                    Log.Error(message);
                }
                else
                {
                    Debug.LogError(message);
                }
            }
            catch (Exception)
            {
                Debug.LogError(message);
            }
        }

        private static void LogWarningFallback(string message)
        {
            try
            {
                if (Logger.Instance?.Log != null)
                {
                    Log.Warning(message);
                }
                else
                {
                    Debug.LogWarning(message);
                }
            }
            catch (Exception)
            {
                Debug.LogWarning(message);
            }
        }

        private bool IsCurrentInstance()
        {
            return !this.m_Destroyed && ReferenceEquals(Instance, this) && this != null;
        }

        private void SubscribeUnhandledException()
        {
            if (!this.m_UnhandledExceptionSubscribed &&
                Logger.Instance?.Log != null &&
                this.IsCurrentInstance())
            {
                AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
                this.m_UnhandledExceptionSubscribed = true;
            }
        }

        private void UnsubscribeUnhandledException()
        {
            if (!this.m_UnhandledExceptionSubscribed)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
            this.m_UnhandledExceptionSubscribed = false;
        }

        private void ShutdownRuntime()
        {
            if (this.m_RuntimeShutdown || !ReferenceEquals(Instance, this))
            {
                return;
            }

            this.m_RuntimeShutdown = true;
            this.UnsubscribeUnhandledException();

            // ET UIComponent 是 FairyGUI 窗体的 owner:先让 owner 释放窗体,
            // 再关闭管理器和 package,避免 owner 清理访问已失效的 UI manager。
            TryShutdown(() => EventSystem.Instance?.Invoke(new OnShutdown()), "OnShutdown");
            TryShutdown(() =>
            {
                if (this.m_WorldOwned)
                {
                    World.Instance.Dispose();
                }
            }, "World.Dispose");
            TryShutdown(() => FairyUIManager.Instance.Shutdown(), "FairyUIManager.Shutdown");
            FairyUIManager.NotifyETRuntimeShutdownCompleted();
        }

        private static void TryShutdown(Action shutdown, string stage)
        {
            try
            {
                shutdown();
            }
            catch (Exception exception)
            {
                string message = $"ET Runner {stage} failed during shutdown: {exception}";
                try
                {
                    if (Logger.Instance?.Log != null)
                    {
                        Log.Error(message);
                    }
                    else
                    {
                        Debug.LogError(message);
                    }
                }
                catch (Exception)
                {
                    Debug.LogError(message);
                }
            }
        }
    }
}
