using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;

namespace ET.Client
{
    [FriendOf(typeof(UIComponent))]
    [EntitySystemOf(typeof(UIComponent))]
    public static partial class UIComponentSystem
    {
        [EntitySystem]
        private static void Awake(this UIComponent self)
        {
            UIComponentFairyUIBridge.Open = OpenFairyUIFormAsync;
            UIComponentFairyUIBridge.Close = CloseFairyUIForm;
            UIComponentFairyUIBridge.Refocus = RefocusFairyUIForm;
            self.PendingFairyUIOpens = new Dictionary<long, CancellationTokenSource>();
            self.OwnedFairyUIForms = new Dictionary<int, CancellationTokenSource>();
            self.NextFairyUIOpenOperationId = 0;
        }

        [EntitySystem]
        private static void Destroy(this UIComponent self)
        {
            Exception firstException = null;
            List<CancellationTokenSource> pendingOpens = self.PendingFairyUIOpens == null
                ? new List<CancellationTokenSource>()
                : new List<CancellationTokenSource>(self.PendingFairyUIOpens.Values);
            foreach (CancellationTokenSource cancellation in pendingOpens)
            {
                TryCleanup(() => Cancel(cancellation), ref firstException);
            }

            self.PendingFairyUIOpens?.Clear();
            foreach (CancellationTokenSource cancellation in pendingOpens)
            {
                TryCleanup(cancellation.Dispose, ref firstException);
            }

            List<KeyValuePair<int, CancellationTokenSource>> ownedForms =
                self.OwnedFairyUIForms == null
                    ? new List<KeyValuePair<int, CancellationTokenSource>>()
                    : new List<KeyValuePair<int, CancellationTokenSource>>(self.OwnedFairyUIForms);
            foreach (KeyValuePair<int, CancellationTokenSource> ownedForm in ownedForms)
            {
                TryCleanup(() => Cancel(ownedForm.Value), ref firstException);
                TryCleanup(() => CloseBySerialId(ownedForm.Key), ref firstException);
                TryCleanup(ownedForm.Value.Dispose, ref firstException);
            }

            self.OwnedFairyUIForms?.Clear();
            self.PendingFairyUIOpens = null;
            self.OwnedFairyUIForms = null;
            self.NextFairyUIOpenOperationId = 0;

            if (firstException != null)
            {
                Log.Error(firstException);
            }
        }

        public static UniTask<FairyUIForm> OpenFairyUIFormAsync(
            this UIComponent self,
            int uiId,
            object userData = null)
        {
            return OpenFairyUIFormCoreAsync(self, uiId, userData, null);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor 生命周期回归专用入口。钩子在业务 OnViewReady 完成后、GF serial 分配前
        /// 同步执行,且仅绑定本次打开,用于稳定验证 owner 销毁竞态。
        /// </summary>
        public static UniTask<FairyUIForm> OpenFairyUIFormAfterViewReadyForTestingAsync(
            this UIComponent self,
            int uiId,
            object userData,
            Action<FairyUIFormComponent> afterViewReadyForTesting)
        {
            if (afterViewReadyForTesting == null)
            {
                throw new ArgumentNullException(nameof(afterViewReadyForTesting));
            }

            return OpenFairyUIFormCoreAsync(self, uiId, userData, afterViewReadyForTesting);
        }
#endif

        private static async UniTask<FairyUIForm> OpenFairyUIFormCoreAsync(
            UIComponent self,
            int uiId,
            object userData,
            Action<FairyUIFormComponent> afterViewReadyForTesting)
        {
            if (self == null || self.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(UIComponent));
            }

            CancellationTokenSource ownerCancellation = new CancellationTokenSource();
            CancellationToken ownerToken = ownerCancellation.Token;
            long operationId = ++self.NextFairyUIOpenOperationId;
            self.PendingFairyUIOpens.Add(operationId, ownerCancellation);

            EntityRef<UIComponent> ownerRef = self;
            FairyUIForm openedForm = null;
            bool ownershipTransferred = false;
            FairyUIFormService.PresenterFactory presenterFactory =
                new FairyUIFormService.PresenterFactory(
                    () => CreateComponentPresenter(self, ownerRef, uiId, afterViewReadyForTesting),
                    presenter => (presenter as FairyUIPresenterAdapter)?.Component.Dispose());
            try
            {
                openedForm = await FairyUIFormService.OpenFairyUIFormAsync(
                    uiId, userData, presenterFactory.Create, ownerToken);
                presenterFactory.Consume();

                UIComponent currentOwner = ownerRef;
                if (currentOwner == null ||
                    currentOwner.PendingFairyUIOpens == null ||
                    !currentOwner.PendingFairyUIOpens.Remove(operationId))
                {
                    CloseBySerialId(openedForm.SerialId);
                    throw new OperationCanceledException(
                        "The ET UI owner was destroyed while opening a FairyGUI form.",
                        ownerToken);
                }

                if (!currentOwner.OwnedFairyUIForms.TryAdd(openedForm.SerialId, ownerCancellation))
                {
                    CloseBySerialId(openedForm.SerialId);
                    throw new InvalidOperationException(
                        $"ET UI owner already tracks FairyGUI serial '{openedForm.SerialId}'.");
                }

                ownershipTransferred = true;
                return openedForm;
            }
            catch
            {
                if (openedForm != null)
                {
                    CloseBySerialId(openedForm.SerialId);
                }

                throw;
            }
            finally
            {
                if (!ownershipTransferred)
                {
                    UIComponent currentOwner = ownerRef;
                    currentOwner?.PendingFairyUIOpens?.Remove(operationId);
                    ownerCancellation.Dispose();
                    presenterFactory.Dispose();
                }
            }
        }

        public static bool CloseFairyUIForm(this UIComponent self, int serialId)
        {
            if (self?.OwnedFairyUIForms == null ||
                !self.OwnedFairyUIForms.Remove(serialId, out CancellationTokenSource ownerCancellation))
            {
                return false;
            }

            Exception firstException = null;
            TryCleanup(() => Cancel(ownerCancellation), ref firstException);
            TryCleanup(() => CloseBySerialId(serialId), ref firstException);
            TryCleanup(ownerCancellation.Dispose, ref firstException);
            if (firstException != null)
            {
                throw firstException;
            }

            return true;
        }

        public static bool RefocusFairyUIForm(this UIComponent self, int serialId, object userData = null)
        {
            if (!self.OwnsFairyUIForm(serialId))
            {
                return false;
            }

            FairyUIForm form = FairyUIManager.Instance.GetUIForm(serialId);
            if (form == null)
            {
                return false;
            }

            FairyUIManager.Instance.RefocusUIForm(form, userData);
            return true;
        }

        public static bool OwnsFairyUIForm(this UIComponent self, int serialId)
        {
            return self?.OwnedFairyUIForms != null && self.OwnedFairyUIForms.ContainsKey(serialId);
        }

        public static int GetOwnedFairyUIFormCount(this UIComponent self)
        {
            return self?.OwnedFairyUIForms?.Count ?? 0;
        }

        public static int GetPendingFairyUIOpenCount(this UIComponent self)
        {
            return self?.PendingFairyUIOpens?.Count ?? 0;
        }

        internal static void CloseFairyUIFormBeforeComponentDestroy<T>(
            T component,
            Action<T> closeComponent)
            where T : FairyUIFormComponent
        {
            if (component == null)
            {
                return;
            }

            FairyUIFormContext context = component.Context;
            bool viewReady = context != null;
            FairyUIForm form = component.FairyForm ?? context?.Form;
            Exception firstException = null;
            // ET 已先把 child 标记为 disposed，通用 dispatcher 会跳过；这里直接回滚
            // OnViewReady 已建立的订阅，即使 GF 尚未分配 form serial。
            TryCleanup(() => context?.CancelLifetime(), ref firstException);
            if (viewReady)
            {
                TryCleanup(() => closeComponent(component), ref firstException);
            }

            component.FairyForm = null;
            component.Context = null;
            component.View = null;
            component.UserData = null;
            component.IsShutdown = false;

            if (form != null)
            {
                TryCleanup(() => CloseBySerialId(form.SerialId), ref firstException);
            }

            if (firstException != null)
            {
                throw firstException;
            }
        }

        private static IFairyUIPresenter CreateComponentPresenter(
            UIComponent self,
            EntityRef<UIComponent> ownerRef,
            int uiId,
            Action<FairyUIFormComponent> afterViewReadyForTesting)
        {
            // 未命中 Component/System 注册表时回退到类 Presenter 注册表(返回 null)。
            if (!FairyUIFormComponentRegistry.TryGet(uiId, out Func<UIComponent, FairyUIFormComponent> factory))
            {
                return null;
            }

            UIComponent currentOwner = ownerRef;
            if (currentOwner == null || currentOwner.IsDisposed)
            {
                throw new OperationCanceledException(
                    "The ET UI owner was destroyed before creating the FairyGUI component presenter.");
            }

            FairyUIFormComponent component = factory(currentOwner);
#if UNITY_EDITOR
            if (afterViewReadyForTesting != null)
            {
                return new FairyUIPresenterAdapter(component, afterViewReadyForTesting);
            }
#endif
            return new FairyUIPresenterAdapter(component);
        }

        private static void Cancel(CancellationTokenSource cancellation)
        {
            if (cancellation != null && !cancellation.IsCancellationRequested)
            {
                cancellation.Cancel();
            }
        }

        private static void CloseBySerialId(int serialId)
        {
            FairyUIManager uiManager = FairyUIManager.Instance;
            if (uiManager.HasUIForm(serialId) || uiManager.IsLoadingUIForm(serialId))
            {
                uiManager.CloseUIForm(serialId);
            }
        }

        private static void TryCleanup(Action cleanup, ref Exception firstException)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                firstException ??= exception;
            }
        }
    }
}
