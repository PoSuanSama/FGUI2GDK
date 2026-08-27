using System;
using System.Collections.Generic;
using FairyGUI;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A FairyGUI display container attached to an existing GF UI group GameObject.
    /// </summary>
    public sealed class FairyUIGroupContainer
    {
        private readonly FairyUIRootService m_Owner;
        private readonly GRoot m_Root;
        private readonly List<GComponent> m_Forms = new List<GComponent>();
        private readonly RectTransform m_HelperRectTransform;
        private readonly Vector3 m_OriginalLocalPosition;
        private readonly Vector3 m_OriginalAnchoredPosition;
        private readonly Quaternion m_OriginalLocalRotation;
        private readonly Vector3 m_OriginalLocalScale;
        private readonly int m_OriginalLayer;

        public string Name { get; }

        public GDKUIGroupHelper Helper { get; }

        public Container Container { get; }

        public int Depth { get; private set; } = int.MinValue;

        internal FairyUIGroupContainer(
            FairyUIRootService owner,
            string name,
            GDKUIGroupHelper helper,
            Container container,
            GRoot root)
        {
            m_Owner = owner;
            Name = name;
            Helper = helper;
            Container = container;
            m_Root = root;

            Transform helperTransform = helper.transform;
            m_HelperRectTransform = helperTransform as RectTransform;
            m_OriginalLocalPosition = helperTransform.localPosition;
            m_OriginalAnchoredPosition = m_HelperRectTransform != null
                ? m_HelperRectTransform.anchoredPosition3D
                : Vector3.zero;
            m_OriginalLocalRotation = helperTransform.localRotation;
            m_OriginalLocalScale = helperTransform.localScale;
            m_OriginalLayer = helper.gameObject.layer;

            Container.onUpdate += HandleContainerUpdate;
            SynchronizeWithRoot();
        }

        public bool IsEmpty => m_Forms.Count == 0;

        internal void SetDepth(int depth)
        {
            if (Depth == depth)
            {
                return;
            }

            Depth = depth;
            m_Owner.ReorderGroups();
        }

        internal void SynchronizeWithRoot()
        {
            if (Helper == null ||
                Container.isDisposed ||
                m_Root?.container == null ||
                m_Root.container.isDisposed ||
                m_Root.container.cachedTransform == null)
            {
                return;
            }

            Transform groupTransform = Helper.transform;
            Transform rootTransform = m_Root.container.cachedTransform;
            if (groupTransform.position != rootTransform.position ||
                groupTransform.rotation != rootTransform.rotation)
            {
                groupTransform.SetPositionAndRotation(rootTransform.position, rootTransform.rotation);
            }

            Vector3 localScale = groupTransform.localScale;
            Vector3 worldScale = groupTransform.lossyScale;
            Vector3 rootWorldScale = rootTransform.lossyScale;
            Vector3 synchronizedScale = new Vector3(
                ResolveLocalScale(localScale.x, worldScale.x, rootWorldScale.x, groupTransform.parent?.lossyScale.x),
                ResolveLocalScale(localScale.y, worldScale.y, rootWorldScale.y, groupTransform.parent?.lossyScale.y),
                ResolveLocalScale(localScale.z, worldScale.z, rootWorldScale.z, groupTransform.parent?.lossyScale.z));
            if (groupTransform.localScale != synchronizedScale)
            {
                groupTransform.localScale = synchronizedScale;
            }

            int rootLayer = rootTransform.gameObject.layer;
            if (Helper.gameObject.layer != rootLayer)
            {
                Helper.gameObject.layer = rootLayer;
                Container.SetChildrenLayer(rootLayer);
            }

            Container.SetSize(m_Root.width, m_Root.height);
        }

        public void AddForm(GComponent form, int depthInUIGroup)
        {
            if (form == null)
            {
                throw new System.ArgumentNullException(nameof(form));
            }

            if (m_Forms.Contains(form))
            {
                throw new InvalidOperationException(
                    $"FairyGUI form '{form.name}' is already attached to GF UI group '{Name}'.");
            }

            SynchronizeWithRoot();
            m_Forms.Add(form);
            Container.AddChild(form.displayObject);
            form.SetSize(m_Root.width, m_Root.height);
            form.AddRelation(m_Root, RelationType.Size);
            SetFormDepth(form, depthInUIGroup);
        }

        public void RemoveForm(GComponent form)
        {
            if (form == null || !m_Forms.Remove(form))
            {
                return;
            }

            if (!form.isDisposed)
            {
                form.RemoveRelation(m_Root, RelationType.Size);
            }

            DisplayObject displayObject = form.displayObject;
            if (!Container.isDisposed &&
                displayObject != null &&
                !displayObject.isDisposed &&
                ReferenceEquals(displayObject.parent, Container))
            {
                Container.RemoveChild(displayObject);
            }
        }

        public void SetFormDepth(GComponent form, int depthInUIGroup)
        {
            if (form == null || !m_Forms.Contains(form))
            {
                return;
            }

            form.sortingOrder = depthInUIGroup;
            m_Forms.Sort((left, right) => left.sortingOrder.CompareTo(right.sortingOrder));
            for (int i = 0; i < m_Forms.Count; i++)
            {
                Container.SetChildIndex(m_Forms[i].displayObject, i);
            }
        }

        internal void Dispose()
        {
            bool wasActive = Helper != null && Helper.gameObject.activeSelf;
            Container.onUpdate -= HandleContainerUpdate;
            if (!Container.isDisposed)
            {
                Container.RemoveFromParent();
                Container.Dispose();
            }
            if (Helper != null)
            {
                RestoreHelperTransform();
                if (wasActive)
                {
                    Helper.gameObject.SetActive(true);
                }
            }
        }

        private void HandleContainerUpdate()
        {
            SynchronizeWithRoot();
        }

        private void RestoreHelperTransform()
        {
            Transform helperTransform = Helper.transform;
            if (m_HelperRectTransform != null)
            {
                m_HelperRectTransform.anchoredPosition3D = m_OriginalAnchoredPosition;
            }
            else
            {
                helperTransform.localPosition = m_OriginalLocalPosition;
            }

            helperTransform.localRotation = m_OriginalLocalRotation;
            helperTransform.localScale = m_OriginalLocalScale;
            Helper.gameObject.layer = m_OriginalLayer;
        }

        private static float ResolveLocalScale(
            float currentLocalScale,
            float currentWorldScale,
            float targetWorldScale,
            float? parentWorldScale)
        {
            const float Epsilon = 0.000001f;
            if (Mathf.Abs(currentWorldScale) > Epsilon)
            {
                return currentLocalScale * targetWorldScale / currentWorldScale;
            }

            if (parentWorldScale.HasValue && Mathf.Abs(parentWorldScale.Value) > Epsilon)
            {
                return targetWorldScale / parentWorldScale.Value;
            }

            return targetWorldScale;
        }
    }
}
