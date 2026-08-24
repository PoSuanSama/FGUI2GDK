using System.Net;
using Cysharp.Threading.Tasks;

namespace ET.Server
{
    [Invoke((long)SceneType.Router)]
    public class FiberInit_Router: AInvokeHandler<FiberInit, UniTask>
    {
        public override async UniTask Handle(FiberInit fiberInit)
        {
            Scene root = fiberInit.Fiber.Root;
            var startSceneConfig = Tables.Instance.DTStartSceneConfig.Get(Options.Instance.StartConfig, (int)root.Id);
            
            // 外部地址用于公告给客户端，监听地址必须覆盖容器网卡，不能绑定到容器回环地址。
            IPEndPoint bindAddress = new(IPAddress.Any, startSceneConfig.OuterIPPort.Port);
            root.AddComponent<RouterComponent, IPEndPoint, string>(bindAddress, startSceneConfig.StartProcessConfig.InnerIP);
            Log.Console($"Router create: {root.Fiber.Id}");
            await UniTask.CompletedTask;
        }
    }
}
