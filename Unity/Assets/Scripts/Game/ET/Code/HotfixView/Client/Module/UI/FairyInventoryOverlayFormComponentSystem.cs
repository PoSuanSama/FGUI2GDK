using System;
using FairyGUI;
using Game;
using Game.FairyGUI.Package1;

namespace ET.Client
{
    /// <summary>
    /// FairyInventoryOverlayForm 的 HotfixView 行为 System(原 FairyInventoryOverlayPresenter 行为迁入)。
    /// </summary>
    [EntitySystemOf(typeof(FairyInventoryOverlayFormComponent))]
    [FriendOf(typeof(FairyInventoryOverlayFormComponent))]
    [FriendOf(typeof(FairyUIFormComponent))]
    public static partial class FairyInventoryOverlayFormComponentSystem
    {
        [EntitySystem]
        private static void FairyUIFormOnViewReady(this FairyInventoryOverlayFormComponent self)
        {
            UIInventoryOverlayView view = self.View as UIInventoryOverlayView;
            if (view == null)
            {
                Log.Error("FairyInventoryOverlayForm OnViewReady: view is not ready.");
                return;
            }

            EntityRef<FairyInventoryOverlayFormComponent> selfRef = self;
            self.CloseClick = context => CloseOverlay(selfRef);
            view.CloseButton.onClick.Add(self.CloseClick);
        }

        [EntitySystem]
        private static void FairyUIFormOnOpen(this FairyInventoryOverlayFormComponent self)
        {
            self.OpenData = self.UserData as FairyInventoryOverlayOpenData;
            if (self.OpenData == null)
            {
                throw new InvalidOperationException("FairyGUI inventory overlay requires FairyInventoryOverlayOpenData.");
            }
        }

        [EntitySystem]
        private static void FairyUIFormOnClose(this FairyInventoryOverlayFormComponent self)
        {
            UIInventoryOverlayView view = self.View as UIInventoryOverlayView;
            if (view != null)
            {
                view.CloseButton.onClick.Remove(self.CloseClick);
            }

            self.CloseClick = null;
            self.OpenData = null;
        }

        private static void CloseOverlay(EntityRef<FairyInventoryOverlayFormComponent> selfRef)
        {
            FairyInventoryOverlayFormComponent self = selfRef;
            if (self != null)
            {
                FairyInventoryFlow.Close(self.OpenData);
            }
        }
    }
}
