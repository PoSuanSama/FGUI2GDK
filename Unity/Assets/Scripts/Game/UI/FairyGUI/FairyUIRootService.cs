using System;
using System.Collections.Generic;
using FairyGUI;
using GameFramework;
using GameFramework.UI;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Maps each GF UI group GameObject into the single FairyGUI display tree without adding a
    /// proxy GameObject to the Unity hierarchy.
    /// </summary>
    public sealed class FairyUIRootService
    {
        private static FairyUIRootService s_Instance;

        private readonly Dictionary<string, FairyUIGroupContainer> m_Groups =
            new Dictionary<string, FairyUIGroupContainer>(StringComparer.Ordinal);
        private readonly Dictionary<GDKUIGroupHelper, FairyUIGroupContainer> m_GroupsByHelper =
            new Dictionary<GDKUIGroupHelper, FairyUIGroupContainer>();

        private bool m_Initialized;

        public static FairyUIRootService Instance => s_Instance ??= new FairyUIRootService();

        public GRoot Root => GRoot.inst;

        public void EnsureInitialized(int designResolutionX, int designResolutionY)
        {
            if (m_Initialized)
            {
                return;
            }

            Root.SetContentScaleFactor(
                designResolutionX,
                designResolutionY,
                UIContentScaler.ScreenMatchMode.MatchWidthOrHeight);
            Root.onSizeChanged.Add(HandleRootSizeChanged);
            AttachStageToUIRoot();
            m_Initialized = true;
        }

        private void AttachStageToUIRoot()
        {
            Transform stageTransform = Stage.inst.gameObject.transform;
            if (stageTransform == null)
            {
                return;
            }

            var uiComponent = GameEntry.UI;
            if (uiComponent == null)
            {
                return;
            }

            Transform uiRoot = uiComponent.transform;
            if (stageTransform.parent == uiRoot)
            {
                return;
            }

            // Reparent Stage under the plain "UI" node (not the ScreenSpace Canvas) so FairyGUI
            // keeps its own content scale and is no longer left floating at the scene root.
            // GRoot remains a child of Stage because FairyGUI requires that logical/physical tree.
            stageTransform.SetParent(uiRoot, true);
        }

        public void ValidateGroup(IUIGroup uiGroup)
        {
            if (uiGroup == null)
            {
                throw new ArgumentNullException(nameof(uiGroup));
            }

            if (uiGroup.Helper is not GDKUIGroupHelper)
            {
                throw new GameFrameworkException(
                    Utility.Text.Format(
                        "GF UI group '{0}' must use '{1}' before it can host FairyGUI forms.",
                        uiGroup.Name,
                        typeof(GDKUIGroupHelper).FullName));
            }
        }

        public FairyUIGroupContainer GetOrCreateGroup(IUIGroup uiGroup)
        {
            ValidateGroup(uiGroup);
            GDKUIGroupHelper helper = (GDKUIGroupHelper)uiGroup.Helper;

            if (m_Groups.TryGetValue(uiGroup.Name, out FairyUIGroupContainer group))
            {
                if (!ReferenceEquals(group.Helper, helper))
                {
                    throw new GameFrameworkException(
                        $"GF UI group '{uiGroup.Name}' changed its helper instance at runtime.");
                }

                group.SetDepth(uiGroup.Depth);
                group.SynchronizeWithRoot();
                return group;
            }

            Container container = new Container(helper.gameObject);
            Root.container.AddChild(container);

            group = new FairyUIGroupContainer(this, uiGroup.Name, helper, container, Root);
            m_Groups.Add(uiGroup.Name, group);
            m_GroupsByHelper.Add(helper, group);
            group.SetDepth(uiGroup.Depth);
            ReorderGroups();
            return group;
        }

        public bool TryReleaseGroup(string groupName)
        {
            if (!m_Groups.TryGetValue(groupName, out FairyUIGroupContainer group))
            {
                return false;
            }

            if (!group.IsEmpty)
            {
                return false;
            }

            m_Groups.Remove(groupName);
            m_GroupsByHelper.Remove(group.Helper);
            group.Dispose();
            ReorderGroups();
            return true;
        }

        internal void SetGroupDepth(GDKUIGroupHelper helper, int depth)
        {
            if (helper != null && m_GroupsByHelper.TryGetValue(helper, out FairyUIGroupContainer group))
            {
                group.SetDepth(depth);
            }
        }

        internal void ReleaseGroup(GDKUIGroupHelper helper)
        {
            if (helper == null || !m_GroupsByHelper.TryGetValue(helper, out FairyUIGroupContainer group))
            {
                return;
            }

            m_GroupsByHelper.Remove(helper);
            m_Groups.Remove(group.Name);
            group.Dispose();
            ReorderGroups();
        }

        internal void ReorderGroups()
        {
            if (m_Groups.Count == 0)
            {
                return;
            }

            Container rootContainer = Root.container;
            if (rootContainer == null || rootContainer.isDisposed)
            {
                return;
            }

            List<FairyUIGroupContainer> groups = new List<FairyUIGroupContainer>(m_Groups.Values);
            groups.Sort((left, right) =>
            {
                int depthComparison = left.Depth.CompareTo(right.Depth);
                return depthComparison != 0
                    ? depthComparison
                    : string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            });

            int firstIndex = rootContainer.numChildren;
            foreach (FairyUIGroupContainer group in groups)
            {
                int currentIndex = rootContainer.GetChildIndex(group.Container);
                if (currentIndex >= 0 && currentIndex < firstIndex)
                {
                    firstIndex = currentIndex;
                }
            }

            for (int i = 0; i < groups.Count; i++)
            {
                rootContainer.SetChildIndex(groups[i].Container, firstIndex + i);
            }
        }

        private void HandleRootSizeChanged()
        {
            foreach (FairyUIGroupContainer group in m_Groups.Values)
            {
                group.SynchronizeWithRoot();
            }
        }
    }
}
