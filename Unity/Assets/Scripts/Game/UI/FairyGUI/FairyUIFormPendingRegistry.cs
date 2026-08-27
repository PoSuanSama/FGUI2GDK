using System;
using System.Collections.Generic;
using GameFramework;

namespace Game
{
    /// <summary>
    /// 在 FairyUIManager（异步准备）与 FairyUIForm.OnInit（UIManager 回调）之间传递待采纳状态。
    /// 复用 UGUI 时代的 serialId 关联思路：OpenUIForm 返回 serialId 后绑定，OnInit 里消费。
    /// </summary>
    internal static class FairyUIFormPendingRegistry
    {
        private static readonly object s_Gate = new object();
        private static readonly Dictionary<int, FairyUIFormPendingState> s_StatesBySerialId =
            new Dictionary<int, FairyUIFormPendingState>();
        private static readonly HashSet<FairyUIFormPendingState> s_OwnedStates =
            new HashSet<FairyUIFormPendingState>();
        private static FairyUIFormPendingState s_SynchronousOpenState;

        internal static IDisposable BeginOpen(FairyUIFormPendingState state)
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
                        "A FairyGUI pending-state handoff is already entering the synchronous open boundary.");
                }

                if (!s_OwnedStates.Add(state))
                {
                    throw new GameFrameworkException(
                        $"FairyGUI pending state '{state.DescriptorKey}' is already registered.");
                }

                s_SynchronousOpenState = state;
                return new SynchronousOpenScope(state);
            }
        }

        internal static void BindSerialId(int serialId, FairyUIFormPendingState state)
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
                        $"GF serial ID '{serialId}' already has a FairyGUI pending state.");
                }

                s_StatesBySerialId.Add(serialId, state);
            }
        }

        internal static FairyUIFormPendingState ConsumeNewInstance(
            int serialId,
            string descriptorKey,
            object userData)
        {
            lock (s_Gate)
            {
                FairyUIFormPendingState state = null;
                if (s_StatesBySerialId.TryGetValue(serialId, out FairyUIFormPendingState boundState))
                {
                    state = boundState;
                    s_StatesBySerialId.Remove(serialId);
                }
                else if (s_SynchronousOpenState != null)
                {
                    state = s_SynchronousOpenState;
                }

                return ConsumeOwnedState(state, descriptorKey, userData, serialId);
            }
        }

        internal static bool TryRemove(FairyUIFormPendingState state)
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

        private static FairyUIFormPendingState ConsumeOwnedState(
            FairyUIFormPendingState state,
            string descriptorKey,
            object userData,
            int serialId)
        {
            if (state == null)
            {
                throw new GameFrameworkException(
                    Utility.Text.Format(
                        "No prepared FairyGUI state is registered for descriptor '{0}' and serial ID '{1}'.",
                        descriptorKey,
                        serialId));
            }

            if (!s_OwnedStates.Contains(state) ||
                !string.Equals(state.DescriptorKey, descriptorKey, StringComparison.Ordinal) ||
                !ReferenceEquals(state.UserData, userData))
            {
                throw new GameFrameworkException(
                    $"Prepared FairyGUI state '{descriptorKey}' does not match the open request.");
            }

            s_OwnedStates.Remove(state);
            RemoveSerialBinding(state);
            return state;
        }

        private static void RemoveSerialBinding(FairyUIFormPendingState state)
        {
            int serialIdToRemove = 0;
            foreach (KeyValuePair<int, FairyUIFormPendingState> pair in s_StatesBySerialId)
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
            private FairyUIFormPendingState m_State;

            internal SynchronousOpenScope(FairyUIFormPendingState state)
            {
                m_State = state;
            }

            public void Dispose()
            {
                FairyUIFormPendingState state = m_State;
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