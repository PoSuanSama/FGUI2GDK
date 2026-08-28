using System;
using FairyGUI;
using Game;
using Game.FairyGUI.Package1;

namespace ET.Client
{
    /// <summary>
    /// FairyRuntimeInspectorForm 的 HotfixView 行为 System(原 FairyRuntimeInspectorPresenter 行为迁入)。
    /// </summary>
    [EntitySystemOf(typeof(FairyRuntimeInspectorFormComponent))]
    [FriendOf(typeof(FairyRuntimeInspectorFormComponent))]
    [FriendOf(typeof(FairyUIFormComponent))]
    public static partial class FairyRuntimeInspectorFormComponentSystem
    {
        [EntitySystem]
        private static void FairyUIFormOnViewReady(this FairyRuntimeInspectorFormComponent self)
        {
            UIRuntimeInspectorView view = self.View as UIRuntimeInspectorView;
            if (view == null)
            {
                Log.Error("FairyRuntimeInspectorForm OnViewReady: view is not ready.");
                return;
            }

            GTextField infoText = new GTextField();
            infoText.name = "runtimeInfo";
            infoText.SetXY(40, 72);
            infoText.SetSize(720, 470);
            view.AddChild(infoText);
            self.InfoText = infoText;

            EntityRef<FairyRuntimeInspectorFormComponent> selfRef = self;
            self.CloseClick = context => CloseInspector(selfRef);
            view.CloseButton.onClick.Add(self.CloseClick);
            RefreshRuntimeInfo(self);
        }

        [EntitySystem]
        private static void FairyUIFormOnOpen(this FairyRuntimeInspectorFormComponent self)
        {
            UIComponent owner = self.UserData as UIComponent;
            if (owner == null || owner.IsDisposed)
            {
                throw new InvalidOperationException(
                    "ET FairyGUI RuntimeInspector requires a live UIComponent owner.");
            }

            self.Owner = owner;
            RefreshRuntimeInfo(self);
        }

        [EntitySystem]
        private static void FairyUIFormOnClose(this FairyRuntimeInspectorFormComponent self)
        {
            UIRuntimeInspectorView view = self.View as UIRuntimeInspectorView;
            if (view != null)
            {
                view.CloseButton.onClick.Remove(self.CloseClick);
            }

            self.CloseClick = null;
            self.InfoText = null;
            self.Owner = default;
        }

        [EntitySystem]
        private static void FairyUIFormOnUpdate(
            this FairyRuntimeInspectorFormComponent self,
            float elapseSeconds,
            float realElapseSeconds)
        {
            RefreshRuntimeInfo(self);
        }

        private static void RefreshRuntimeInfo(FairyRuntimeInspectorFormComponent self)
        {
            if (self.InfoText == null)
            {
                return;
            }

            FairyUIManager uiManager = FairyUIManager.Instance;
            int loadedForms = uiManager.GetAllLoadedUIForms().Length;
            int loadingForms = uiManager.GetAllLoadingUIFormSerialIds().Length;
            int tableCount = 0;
            if (Tables.Instance != null && Tables.Instance.DataTables != null)
            {
                foreach (var _ in Tables.Instance.DataTables)
                {
                    tableCount++;
                }
            }

            self.InfoText.text = string.Join(
                Environment.NewLine,
                "FairyGUI RuntimeInspector",
                $"Loaded UIForms: {loadedForms}",
                $"Loading UIForms: {loadingForms}",
                $"ET Luban Tables: {tableCount}",
                "Package: Package1",
                "Component: RuntimeInspectorView");
        }

        private static void CloseInspector(EntityRef<FairyRuntimeInspectorFormComponent> selfRef)
        {
            FairyRuntimeInspectorFormComponent self = selfRef;
            if (self == null)
            {
                return;
            }

            UIComponent owner = self.Owner;
            if (owner == null)
            {
                return;
            }

            FairyUIForm form = FairyUIManager.Instance.GetUIForm(
                "Assets/Res/UI/FairyGUI/FairyRuntimeInspectorForm.json");
            if (form != null)
            {
                UIComponentFairyUIBridge.CloseBySerialId(owner, form.SerialId);
            }
        }
    }
}
