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
            private void Update()
            {
                TimeInfo.Instance.Update();
                FiberManager.Instance.Update();
            }

            private void LateUpdate()
            {
                FiberManager.Instance.LateUpdate();
            }

            private void OnDestroy()
            {
                EventSystem.Instance.Invoke(new OnShutdown());
                World.Instance.Dispose();
            }

            private void OnApplicationPause(bool pauseStatus)
            {
                EventSystem.Instance.Invoke(new OnApplicationPause(pauseStatus));
            }

            private void OnApplicationFocus(bool hasFocus)
            {
                EventSystem.Instance.Invoke(new OnApplicationFocus(hasFocus));
            }
        }

        private Runner m_RunnerComponent;

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
            if (this.m_RunnerComponent != null)
            {
                Runner runner = this.m_RunnerComponent;
                this.m_RunnerComponent = null;
                DestroyImmediate(runner);
            }
        }

        private async UniTaskVoid StartAsync()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Log.Error(e.ExceptionObject.ToString());
            };

            // GameEntry 的 Awake 先于场景对象 Start:GF 组件在 GameEntry.Start 后才可用。
            // ET 与 GameHot 并存时(Standalone 双符号冒烟),这里的 Start 可能在 GameEntry.Start
            // 之前执行,直接访问 GameEntry.CodeRunner 会空引用。有界等待 GameEntry 就绪,
            // 失败时记录诊断而不是抛出(启动链不应被自身初始化顺序打断)。
            int waitFrames = 0;
            while (waitFrames < 120 &&
                   (GameEntry.Base == null || GameEntry.CodeRunner == null))
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                waitFrames++;
            }

            if (GameEntry.Base == null || GameEntry.CodeRunner == null)
            {
                Log.Error("ET Init: GameEntry components are not ready within 120 frames; continue with degraded init.");
            }

            // Awaitable 扩展要求先订阅 GF 事件;双符号模式下 GameHot 的 ProcedureLaunch
            // 可能晚于 ET 初始化执行,这里按幂等调用,避免重复订阅 handler。
            if (!UnityGameFramework.Extension.Awaitable.IsValid)
            {
                UnityGameFramework.Extension.Awaitable.SubscribeEvent();
            }

            // 命令行参数
            string[] args = "".Split(" ");
            Parser.Default.ParseArguments<Options>(args)
                    .WithNotParsed(error => throw new Exception($"命令行格式错误! {error}"))
                    .WithParsed((o) => World.Instance.AddSingleton(o));
            Options.Instance.StartConfig = "Localhost";

            World.Instance.AddSingleton<Logger, ILog>(new UnityLogger());
            World.Instance.AddSingleton<TimeInfo, ITimeNow>(new UnityTimeNow());
            World.Instance.AddSingleton<FiberManager>();
            World.Instance.AddSingleton<ConfigComponent, IConfigReader>(new ConfigReader());
            World.Instance.AddSingleton<CodeLoaderComponent, ICodeLoader>(new CodeLoader());

            await CodeLoaderComponent.Instance.StartAsync();
            this.m_RunnerComponent = this.gameObject.AddComponent<Runner>();
        }
    }
}