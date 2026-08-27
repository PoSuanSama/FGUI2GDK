using Game;
using MongoDB.Bson.Serialization.Attributes;

namespace ET.Client
{
    [ComponentOf(typeof(UIComponent))]
    public class FairyUIWidgetComponent : Entity, IAwake<Game.FairyUIWidget>, IDestroy
    {
        [BsonIgnore]
        public Game.FairyUIWidget Widget { get; set; }
    }
}
