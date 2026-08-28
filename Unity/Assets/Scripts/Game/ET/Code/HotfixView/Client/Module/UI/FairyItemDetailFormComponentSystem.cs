using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FairyGUI;
using Game;
using Game.FairyGUI.Package1;
using UnityEngine;

namespace ET.Client
{
    /// <summary>
    /// FairyItemDetailForm 的 HotfixView 行为 System(原 FairyItemDetailPresenter 行为迁入)。
    /// 三实例界面:每个详情窗 Component 独立,拖动/置顶状态都在各自实例上。
    /// </summary>
    [EntitySystemOf(typeof(FairyItemDetailFormComponent))]
    [FriendOf(typeof(FairyItemDetailFormComponent))]
    [FriendOf(typeof(FairyUIFormComponent))]
    public static partial class FairyItemDetailFormComponentSystem
    {
        [EntitySystem]
        private static void FairyUIFormOnViewReady(this FairyItemDetailFormComponent self)
        {
            UIItemDetailWindow view = self.View as UIItemDetailWindow;
            if (view == null)
            {
                Log.Error("FairyItemDetailForm OnViewReady: view is not ready.");
                return;
            }

            self.WindowParts = new List<GObject>
            {
                view.WindowFrame,
                view.WindowTopbar,
                view.TitleText,
                view.FocusTokenText,
                view.ItemNameText,
                view.ItemTypeText,
                view.DetailLine,
                view.DescriptionText,
                view.FocusStatusText,
                view.OpenOverlayButton,
                view.CloseButton
            };

            EntityRef<FairyItemDetailFormComponent> selfRef = self;
            view.WindowFrame.draggable = true;
            view.WindowFrame.dragBounds = new Rect(0f, 0f, view.width, view.height);

            self.DragStart = context => OnWindowDragStart(selfRef);
            self.DragMove = context => OnWindowDragMove(selfRef);
            self.DragEnd = context => OnWindowDragEnd(selfRef);
            self.WindowClick = context => OnWindowClick(selfRef);
            self.OpenOverlayClick = context => OpenOverlayAsync(selfRef).Forget();
            self.CloseClick = context => CloseDetail(selfRef);

            view.WindowFrame.onDragStart.Add(self.DragStart);
            view.WindowFrame.onDragMove.Add(self.DragMove);
            view.WindowFrame.onDragEnd.Add(self.DragEnd);
            view.WindowFrame.onClick.Add(self.WindowClick);
            view.OpenOverlayButton.onClick.Add(self.OpenOverlayClick);
            view.CloseButton.onClick.Add(self.CloseClick);
        }

        [EntitySystem]
        private static void FairyUIFormOnOpen(this FairyItemDetailFormComponent self)
        {
            self.OpenData = self.UserData as FairyItemDetailOpenData;
            if (self.OpenData == null)
            {
                throw new InvalidOperationException("FairyGUI item detail requires FairyItemDetailOpenData.");
            }

            UIItemDetailWindow view = self.View as UIItemDetailWindow;
            if (view == null)
            {
                return;
            }

            FairyInventoryItemData item = self.OpenData.Item;
            int slot = (self.OpenData.Token - 1) % 5;
            view.SetXY((slot - 2) * 46, ((slot * 2) % 5 - 2) * 28);
            view.FocusTokenText.text = $"窗口 #{self.OpenData.Token}";
            view.ItemNameText.text = item.Name;
            view.ItemTypeText.text = $"{item.Category} / 数量 {item.Count}";
            view.DescriptionText.text = global::GameFramework.Utility.Text.Format(
                "{0}\n点击窗口主体可拖动，单击会置顶。",
                item.Description);
            view.FocusStatusText.text = "已打开，等待点击聚焦";
        }

        [EntitySystem]
        private static void FairyUIFormOnClose(this FairyItemDetailFormComponent self)
        {
            UIItemDetailWindow view = self.View as UIItemDetailWindow;
            if (view != null)
            {
                view.WindowFrame.onDragStart.Remove(self.DragStart);
                view.WindowFrame.onDragMove.Remove(self.DragMove);
                view.WindowFrame.onDragEnd.Remove(self.DragEnd);
                view.WindowFrame.onClick.Remove(self.WindowClick);
                view.OpenOverlayButton.onClick.Remove(self.OpenOverlayClick);
                view.CloseButton.onClick.Remove(self.CloseClick);
                view.WindowFrame.draggable = false;
            }

            self.DragStart = null;
            self.DragMove = null;
            self.DragEnd = null;
            self.WindowClick = null;
            self.OpenOverlayClick = null;
            self.CloseClick = null;
            self.WindowParts?.Clear();
            self.WindowParts = null;

            FairyItemDetailOpenData openData = self.OpenData;
            self.OpenData = null;
            FairyInventoryFlow.NotifyDetailClosed(openData);
        }

        [EntitySystem]
        private static void FairyUIFormOnPause(this FairyItemDetailFormComponent self)
        {
            ++self.PauseCount;
            UpdateLifecycleStatus(self, "已暂停");
        }

        [EntitySystem]
        private static void FairyUIFormOnResume(this FairyItemDetailFormComponent self)
        {
            ++self.ResumeCount;
            UpdateLifecycleStatus(self, "已恢复");
        }

        [EntitySystem]
        private static void FairyUIFormOnCover(this FairyItemDetailFormComponent self)
        {
            ++self.CoverCount;
            UpdateLifecycleStatus(self, "已被覆盖");
        }

        [EntitySystem]
        private static void FairyUIFormOnReveal(this FairyItemDetailFormComponent self)
        {
            ++self.RevealCount;
            UpdateLifecycleStatus(self, "已重新显示");
        }

        [EntitySystem]
        private static void FairyUIFormOnRefocus(this FairyItemDetailFormComponent self)
        {
            ++self.RefocusCount;
            UpdateLifecycleStatus(self, $"已置顶 {self.RefocusCount} 次");
        }

        private static void OnWindowDragStart(EntityRef<FairyItemDetailFormComponent> selfRef)
        {
            FairyItemDetailFormComponent self = selfRef;
            if (self == null || self.View is not UIItemDetailWindow view)
            {
                return;
            }

            self.LastDragX = view.WindowFrame.x;
            self.LastDragY = view.WindowFrame.y;
        }

        private static void OnWindowDragMove(EntityRef<FairyItemDetailFormComponent> selfRef)
        {
            FairyItemDetailFormComponent self = selfRef;
            if (self == null || self.View is not UIItemDetailWindow view || self.WindowParts == null)
            {
                return;
            }

            float dx = view.WindowFrame.x - self.LastDragX;
            float dy = view.WindowFrame.y - self.LastDragY;
            if (dx == 0f && dy == 0f)
            {
                return;
            }

            foreach (GObject part in self.WindowParts)
            {
                if (part == view.WindowFrame)
                {
                    continue;
                }

                part.x += dx;
                part.y += dy;
            }

            self.LastDragX = view.WindowFrame.x;
            self.LastDragY = view.WindowFrame.y;
        }

        private static void OnWindowDragEnd(EntityRef<FairyItemDetailFormComponent> selfRef)
        {
            FairyItemDetailFormComponent self = selfRef;
            if (self == null || self.View is not UIItemDetailWindow view)
            {
                return;
            }

            self.LastDragX = view.WindowFrame.x;
            self.LastDragY = view.WindowFrame.y;
        }

        private static void OnWindowClick(EntityRef<FairyItemDetailFormComponent> selfRef)
        {
            FairyItemDetailFormComponent self = selfRef;
            if (self != null)
            {
                FairyInventoryFlow.Refocus(self.OpenData);
            }
        }

        private static void CloseDetail(EntityRef<FairyItemDetailFormComponent> selfRef)
        {
            FairyItemDetailFormComponent self = selfRef;
            if (self != null)
            {
                FairyInventoryFlow.Close(self.OpenData);
            }
        }

        private static void UpdateLifecycleStatus(FairyItemDetailFormComponent self, string status)
        {
            if (self.View is UIItemDetailWindow view)
            {
                view.FocusStatusText.text = status;
            }
        }

        private static async UniTaskVoid OpenOverlayAsync(EntityRef<FairyItemDetailFormComponent> selfRef)
        {
            try
            {
                FairyItemDetailFormComponent self = selfRef;
                UIComponent owner = self?.OpenData?.Owner;
                if (owner == null)
                {
                    return;
                }

                await FairyInventoryFlow.OpenOverlayAsync(owner);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }
    }
}
