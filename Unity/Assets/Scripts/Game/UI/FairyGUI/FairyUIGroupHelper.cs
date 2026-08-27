using System;
using System.Collections.Generic;
using FairyGUI;
using GameFramework.UI;

namespace Game
{
    /// <summary>
    /// FairyGUI 原生的界面组辅助器：一个挂在 GRoot 下的显示容器，承载该组内所有界面。
    /// 不再依赖 UGUI 的 Canvas/UIGroupHelper，深度由 FairyGUI 的 child index 表达。
    /// </summary>
    public sealed class FairyUIGroupHelper : IUIGroupHelper
    {
        private readonly List<GComponent> m_Forms = new List<GComponent>();
        private readonly Container m_Container;

        public FairyUIGroupHelper(string name)
        {
            Name = name;
            m_Container = new Container($"FairyUI Group - {name}");
            m_Container.onUpdate += HandleContainerUpdate;
        }

        public string Name { get; }

        public Container Container => m_Container;

        public int Depth { get; private set; } = int.MinValue;

        public bool IsEmpty => m_Forms.Count == 0;

        public void SetDepth(int depth)
        {
            Depth = depth;
        }

        public void AttachToRoot(GRoot root)
        {
            if (root == null || root.isDisposed || root.container == null)
            {
                return;
            }

            if (!ReferenceEquals(m_Container.parent, root.container))
            {
                root.container.AddChild(m_Container);
            }

            SynchronizeWithRoot(root);
        }

        public void AddForm(GComponent form, int depthInUIGroup)
        {
            if (form == null)
            {
                throw new ArgumentNullException(nameof(form));
            }

            if (m_Forms.Contains(form))
            {
                throw new InvalidOperationException(
                    $"FairyGUI form '{form.name}' is already attached to UI group '{Name}'.");
            }

            m_Forms.Add(form);
            m_Container.AddChild(form.displayObject);
            form.SetSize(m_Container.width, m_Container.height);
            form.AddRelation(GRoot.inst, RelationType.Size);
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
                form.RemoveRelation(GRoot.inst, RelationType.Size);
            }

            DisplayObject displayObject = form.displayObject;
            if (!m_Container.isDisposed &&
                displayObject != null &&
                !displayObject.isDisposed &&
                ReferenceEquals(displayObject.parent, m_Container))
            {
                m_Container.RemoveChild(displayObject);
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
                m_Container.SetChildIndex(m_Forms[i].displayObject, i);
            }
        }

        public void Dispose()
        {
            m_Container.onUpdate -= HandleContainerUpdate;
            if (!m_Container.isDisposed)
            {
                m_Container.RemoveFromParent();
                m_Container.Dispose();
            }

            m_Forms.Clear();
        }

        private void HandleContainerUpdate()
        {
            GRoot root = GRoot.inst;
            if (root != null && !root.isDisposed)
            {
                SynchronizeWithRoot(root);
            }
        }

        private void SynchronizeWithRoot(GRoot root)
        {
            if (root == null || m_Container.isDisposed || root.container == null || root.container.isDisposed)
            {
                return;
            }

            m_Container.SetSize(root.width, root.height);
        }
    }
}