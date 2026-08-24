using System.Net;
using Cysharp.Threading.Tasks;

namespace ET.Server
{
    [Invoke((long)SceneType.Realm)]
    public class FiberInit_Realm: AInvokeHandler<FiberInit, UniTask>
    {
        public override async UniTask Handle(FiberInit fiberInit)
        {
            Scene root = fiberInit.Fiber.Root;
            root.AddComponent<MailBoxComponent, MailBoxType>(MailBoxType.UnOrderedMessage);
            root.AddComponent<TimerComponent>();
            root.AddComponent<CoroutineLockComponent>();
            root.AddComponent<ProcessInnerSender>();
            root.AddComponent<MessageSender>();
            var startSceneConfig = Tables.Instance.DTStartSceneConfig.Get(Options.Instance.StartConfig, root.Fiber.Id);
            IPEndPoint bindAddress = new(IPAddress.Any, startSceneConfig.InnerIPPort.Port);
            root.AddComponent<NetComponent, IPEndPoint, NetworkProtocol>(bindAddress, NetworkProtocol.UDP);

            await UniTask.CompletedTask;
        }
    }
}
