using System;
using Cysharp.Threading.Tasks;
using FairyGUI;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Game
{
    public abstract class AFairyUIForm : AExUIForm
    {
        private const int DesignResolutionX = 1280;
        private const int DesignResolutionY = 720;
        private const float MinimumParentScale = 0.0001f;

        private UIPanel m_UIPanel;
        private Transform m_TransformIsolation;
        private FairyPackageLease m_PackageLease;
        private int m_OpenVersion;
        private bool m_ViewReady;

        protected abstract string fairyPackageName { get; }

        protected abstract string fairyComponentName { get; }

        protected GComponent fairyView => m_UIPanel?.ui;

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            int openVersion = ++m_OpenVersion;
            LoadFairyViewAsync(openVersion).Forget();
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            ++m_OpenVersion;
            ReleaseViewBindings();
            ReleaseFairyPanel();
            ReleaseFairyPackage();
            base.OnClose(isShutdown, userData);
        }

        protected override void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            base.OnDepthChanged(uiGroupDepth, depthInUIGroup);
            ApplySortingOrder();
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            RefreshTransformIsolation();
        }

        protected virtual void OnFairyViewReady()
        {
        }

        protected virtual void OnFairyViewClosing()
        {
        }

        private async UniTask LoadFairyViewAsync(int openVersion)
        {
            try
            {
                FairyPackageLease packageLease = await FairyPackageManager.AcquireAsync(fairyPackageName);

                if (openVersion != m_OpenVersion)
                {
                    packageLease.Dispose();
                    return;
                }

                m_PackageLease = packageLease;

                GRoot.inst.SetContentScaleFactor(
                    DesignResolutionX,
                    DesignResolutionY,
                    UIContentScaler.ScreenMatchMode.MatchWidthOrHeight);

                GameObject isolationObject = new GameObject("FairyGUI Transform Isolation");
                m_TransformIsolation = isolationObject.transform;
                m_TransformIsolation.SetParent(transform, false);
                RefreshTransformIsolation();

                GameObject panelObject = new GameObject($"{gameObject.name} (FairyGUI)");
                panelObject.layer = LayerMask.NameToLayer(StageCamera.LayerName);
                panelObject.transform.SetParent(m_TransformIsolation, false);
                m_UIPanel = panelObject.AddComponent<UIPanel>();
                m_UIPanel.packageName = fairyPackageName;
                m_UIPanel.componentName = fairyComponentName;
                m_UIPanel.fitScreen = FitScreen.FitSize;
                m_UIPanel.CreateUI();
                ApplySortingOrder();

                if (m_UIPanel.ui == null)
                {
                    throw new InvalidOperationException(
                        $"Unable to create FairyGUI view '{fairyPackageName}/{fairyComponentName}'.");
                }

                m_ViewReady = true;
                OnFairyViewReady();
            }
            catch (Exception exception)
            {
                if (openVersion == m_OpenVersion)
                {
                    ReleaseViewBindings();
                    ReleaseFairyPanel();
                    ReleaseFairyPackage();
                    Log.Error(
                        "Failed to open FairyGUI view '{0}/{1}': {2}",
                        fairyPackageName,
                        fairyComponentName,
                        exception);
                }
            }
        }

        private void ApplySortingOrder()
        {
            if (m_UIPanel?.container != null)
            {
                m_UIPanel.SetSortingOrder(Depth, true);
            }
        }

        private void RefreshTransformIsolation()
        {
            if (m_TransformIsolation == null)
            {
                return;
            }

            Transform parentTransform = m_TransformIsolation.parent;
            Vector3 parentScale = parentTransform.lossyScale;
            if (Mathf.Abs(parentScale.x) < MinimumParentScale ||
                Mathf.Abs(parentScale.y) < MinimumParentScale ||
                Mathf.Abs(parentScale.z) < MinimumParentScale)
            {
                throw new InvalidOperationException(
                    $"Unable to isolate FairyGUI from zero-scale parent '{parentTransform.name}'.");
            }

            m_TransformIsolation.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            m_TransformIsolation.localScale = new Vector3(
                1f / parentScale.x,
                1f / parentScale.y,
                1f / parentScale.z);
        }

        private void ReleaseViewBindings()
        {
            if (!m_ViewReady)
            {
                return;
            }

            OnFairyViewClosing();
            m_ViewReady = false;
        }

        private void ReleaseFairyPanel()
        {
            GameObject panelObject = m_UIPanel != null ? m_UIPanel.gameObject : null;
            GameObject isolationObject = m_TransformIsolation != null ? m_TransformIsolation.gameObject : null;
            m_UIPanel = null;
            m_TransformIsolation = null;

            if (isolationObject != null)
            {
                isolationObject.SetActive(false);
                Destroy(isolationObject);
                return;
            }

            if (panelObject != null)
            {
                panelObject.SetActive(false);
                Destroy(panelObject);
            }
        }

        private void ReleaseFairyPackage()
        {
            FairyPackageLease packageLease = m_PackageLease;
            m_PackageLease = null;
            packageLease?.Dispose();
        }

        protected override void OnPause()
        {
            base.OnPause();
            SetFairyViewInteraction(false);
        }

        protected override void OnResume()
        {
            base.OnResume();
            RefreshTransformIsolation();
            SetFairyViewInteraction(true);
        }

        protected override void OnCover()
        {
            base.OnCover();
            SetFairyViewInteraction(false);
        }

        protected override void OnReveal()
        {
            base.OnReveal();
            RefreshTransformIsolation();
            SetFairyViewInteraction(true);
        }

        private void SetFairyViewInteraction(bool enabled)
        {
            if (m_UIPanel?.container == null || m_UIPanel.ui == null)
            {
                return;
            }

            m_UIPanel.container.touchable = enabled;
            m_UIPanel.ui.visible = enabled;
        }
    }
}
