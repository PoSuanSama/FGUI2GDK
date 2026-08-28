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

        public static async UniTask<FairyUIForm> OpenFairyUIFormAsync(
            this UIComponent self,
            int uiId,
            object userData = null)
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
            try
            {
                openedForm = await FairyUIFormService.OpenFairyUIFormAsync(uiId, userData, ownerToken);

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
