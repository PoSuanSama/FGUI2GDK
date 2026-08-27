using System;

namespace ET.Client
{
    public enum FairyInventoryCategory
    {
        All,
        Equipment,
        Consumable,
        Quest,
    }

    [global::ET.EnableClass]
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

    [global::ET.EnableClass]
    public class FairyFormInstanceData
    {
        public Game.FairyUIForm UIForm { get; private set; }

        public void Attach(Game.FairyUIForm uiForm)
        {
            UIForm = uiForm ?? throw new ArgumentNullException(nameof(uiForm));
        }
    }

    [global::ET.EnableClass]
    public sealed class FairyInventoryOpenData : FairyFormInstanceData
    {
    }

    [global::ET.EnableClass]
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

    [global::ET.EnableClass]
    public sealed class FairyInventoryOverlayOpenData : FairyFormInstanceData
    {
    }
}
