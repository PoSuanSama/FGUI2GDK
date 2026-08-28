using System;
using System.Reflection;
using AgentBridge;
using Cysharp.Threading.Tasks;
using ET.Client;
using UnityEditor;

namespace ET
{
    /// <summary>
    /// FairyGUI Component/System dispatcher 骨架自检(非 PlayMode):
    /// 验证 ModelView 状态组件 + HotfixView 静态 System 经 EntitySystemSingleton
    /// 注册后,能被 FairyUIFormSystemDispatcher 按类型正确派发。
    ///
    /// EditMode 下 CodeTypes/EntitySystemSingleton 尚未由 CodeLoader 初始化,
    /// 自检先做最小隔离初始化,结束后恢复基线。
    ///
    /// Entity.IsDisposed == (InstanceId == 0):直接 new 的 Entity 没有合法 InstanceId,
    /// 会被派发守卫当作已销毁;EditMode 又没有 Fiber/Scene 运行时供正常注册链赋值,
    /// 因此测试专用地用反射给 InstanceId 赋非零值。派发自检只依赖 InstanceId 与
    /// TypeSystems,不依赖 Fiber 注册。PlayMode 场景由 ET 冒烟覆盖真实注册链。
    /// </summary>
    public static class FairyUIFormSkeletonSelfCheck
    {
        [AgentCallable("FairyGUI 骨架自检:HotfixView System 注册与 ModelView 派发闭环。", 30)]
        public static async UniTask RunFairyUIFormSkeletonSelfCheck()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyUIForm skeleton self-check requires EditMode.");
            }

            bool initializedHere = false;
            if (CodeTypes.Instance == null)
            {
                World.Instance.AddSingleton<CodeTypes, Assembly[]>(new[]
                {
                    typeof(World).Assembly,
                    typeof(FairyUIFormComponent).Assembly,
                    typeof(FairyDemoFormComponentSystem).Assembly
                });
                initializedHere = true;
            }

            if (Logger.Instance == null)
            {
                // EditMode 下 Logger 未初始化时,HotfixView 行为的 Log.Error 与
                // dispatcher 的异常隔离都会空引用,先给最小实现。
                // UnityLogger 在 Game.ET.Loader,Editor asmdef 未引用,此处用局部实现。
                World.Instance.AddSingleton<Logger, ILog>(new SelfCheckLogger());
            }

            if (EntitySystemSingleton.Instance == null)
            {
                World.Instance.AddSingleton<EntitySystemSingleton>();
            }

            FairyDemoFormComponent component = new FairyDemoFormComponent();
            try
            {
                SetInstanceId(component, 12345L);

                // 派发前无 View:HotfixView 行为应记录错误但不抛异常(派发层异常隔离)。
                FairyUIFormSystemDispatcher.FairyUIFormOnOpen(component);
                await UniTask.Yield();

                if (component.OnOpenCount != 1)
                {
                    throw new InvalidOperationException(
                        $"FairyDemoFormComponentSystem OnOpen was not dispatched, count={component.OnOpenCount}.");
                }

                if (component.OnCloseCount != 0)
                {
                    throw new InvalidOperationException(
                        $"Unexpected OnClose dispatch before close, count={component.OnCloseCount}.");
                }

                FairyUIFormSystemDispatcher.FairyUIFormOnClose(component);
                if (component.OnCloseCount != 1)
                {
                    throw new InvalidOperationException(
                        $"FairyDemoFormComponentSystem OnClose was not dispatched, count={component.OnCloseCount}.");
                }

                // 非生命周期接口(System 未实现)的派发必须安全无副作用。
                FairyUIFormSystemDispatcher.FairyUIFormOnPause(component);
                FairyUIFormSystemDispatcher.FairyUIFormOnUpdate(component, 0.016f, 0.016f);
            }
            finally
            {
                // 自检实体未注册 Fiber/对象池;正常 Dispose 会走到
                // ObjectPool.Instance.Recycle,而 EditMode 没有对象池,会空引用。
                // 它不在任何注册表里,把 InstanceId 归零(IsDisposed 语义)后由 GC 回收。
                SetInstanceId(component, 0);
                if (initializedHere)
                {
                    World.Instance.Dispose();
                }
            }
        }

        private static void SetInstanceId(Entity entity, long instanceId)
        {
            PropertyInfo instanceIdProperty = typeof(Entity).GetProperty(
                "InstanceId",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            instanceIdProperty.GetSetMethod(true).Invoke(entity, new object[] { instanceId });
        }

        private sealed class SelfCheckLogger : ILog
        {
            public void Trace(string message) { }
            public void Warning(string message) => UnityEngine.Debug.LogWarning(message);
            public void Info(string message) { }
            public void Debug(string message) { }
            public void Error(string message) => UnityEngine.Debug.LogError(message);
            public void Error(Exception e) => UnityEngine.Debug.LogException(e);
        }
    }
}
