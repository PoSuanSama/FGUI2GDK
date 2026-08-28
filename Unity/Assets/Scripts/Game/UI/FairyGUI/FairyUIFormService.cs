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

        /// <summary>
        /// per-open Presenter 工厂重载:ET 的 Component/System 打开链用
        /// UIComponentSystem 预先创建的 Component + Adapter 覆盖注册表工厂。
        /// GameHot 与旧类 Presenter 路径不受影响。
        /// </summary>
        public static UniTask<FairyUIForm> OpenFairyUIFormAsync(
            int uiId,
            object userData,
            System.Func<FairyUIFormDescriptor, IFairyUIPresenter> presenterFactory,
            CancellationToken ownerToken = default)
        {
            return FairyUIManager.Instance.OpenFairyUIFormAsync(uiId, userData, ownerToken, presenterFactory);
        }

        /// <summary>
        /// 不依赖 ET 类型的 per-open Presenter 工厂门面:ET 打开链把"创建 Component/System 路径
        /// 的 Presenter(Adapter)"封装成 System.Func 传入 FairyUIManager,本类不引用 ET。
        /// 工厂返回 null 时 FairyUIManager 回退到 FairyUIPresenterRegistry 的类 Presenter。
        /// 打开失败时调用 <see cref="Dispose"/> 清理已创建的 per-open Presenter。
        /// </summary>
        public sealed class PresenterFactory
        {
            private readonly System.Func<IFairyUIPresenter> m_Create;
            private readonly System.Action m_Dispose;
            private IFairyUIPresenter m_Created;

            public PresenterFactory(
                System.Func<IFairyUIPresenter> create,
                System.Action dispose)
            {
                m_Create = create;
                m_Dispose = dispose;
            }

            public IFairyUIPresenter Create(FairyUIFormDescriptor descriptor)
            {
                m_Created = m_Create?.Invoke();
                return m_Created;
            }

            /// <summary>
            /// 打开成功后调用:把已创建的 Presenter 标记为已消费,后续 Dispose 不再清理。
            /// </summary>
            public void Consume()
            {
                m_Created = null;
            }

            /// <summary>
            /// 幂等清理:创建了但未被成功打开的 per-open Presenter(打开失败/取消路径)。
            /// </summary>
            public void Dispose()
            {
                if (m_Created == null)
                {
                    return;
                }

                m_Created = null;
                m_Dispose?.Invoke();
            }
        }
    }
}