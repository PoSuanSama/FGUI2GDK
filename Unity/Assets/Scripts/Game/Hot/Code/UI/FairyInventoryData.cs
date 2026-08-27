using System;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    public enum FairyInventoryCategory
    {
        All,
        Equipment,
        Consumable,
        Quest,
    }

    public sealed class FairyInventoryItemData
    {
        public FairyInventoryItemData(
            int id,
            string name,
            FairyInventoryCategory category,
            int count,
            string description)
        {
            Id = id;
            Name = name;
            Category = category;
            Count = count;
            Description = description;
        }

        public int Id { get; }
        public string Name { get; }
        public FairyInventoryCategory Category { get; }
        public int Count { get; }
        public string Description { get; }
    }

    public class FairyFormInstanceData
    {
        public UIForm UIForm { get; private set; }

        public void Attach(UIForm uiForm)
        {
            UIForm = uiForm ?? throw new ArgumentNullException(nameof(uiForm));
        }
    }

    public sealed class FairyInventoryOpenData : FairyFormInstanceData
    {
    }

    public sealed class FairyItemDetailOpenData : FairyFormInstanceData
    {
        private bool m_Closed;

        public FairyItemDetailOpenData(FairyInventoryItemData item, int token)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Token = token;
        }

        public FairyInventoryItemData Item { get; }
        public int Token { get; }

        public bool TryMarkClosed()
        {
            if (m_Closed)
            {
                return false;
            }

            m_Closed = true;
            return true;
        }
    }

    public sealed class FairyInventoryOverlayOpenData : FairyFormInstanceData
    {
    }
}
