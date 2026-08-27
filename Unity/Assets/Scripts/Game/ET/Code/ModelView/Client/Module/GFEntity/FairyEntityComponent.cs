using Game;
using MongoDB.Bson.Serialization.Attributes;

namespace ET.Client
{
    [ComponentOf(typeof(Scene))]
    public class FairyEntityComponent : Entity, IAwake<Game.FairyEntity>, IDestroy
    {
        [BsonIgnore]
        public Game.FairyEntity Entity { get; set; }
    }
}
