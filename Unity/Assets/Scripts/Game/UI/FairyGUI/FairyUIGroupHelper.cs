using System;
using System.Collections.Generic;
using FairyGUI;
using GameFramework.UI;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// FairyGUI 原生的界面组辅助器(design §10.3):
    /// 一个挂在 GRoot 下的全屏显示容器,内含一个安全区容器。
    /// 安全区容器按 Screen.safeArea 换算到 GRoot 设计坐标(Y 轴翻转、
    /// 除以 contentScaleFactor),方向/安全区变化时统一重算;
    /// 界面默认挂安全区容器(页面经 FairyGUI 关系适配),全屏界面(descriptor
    /// fullScreen,如覆盖层/背景)挂全屏容器。
    /// </summary>
    public sealed class FairyUIGroupHelper : IUIGroupHelper
    {
        private readonly List<GComponent> m_Forms = new List<GComponent>();
        private readonly Container m_Container;
        private GComponent m_SafeAreaContainer;

        /// <summary>
        /// 上次应用的安全区逻辑矩形(GRoot 设计坐标),变化才重算。
        /// </summary>
        private Rect m_AppliedSafeArea = new Rect(float.NaN, float.NaN, float.NaN, float.NaN);

        public FairyUIGroupHelper(string name)
        {
            Name = name;
            m_Container = new Container($"FairyUI Group - {name}");
            m_Container.onUpdate += HandleContainerUpdate;
        }

        public string Name { get; }

        public Container Container => m_Container;

        public GComponent SafeAreaContainer => m_SafeAreaContainer;

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
            AddForm(form, depthInUIGroup, attachToSafeArea: true);
        }

        /// <summary>
        /// 把窗体挂到组容器。<paramref name="attachToSafeArea"/> 为 true 时挂安全区
        /// 容器(默认,页面经关系适配安全区);false 时挂全屏容器(覆盖层/背景)。
        /// </summary>
        public void AddForm(GComponent form, int depthInUIGroup, bool attachToSafeArea)
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

            GComponent host = attachToSafeArea ? EnsureSafeAreaContainer() : null;
            if (host != null)
            {
                // GComponent.AddChild(GObject) 内部挂 displayObject。
                host.AddChild(form);
                form.SetSize(host.width, host.height);
                // 页面经关系适配安全区:容器尺寸变化时窗体跟随。
                form.AddRelation(host, RelationType.Size);
            }
            else
            {
                m_Container.AddChild(form.displayObject);
                form.SetSize(m_Container.width, m_Container.height);
                // 全屏界面跟随 GRoot(组容器与 GRoot 同尺寸)。
                form.AddRelation(GRoot.inst, RelationType.Size);
            }

            m_Forms.Add(form);
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
                form.RemoveRelation(m_SafeAreaContainer, RelationType.Size);
            }

            DisplayObject displayObject = form.displayObject;
            if (displayObject != null &&
                !displayObject.isDisposed &&
                (ReferenceEquals(displayObject.parent, m_Container) ||
                 (m_SafeAreaContainer != null && ReferenceEquals(displayObject.parent, m_SafeAreaContainer.displayObject))))
            {
                displayObject.parent.RemoveChild(displayObject);
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
                DisplayObject displayObject = m_Forms[i].displayObject;
                if (displayObject != null && displayObject.parent != null)
                {
                    displayObject.parent.SetChildIndex(displayObject, i);
                }
            }
        }

        public void Dispose()
        {
            m_Container.onUpdate -= HandleContainerUpdate;
            if (m_SafeAreaContainer != null && !m_SafeAreaContainer.isDisposed)
            {
                m_SafeAreaContainer.Dispose();
                m_SafeAreaContainer = null;
            }

            if (!m_Container.isDisposed)
            {
                m_Container.RemoveFromParent();
                m_Container.Dispose();
            }

            m_Forms.Clear();
        }

        private GComponent EnsureSafeAreaContainer()
        {
            if (m_SafeAreaContainer != null)
            {
                return m_SafeAreaContainer;
            }

            GComponent safeAreaContainer = new GComponent();
            safeAreaContainer.name = $"FairyUI SafeArea - {Name}";
            safeAreaContainer.SetSize(m_Container.width, m_Container.height);
            m_Container.AddChild(safeAreaContainer.displayObject);
            m_SafeAreaContainer = safeAreaContainer;
            ApplyScreenSafeArea();
            return m_SafeAreaContainer;
        }

        private void HandleContainerUpdate()
        {
            GRoot root = GRoot.inst;
            if (root != null && !root.isDisposed)
            {
                SynchronizeWithRoot(root);
            }

            if (m_SafeAreaContainer != null)
            {
                ApplyScreenSafeArea();
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

        /// <summary>
        /// 把当前 Screen.safeArea(像素)换算到 GRoot 设计坐标并应用到安全区容器。
        /// Y 轴翻转:Unity 屏幕原点在左下,GRoot 原点在左上。
        /// 测试可经 <see cref="ApplySafeAreaRect"/> 注入任意像素矩形验证换算。
        /// </summary>
        private void ApplyScreenSafeArea()
        {
            ApplySafeAreaRect(Screen.safeArea);
        }

        /// <summary>
        /// 像素矩形 -> GRoot 设计坐标 -> 安全区容器位置/尺寸(变化才应用)。
        /// 测试经此入口注入任意像素矩形验证换算。
        /// </summary>
        public void ApplySafeAreaRect(Rect pixelSafeArea)
        {
            if (m_SafeAreaContainer == null || m_SafeAreaContainer.isDisposed)
            {
                return;
            }

            float scaleFactor = GRoot.contentScaleFactor;
            if (scaleFactor <= 0f)
            {
                return;
            }

            float screenHeight = Screen.height;
            if (screenHeight <= 0f)
            {
                return;
            }

            float logicalX = pixelSafeArea.xMin / scaleFactor;
            float logicalY = (screenHeight - pixelSafeArea.yMax) / scaleFactor;
            float logicalWidth = pixelSafeArea.width / scaleFactor;
            float logicalHeight = pixelSafeArea.height / scaleFactor;

            // 钳制到全屏容器范围内,防止编辑器/模拟器异常数据。
            logicalWidth = Mathf.Min(logicalWidth, m_Container.width - logicalX);
            logicalHeight = Mathf.Min(logicalHeight, m_Container.height - logicalY);
            logicalX = Mathf.Max(0f, logicalX);
            logicalY = Mathf.Max(0f, logicalY);

            Rect logical = new Rect(logicalX, logicalY, logicalWidth, logicalHeight);
            if (logical == m_AppliedSafeArea)
            {
                return;
            }

            m_SafeAreaContainer.SetXY(logical.x, logical.y);
            m_SafeAreaContainer.SetSize(logical.width, logical.height);
            m_AppliedSafeArea = logical;
        }
    }
}
