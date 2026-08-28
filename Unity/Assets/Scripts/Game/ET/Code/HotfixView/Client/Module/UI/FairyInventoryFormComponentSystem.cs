using System;
using Cysharp.Threading.Tasks;
using FairyGUI;
using Game;
using Game.FairyGUI.Package1;

namespace ET.Client
{
    /// <summary>
    /// FairyInventoryForm 的 HotfixView 行为 System(原 FairyInventoryPresenter 行为迁入)。
    /// 静态纯方法;按钮/事件订阅用闭包捕获 EntityRef,不在 System 里持有实例状态。
    /// </summary>
    [EntitySystemOf(typeof(FairyInventoryFormComponent))]
    [FriendOf(typeof(FairyInventoryFormComponent))]
    [FriendOf(typeof(FairyUIFormComponent))]
    public static partial class FairyInventoryFormComponentSystem
    {
        [EntitySystem]
        private static void FairyUIFormOnViewReady(this FairyInventoryFormComponent self)
        {
            UIInventoryView view = self.View as UIInventoryView;
            if (view == null)
            {
                Log.Error("FairyInventoryForm OnViewReady: view is not ready.");
                return;
            }

            EntityRef<FairyInventoryFormComponent> selfRef = self;
            self.AllClick = context => ApplyCategory(selfRef, FairyInventoryCategory.All);
            self.EquipmentClick = context => ApplyCategory(selfRef, FairyInventoryCategory.Equipment);
            self.ConsumableClick = context => ApplyCategory(selfRef, FairyInventoryCategory.Consumable);
            self.QuestClick = context => ApplyCategory(selfRef, FairyInventoryCategory.Quest);
            self.ItemClick = context => OnItemClick(selfRef, context);
            self.OpenOverlayClick = context => OpenOverlayAsync(selfRef).Forget();
            self.CloseClick = context => CloseInventory(selfRef);

            view.AllButton.onClick.Add(self.AllClick);
            view.EquipmentButton.onClick.Add(self.EquipmentClick);
            view.ConsumableButton.onClick.Add(self.ConsumableClick);
            view.QuestButton.onClick.Add(self.QuestClick);
            view.ItemList.onClickItem.Add(self.ItemClick);
            view.OpenOverlayButton.onClick.Add(self.OpenOverlayClick);
            view.CloseButton.onClick.Add(self.CloseClick);

            self.DetailCountChangedHandler = count => OnDetailCountChanged(selfRef, count);
            FairyInventoryFlow.DetailCountChanged += self.DetailCountChangedHandler;

            ApplyCategory(selfRef, FairyInventoryCategory.All);
            OnDetailCountChanged(selfRef, FairyInventoryFlow.OpenDetailCount);
        }

        [EntitySystem]
        private static void FairyUIFormOnOpen(this FairyInventoryFormComponent self)
        {
            self.OpenData = self.UserData as FairyInventoryOpenData;
            if (self.OpenData == null)
            {
                throw new InvalidOperationException("FairyGUI inventory requires FairyInventoryOpenData.");
            }
        }

        [EntitySystem]
        private static void FairyUIFormOnClose(this FairyInventoryFormComponent self)
        {
            FairyInventoryOpenData openData = self.OpenData;
            UIComponent owner = openData?.Owner;
            FairyInventoryFlow.DetailCountChanged -= self.DetailCountChangedHandler;
            self.DetailCountChangedHandler = null;

            UIInventoryView view = self.View as UIInventoryView;
            if (view != null)
            {
                view.AllButton.onClick.Remove(self.AllClick);
                view.EquipmentButton.onClick.Remove(self.EquipmentClick);
                view.ConsumableButton.onClick.Remove(self.ConsumableClick);
                view.QuestButton.onClick.Remove(self.QuestClick);
                view.ItemList.onClickItem.Remove(self.ItemClick);
                view.OpenOverlayButton.onClick.Remove(self.OpenOverlayClick);
                view.CloseButton.onClick.Remove(self.CloseClick);
                view.ItemList.RemoveChildrenToPool();
            }

            self.AllClick = null;
            self.EquipmentClick = null;
            self.ConsumableClick = null;
            self.QuestClick = null;
            self.ItemClick = null;
            self.OpenOverlayClick = null;
            self.CloseClick = null;
            self.OpenData = null;
            FairyInventoryFlow.CloseAllDetails(owner);
        }

        [EntitySystem]
        private static void FairyUIFormOnPause(this FairyInventoryFormComponent self)
        {
            ++self.PauseCount;
        }

        [EntitySystem]
        private static void FairyUIFormOnResume(this FairyInventoryFormComponent self)
        {
            ++self.ResumeCount;
        }

        [EntitySystem]
        private static void FairyUIFormOnCover(this FairyInventoryFormComponent self)
        {
            ++self.CoverCount;
        }

        [EntitySystem]
        private static void FairyUIFormOnReveal(this FairyInventoryFormComponent self)
        {
            ++self.RevealCount;
        }

        private static void OnItemClick(EntityRef<FairyInventoryFormComponent> selfRef, EventContext context)
        {
            FairyInventoryFormComponent self = selfRef;
            UIInventoryItem item = context.data as UIInventoryItem;
            if (self == null || item?.data is not FairyInventoryItemData itemData)
            {
                return;
            }

            OpenDetailAsync(self, itemData).Forget();
        }

        private static void CloseInventory(EntityRef<FairyInventoryFormComponent> selfRef)
        {
            FairyInventoryFormComponent self = selfRef;
            if (self != null)
            {
                FairyInventoryFlow.Close(self.OpenData);
            }
        }

        private static void OnDetailCountChanged(
            EntityRef<FairyInventoryFormComponent> selfRef,
            int count)
        {
            FairyInventoryFormComponent self = selfRef;
            if (self == null)
            {
                return;
            }

            UIInventoryView view = self.View as UIInventoryView;
            if (view != null)
            {
                view.StatusText.text = $"{count} 个详情窗口（建议同时打开 3 个）";
            }
        }

        private static void ApplyCategory(EntityRef<FairyInventoryFormComponent> selfRef, FairyInventoryCategory category)
        {
            FairyInventoryFormComponent self = selfRef;
            if (self == null)
            {
                return;
            }

            UIInventoryView view = self.View as UIInventoryView;
            if (view == null)
            {
                return;
            }

            view.Category.selectedIndex = (int)category;
            view.ControllerPageText.text = view.Category.selectedPage;
            view.AllButton.selected = category == FairyInventoryCategory.All;
            view.EquipmentButton.selected = category == FairyInventoryCategory.Equipment;
            view.ConsumableButton.selected = category == FairyInventoryCategory.Consumable;
            view.QuestButton.selected = category == FairyInventoryCategory.Quest;

            view.ItemList.RemoveChildrenToPool();
            foreach (DRInventory row in Tables.Instance.DTInventory.DataList)
            {
                FairyInventoryCategory itemCategory = (FairyInventoryCategory)row.Category;
                if (category != FairyInventoryCategory.All && itemCategory != category)
                {
                    continue;
                }

                FairyInventoryItemData itemData = new FairyInventoryItemData(
                    row.Id,
                    row.Name,
                    itemCategory,
                    row.Count,
                    row.Description);

                UIInventoryItem item = view.ItemList.AddItemFromPool() as UIInventoryItem;
                if (item == null)
                {
                    throw new InvalidOperationException("Inventory list default item is not UIInventoryItem.");
                }

                item.data = itemData;
                item.ItemName.text = itemData.Name;
                item.ItemType.text = GetCategoryText(itemData.Category);
                item.ItemCount.text = $"x{itemData.Count}";
            }
        }

        private static string GetCategoryText(FairyInventoryCategory category)
        {
            return category switch
            {
                FairyInventoryCategory.Equipment => "装备",
                FairyInventoryCategory.Consumable => "消耗品",
                FairyInventoryCategory.Quest => "任务物品",
                _ => "全部",
            };
        }

        private static async UniTaskVoid OpenDetailAsync(
            FairyInventoryFormComponent self,
            FairyInventoryItemData itemData)
        {
            try
            {
                UIComponent owner = self.OpenData?.Owner;
                if (owner == null)
                {
                    return;
                }

                await FairyInventoryFlow.OpenDetailAsync(owner, itemData);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }

        private static async UniTaskVoid OpenOverlayAsync(EntityRef<FairyInventoryFormComponent> selfRef)
        {
            try
            {
                FairyInventoryFormComponent self = selfRef;
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
