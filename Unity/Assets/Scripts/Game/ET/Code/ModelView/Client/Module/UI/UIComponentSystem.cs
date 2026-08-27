namespace ET.Client
{
    [FriendOf(typeof(UIComponent))]
    [EntitySystemOf(typeof(UIComponent))]
    public static partial class UIComponentSystem
    {
        [EntitySystem]
        private static void Awake(this UIComponent self)
        {
        }

        [EntitySystem]
        private static void Destroy(this UIComponent self)
        {
        }
    }
}
