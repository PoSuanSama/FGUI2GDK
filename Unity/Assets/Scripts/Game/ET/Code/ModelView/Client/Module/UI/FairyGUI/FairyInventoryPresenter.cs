using System;
using FairyGUI;
using Game;
using Game.FairyGUI.Package1;

namespace ET.Client
{
    [FairyUIPresenter(UGFUIFormId.FairyInventoryForm)]
    [global::ET.EnableClass]
    public sealed class FairyInventoryPresenter : IFairyUIPresenter
    {
        private FairyInventoryOpenData m_OpenData;
        private UIInventoryView m_View;

        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }
        public int CoverCount { get; private set; }
        public int RevealCount { get; private set; }

        public void OnViewReady(GComponent view)
        {
            m_View = view as UIInventoryView;
            if (m_View == null)
            {
                throw new InvalidOperationException(
                    $"FairyGUI inventory requires '{typeof(UIInventoryView).FullName}', found '{view?.GetType().FullName}'.");
            }

            m_View.AllButton.onClick.Add(OnAllClick);
            m_View.EquipmentButton.onClick.Add(OnEquipmentClick);
            m_View.ConsumableButton.onClick.Add(OnConsumableClick);
            m_View.QuestButton.onClick.Add(OnQuestClick);
            m_View.ItemList.onClickItem.Add(OnItemClick);
            m_View.OpenOverlayButton.onClick.Add(OnOpenOverlayClick);
            m_View.CloseButton.onClick.Add(OnCloseClick);
            FairyInventoryFlow.DetailCountChanged += OnDetailCountChanged;

            ApplyCategory(FairyInventoryCategory.All);
            OnDetailCountChanged(FairyInventoryFlow.OpenDetailCount);
        }

        public void OnOpen(object userData)
        {
            m_OpenData = userData as FairyInventoryOpenData;
            if (m_OpenData == null)
            {
                throw new InvalidOperationException("FairyGUI inventory requires FairyInventoryOpenData.");
            }
        }

        public void OnClose(bool isShutdown, object userData)
        {
            FairyInventoryFlow.DetailCountChanged -= OnDetailCountChanged;
            if (m_View != null)
            {
                m_View.AllButton.onClick.Remove(OnAllClick);
                m_View.EquipmentButton.onClick.Remove(OnEquipmentClick);
                m_View.ConsumableButton.onClick.Remove(OnConsumableClick);
                m_View.QuestButton.onClick.Remove(OnQuestClick);
                m_View.ItemList.onClickItem.Remove(OnItemClick);
                m_View.OpenOverlayButton.onClick.Remove(OnOpenOverlayClick);
                m_View.CloseButton.onClick.Remove(OnCloseClick);
                m_View.ItemList.RemoveChildrenToPool();
                m_View = null;
            }

            m_OpenData = null;
            FairyInventoryFlow.CloseAllDetails();
        }

        public void OnPause()
        {
            ++PauseCount;
        }

        public void OnResume()
        {
            ++ResumeCount;
        }

        public void OnCover()
        {
            ++CoverCount;
        }

        public void OnReveal()
        {
            ++RevealCount;
        }

        public void OnRefocus(object userData)
        {
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        private void OnAllClick()
        {
            ApplyCategory(FairyInventoryCategory.All);
        }

        private void OnEquipmentClick()
        {
            ApplyCategory(FairyInventoryCategory.Equipment);
        }

        private void OnConsumableClick()
        {
            ApplyCategory(FairyInventoryCategory.Consumable);
        }

        private void OnQuestClick()
        {
            ApplyCategory(FairyInventoryCategory.Quest);
        }

        private void OnItemClick(EventContext context)
        {
            UIInventoryItem item = context.data as UIInventoryItem;
            if (item?.data is not FairyInventoryItemData itemData)
            {
                return;
            }

            OpenDetailAsync(itemData).Forget();
        }

        private void OnOpenOverlayClick()
        {
            OpenOverlayAsync().Forget();
        }

        private void OnCloseClick()
        {
            FairyInventoryFlow.Close(m_OpenData);
        }

        private void ApplyCategory(FairyInventoryCategory category)
        {
            m_View.Category.selectedIndex = (int)category;
            m_View.ControllerPageText.text = m_View.Category.selectedPage;
            m_View.AllButton.selected = category == FairyInventoryCategory.All;
            m_View.EquipmentButton.selected = category == FairyInventoryCategory.Equipment;
            m_View.ConsumableButton.selected = category == FairyInventoryCategory.Consumable;
            m_View.QuestButton.selected = category == FairyInventoryCategory.Quest;

            m_View.ItemList.RemoveChildrenToPool();
            foreach (DRInventory row in ET.Tables.Instance.DTInventory.DataList)
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

                UIInventoryItem item = m_View.ItemList.AddItemFromPool() as UIInventoryItem;
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

        private void OnDetailCountChanged(int count)
        {
            if (m_View != null)
            {
                m_View.StatusText.text = $"{count} 个详情窗口（建议同时打开 3 个）";
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

        private static async Cysharp.Threading.Tasks.UniTaskVoid OpenDetailAsync(
            FairyInventoryItemData itemData)
        {
            try
            {
                await FairyInventoryFlow.OpenDetailAsync(itemData);
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }

        private static async Cysharp.Threading.Tasks.UniTaskVoid OpenOverlayAsync()
        {
            try
            {
                await FairyInventoryFlow.OpenOverlayAsync();
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }
    }
}
