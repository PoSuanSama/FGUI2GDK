using Game;

namespace ET.Client
{
    [FriendOf(typeof(FairyUIWidgetComponent))]
    [EntitySystemOf(typeof(FairyUIWidgetComponent))]
    public static partial class FairyUIWidgetComponentSystem
    {
        [EntitySystem]
        private static void Awake(this FairyUIWidgetComponent self, Game.FairyUIWidget widget)
        {
            self.Widget = widget;
        }

        [EntitySystem]
        private static void Destroy(this FairyUIWidgetComponent self)
        {
            self.Widget = null;
        }
    }
}
