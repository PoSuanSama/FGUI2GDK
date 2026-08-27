using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game
{
    /// <summary>
    /// 运行时入口的门面：所有 FairyGUI 界面统一经 <see cref="FairyUIManager"/> 打开。
    /// </summary>
    public static class FairyUIFormService
    {
        public static UniTask<FairyUIForm> OpenFairyUIFormAsync(
            int uiId,
            object userData = null,
            CancellationToken ownerToken = default)
        {
            return FairyUIManager.Instance.OpenFairyUIFormAsync(uiId, userData, ownerToken);
        }
    }
}