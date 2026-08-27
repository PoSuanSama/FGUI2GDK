using FairyGUI;
using Game.FairyGUI.Package1;

namespace Game
{
    public sealed class FairyInventoryItemWidget : FairyUIWidget
    {
        public static FairyInventoryItemWidget Create()
        {
            FairyInventoryItemWidget widget = new FairyInventoryItemWidget();
            GComponent view = UIPackage.CreateObject("Package1", "InventoryItem") as GComponent;
            if (view == null)
            {
                throw new GameFramework.GameFrameworkException("Failed to create FairyGUI InventoryItem widget.");
            }

            widget.SetView(view);
            return widget;
        }
    }
}
