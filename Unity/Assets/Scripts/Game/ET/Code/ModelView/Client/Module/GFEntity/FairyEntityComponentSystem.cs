using Game;

namespace ET.Client
{
    [FriendOf(typeof(FairyEntityComponent))]
    [EntitySystemOf(typeof(FairyEntityComponent))]
    public static partial class FairyEntityComponentSystem
    {
        [EntitySystem]
        private static void Awake(this FairyEntityComponent self, Game.FairyEntity entity)
        {
            self.Entity = entity;
        }

        [EntitySystem]
        private static void Destroy(this FairyEntityComponent self)
        {
            self.Entity = null;
        }
    }
}
