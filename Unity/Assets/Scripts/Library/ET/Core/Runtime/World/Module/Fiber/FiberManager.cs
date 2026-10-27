using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace ET
{
    public enum SchedulerType
    {
        Main,
        Thread,
        ThreadPool,
    }
    
    public class FiberManager: Singleton<FiberManager>, ISingletonAwake, ISingletonReverseDispose
    {
        private readonly IScheduler[] schedulers = new IScheduler[3];
        
        private int idGenerator = 10000000; // 10000000以下为保留的用于StartSceneConfig的fiber id, 1个区配置1000个纤程，可以配置10000个区
        private ConcurrentDictionary<int, Fiber> fibers = new();

        private MainThreadScheduler mainThreadScheduler;
        
        public void Awake()
        {
            this.mainThreadScheduler = new MainThreadScheduler(this);
            this.schedulers[(int)SchedulerType.Main] = this.mainThreadScheduler;
            
#if UNITY_EDITOR
            this.schedulers[(int)SchedulerType.Thread] = this.mainThreadScheduler;
            this.schedulers[(int)SchedulerType.ThreadPool] = this.mainThreadScheduler;
#else
            this.schedulers[(int)SchedulerType.Thread] = new ThreadScheduler(this);
            this.schedulers[(int)SchedulerType.ThreadPool] = new ThreadPoolScheduler(this);
#endif
        }
        
        public void Update()
        {
            this.mainThreadScheduler.Update();
        }

        public void LateUpdate()
        {
            this.mainThreadScheduler.LateUpdate();
        }

        protected override void Destroy()
        {
            foreach (IScheduler scheduler in this.schedulers)
            {
                scheduler.Dispose();
            }
            
            foreach (var kv in this.fibers)
            {
                kv.Value.Dispose();
            }

            this.fibers = null;
        }

        public async UniTask<int> Create(SchedulerType schedulerType, int fiberId, int zone, SceneType sceneType, string name)
        {
            // GDK vendor 补丁: fiber 提到 try 外,失败时才能回滚已登记的残留实例。
            Fiber fiber = null;
            try
            {
                fiber = new(fiberId, zone, sceneType, name);

                if (!this.fibers.TryAdd(fiberId, fiber))
                {
                    throw new Exception($"same fiber already existed, if you remove, please await Remove then Create fiber! {fiberId}");
                }
                this.schedulers[(int) schedulerType].Add(fiberId);
                
                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                
                fiber.ThreadSynchronizationContext.Post(() =>
                {
                    async UniTaskVoid RunAsync()
                    {
                        try
                        {
                            // 根据Fiber的SceneType分发Init,必须在Fiber线程中执行
                            await EventSystem.Instance.Invoke<FiberInit, UniTask>((long)sceneType, new FiberInit() {Fiber = fiber});
                            tcs.TrySetResult(true);
                        }
                        catch (Exception e)
                        {
                            // GDK vendor 补丁: 原实现只记日志不完成 tcs,导致下面 await tcs.Task 永久挂起,
                            // 启动直接卡死且无明确报错。Init 失败必须让等待方看到异常。
                            Log.Error($"init fiber fail: {sceneType} {e}");
                            tcs.TrySetException(e);
                        }
                    }
                    RunAsync().Forget();
                });

                await tcs.Task;
                return fiberId;
            }
            catch (Exception e)
            {
                // GDK vendor 补丁: 初始化失败的 fiber 已进 fibers 与 scheduler,不回滚会留下半初始化实例,
                // 同 id 重试会撞 "same fiber already existed"。scheduler 无 Remove 接口,
                // 其 id 残留属上游设计,不影响正确性。
                if (fiber != null && this.fibers.TryRemove(fiberId, out Fiber removed))
                {
                    try
                    {
                        // 在 fiber 线程释放,避免与刚失败的 Init 竞争。
                        removed.ThreadSynchronizationContext.Post(() =>
                        {
                            try
                            {
                                removed.Dispose();
                            }
                            catch (Exception disposeError)
                            {
                                Log.Error($"dispose failed fiber fail: {fiberId} {disposeError}");
                            }
                        });
                    }
                    catch (Exception postError)
                    {
                        Log.Error($"post dispose failed fiber fail: {fiberId} {postError}");
                    }
                }

                throw new Exception($"create fiber error: {fiberId} {sceneType}", e);
            }
        }
        
        public async UniTask<int> Create(SchedulerType schedulerType, int zone, SceneType sceneType, string name)
        {
            int fiberId = Interlocked.Increment(ref this.idGenerator);
            return await this.Create(schedulerType, fiberId, zone, sceneType, name);
        }
        
        public async UniTask Remove(int id)
        {
            Fiber fiber = this.Get(id);
            // GDK vendor 补丁: fiber 不存在时原实现直接访问 ThreadSynchronizationContext 抛 NRE,
            // 且 tcs 永不完成导致 await 挂起。移除不存在的 fiber 应视为幂等成功。
            if (fiber == null)
            {
                return;
            }

            TaskCompletionSource<bool> tcs = new();
            // 要扔到fiber线程执行，否则会出现线程竞争
            fiber.ThreadSynchronizationContext.Post(() =>
            {
                try
                {
                    if (this.fibers.Remove(id, out Fiber f))
                    {
                        f.Dispose();
                    }
                    tcs.TrySetResult(true);
                }
                catch (Exception e)
                {
                    // GDK vendor 补丁: Dispose 抛异常时原实现会跳过 SetResult,导致 await 永久挂起。
                    Log.Error($"remove fiber fail: {id} {e}");
                    tcs.TrySetException(e);
                }
            });
            await tcs.Task;
        }

        // 不允许外部调用，容易出现多线程问题, 只能通过消息通信，不允许直接获取其它Fiber引用
        internal Fiber Get(int id)
        {
            this.fibers.TryGetValue(id, out Fiber fiber);
            return fiber;
        }

        public int Count()
        {
            return this.fibers.Count;
        }
    }
}