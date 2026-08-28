using FairyGUI;
using Game;
using MemoryPack;
using MongoDB.Bson.Serialization.Attributes;

namespace ET.Client
{
    /// <summary>
    /// ET 侧 FairyGUI 界面的 Entity 状态基类。
    ///
    /// 与原 UGFUIForm(8b39d6cc 删除前)同构:状态保存在非热更的 ModelView,
    /// 行为由 HotfixView 的 static System 提供,经 <see cref="FairyUIFormSystemDispatcher"/>
    /// 运行时派发。Hotfix 程序集不允许声明字段/属性(ET0004),因此状态必须留在此处。
    ///
    /// 实例作为 UIComponent 的子 Entity 持有,由 ET 打开流程创建;
    /// 生命周期数据(userData / isShutdown)由 <see cref="FairyUIPresenterAdapter"/> 写入。
    /// </summary>
    [EnableMethod]
    public class FairyUIFormComponent : Entity
    {
        [BsonIgnore]
        [MemoryPackIgnore]
        public FairyUIForm FairyForm { get; set; }

        [BsonIgnore]
        [MemoryPackIgnore]
        public GComponent View { get; set; }

        [BsonIgnore]
        [MemoryPackIgnore]
        public object UserData { get; set; }

        [BsonIgnore]
        [MemoryPackIgnore]
        public bool IsShutdown { get; set; }

        /// <summary>
        /// 当前窗体的 GF serial ID;尚未打开时为 0。
        /// </summary>
        public int SerialId => FairyForm?.SerialId ?? 0;

        public bool Available => FairyForm != null && !IsDisposed;
    }
}
