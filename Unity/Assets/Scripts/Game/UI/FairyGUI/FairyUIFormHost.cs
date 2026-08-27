using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FairyGUI;
using GameFramework;
using GameFramework.UI;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Game
{
    /// <summary>
    /// Lightweight pooled host for a FairyGUI UIForm.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FairyUIFormHost : MonoBehaviour
    {
        internal string DescriptorKey;
    }

    /// <summary>
    /// Owns every resource prepared before entering GF's fallible open boundary.
    /// </summary>
    internal sealed class FairyUIFormPreparedState : IDisposable
    {
        private FairyPackageLease m_PackageLease;
        private FairyUIGroupContainer m_Group;
        private GComponent m_View;
        private IFairyUIPresenter m_Presenter;
        private CancellationTokenRegistration m_OwnerCancellation;
        private bool m_PresenterReady;
        private bool m_PresenterOpened;
        private bool m_Adopted;
        private bool m_Disposed;

        internal FairyUIFormPreparedState(
            string descriptorKey,
            FairyUIFormDescriptor descriptor,
            FairyPackageLease packageLease,
            GComponent view,
            IFairyUIPresenter presenter,
            object userData)
        {
            DescriptorKey = descriptorKey;
            Descriptor = descriptor;
            m_PackageLease = packageLease;
            m_View = view;
            m_Presenter = presenter;
            UserData = userData;
        }

        internal string DescriptorKey { get; }
        internal FairyUIFormDescriptor Descriptor { get; }
        internal object UserData { get; }
        internal FairyPackageLease PackageLease => m_PackageLease;
        internal GComponent View => m_View;
        internal IFairyUIPresenter Presenter => m_Presenter;
        internal bool IsDisposed => m_Disposed;
        internal bool IsAdopted => m_Adopted;
        internal bool IsPresenterOpened => m_PresenterOpened;

        internal void MarkPresenterReady()
        {
            m_PresenterReady = true;
        }

        internal void MarkPresenterOpened()
        {
            m_PresenterOpened = true;
        }

        internal void Adopt(IUIGroup uiGroup)
        {
            if (m_Disposed || m_View == null || m_Presenter == null)
            {
                throw new GameFrameworkException(
                    $"FairyGUI prepared state '{DescriptorKey}' is no longer valid.");
            }

            if (m_Adopted)
            {
                throw new GameFrameworkException(
                    $"FairyGUI prepared state '{DescriptorKey}' was already adopted.");
            }

            m_Group = FairyUIRootService.Instance.GetOrCreateGroup(uiGroup);
            m_Group.AddForm(m_View, 0);
            m_View.visible = false;
            m_View.touchable = false;
            m_Adopted = true;
        }

        internal void SetVisible(bool visible)
        {
            if (m_View == null ||
                m_View.isDisposed ||
                m_View.displayObject == null ||
                m_View.displayObject.isDisposed)
            {
                return;
            }

            m_View.visible = visible;
            m_View.touchable = visible;
        }

        internal void SetDepth(int uiGroupDepth, int depthInUIGroup)
        {
            m_Group?.SetDepth(uiGroupDepth);
            m_Group?.SetFormDepth(m_View, depthInUIGroup);
        }

        internal void ObserveOwnerCancellation(UIForm uiForm, CancellationToken ownerToken)
        {
            m_OwnerCancellation.Dispose();
            if (!ownerToken.CanBeCanceled || m_Disposed)
            {
                return;
            }

            int serialId = uiForm.SerialId;
            m_OwnerCancellation = ownerToken.Register(() => CloseOwnedFormAsync(serialId).Forget());
        }

        internal void Release(bool isShutdown, object userData)
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            Exception firstException = null;
            TryCleanup(() => m_OwnerCancellation.Dispose(), ref firstException);

            IFairyUIPresenter presenter = m_Presenter;
            m_Presenter = null;
            if (presenter != null && (m_PresenterOpened || m_PresenterReady))
            {
                TryCleanup(() => presenter.OnClose(isShutdown, userData), ref firstException);
            }

            GComponent view = m_View;
            m_View = null;
            FairyUIGroupContainer group = m_Group;
            m_Group = null;
            if (view != null)
            {
                TryCleanup(() => group?.RemoveForm(view), ref firstException);
                TryCleanup(view.Dispose, ref firstException);
            }

            if (group != null && group.IsEmpty)
            {
                TryCleanup(() => FairyUIRootService.Instance.TryReleaseGroup(group.Name), ref firstException);
            }

            FairyPackageLease packageLease = m_PackageLease;
            m_PackageLease = null;
            TryCleanup(() => packageLease?.Dispose(), ref firstException);
            m_Adopted = false;

            if (firstException != null)
            {
                throw firstException;
            }
        }

        public void Dispose()
        {
            Release(false, UserData);
        }

        private static async UniTaskVoid CloseOwnedFormAsync(int serialId)
        {
            await UniTask.SwitchToMainThread();
            try
            {
                if (GameEntry.UI.HasUIForm(serialId) || GameEntry.UI.IsLoadingUIForm(serialId))
                {
                    GameEntry.UI.CloseUIForm(serialId);
                }
            }
            catch (Exception exception)
            {
                Log.Error("Failed to close owner-cancelled FairyGUI UI form '{0}': {1}", serialId, exception);
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

    /// <summary>
    /// Transfers prepared state to the GF helper without replacing the caller's userData.
    /// </summary>
    internal static class FairyUIFormPreparedRegistry
    {
        private static readonly object s_Gate = new object();
        private static readonly Dictionary<int, FairyUIFormPreparedState> s_StatesBySerialId =
            new Dictionary<int, FairyUIFormPreparedState>();
        private static readonly HashSet<FairyUIFormPreparedState> s_OwnedStates =
            new HashSet<FairyUIFormPreparedState>();
        private static FairyUIFormPreparedState s_SynchronousOpenState;

        internal static IDisposable BeginOpen(FairyUIFormPreparedState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            lock (s_Gate)
            {
                if (s_SynchronousOpenState != null)
                {
                    throw new GameFrameworkException(
                        "A FairyGUI prepared-state handoff is already entering GF's synchronous open boundary.");
                }

                if (!s_OwnedStates.Add(state))
                {
                    throw new GameFrameworkException(
                        $"FairyGUI prepared state '{state.DescriptorKey}' is already registered.");
                }

                s_SynchronousOpenState = state;
                return new SynchronousOpenScope(state);
            }
        }

        internal static void BindSerialId(int serialId, FairyUIFormPreparedState state)
        {
            lock (s_Gate)
            {
                if (!s_OwnedStates.Contains(state))
                {
                    return;
                }

                if (s_StatesBySerialId.ContainsKey(serialId))
                {
                    throw new GameFrameworkException(
                        $"GF serial ID '{serialId}' already has a FairyGUI prepared state.");
                }

                s_StatesBySerialId.Add(serialId, state);
            }
        }

        internal static FairyUIFormPreparedState ConsumeNewInstance(
            int serialId,
            string descriptorKey,
            object userData)
        {
            lock (s_Gate)
            {
                FairyUIFormPreparedState state = null;
                if (s_StatesBySerialId.TryGetValue(serialId, out FairyUIFormPreparedState boundState))
                {
                    state = boundState;
                    s_StatesBySerialId.Remove(serialId);
                }
                else if (s_SynchronousOpenState != null)
                {
                    // Resource callbacks are normally asynchronous. This fallback also makes the
                    // handoff correct if an active resource provider completes inside OpenUIForm.
                    state = s_SynchronousOpenState;
                }

                return ConsumeOwnedState(state, descriptorKey, userData, serialId);
            }
        }

        internal static FairyUIFormPreparedState ConsumePooledInstance(
            string descriptorKey,
            object userData)
        {
            lock (s_Gate)
            {
                return ConsumeOwnedState(
                    s_SynchronousOpenState,
                    descriptorKey,
                    userData,
                    serialId: 0);
            }
        }

        internal static bool TryRemove(FairyUIFormPreparedState state)
        {
            if (state == null)
            {
                return false;
            }

            lock (s_Gate)
            {
                if (!s_OwnedStates.Remove(state))
                {
                    return false;
                }

                RemoveSerialBinding(state);
                return true;
            }
        }

        private static FairyUIFormPreparedState ConsumeOwnedState(
            FairyUIFormPreparedState state,
            string descriptorKey,
            object userData,
            int serialId)
        {
            if (state == null)
            {
                string message = serialId == 0
                    ? Utility.Text.Format(
                        "No prepared FairyGUI state is registered for descriptor '{0}'.",
                        descriptorKey)
                    : Utility.Text.Format(
                        "No prepared FairyGUI state is registered for descriptor '{0}' and GF serial ID '{1}'.",
                        descriptorKey,
                        serialId);
                throw new GameFrameworkException(message);
            }

            if (!s_OwnedStates.Contains(state) ||
                !string.Equals(state.DescriptorKey, descriptorKey, StringComparison.Ordinal) ||
                !ReferenceEquals(state.UserData, userData))
            {
                throw new GameFrameworkException(
                    $"Prepared FairyGUI state '{descriptorKey}' does not match the GF open request.");
            }

            s_OwnedStates.Remove(state);
            RemoveSerialBinding(state);
            return state;
        }

        private static void RemoveSerialBinding(FairyUIFormPreparedState state)
        {
            int serialIdToRemove = 0;
            foreach (KeyValuePair<int, FairyUIFormPreparedState> pair in s_StatesBySerialId)
            {
                if (ReferenceEquals(pair.Value, state))
                {
                    serialIdToRemove = pair.Key;
                    break;
                }
            }

            if (serialIdToRemove != 0)
            {
                s_StatesBySerialId.Remove(serialIdToRemove);
            }
        }

        private sealed class SynchronousOpenScope : IDisposable
        {
            private FairyUIFormPreparedState m_State;

            internal SynchronousOpenScope(FairyUIFormPreparedState state)
            {
                m_State = state;
            }

            public void Dispose()
            {
                FairyUIFormPreparedState state = m_State;
                m_State = null;
                if (state == null)
                {
                    return;
                }

                lock (s_Gate)
                {
                    if (ReferenceEquals(s_SynchronousOpenState, state))
                    {
                        s_SynchronousOpenState = null;
                    }
                }
            }
        }
    }
}
