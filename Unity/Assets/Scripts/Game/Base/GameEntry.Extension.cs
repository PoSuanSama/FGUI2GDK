using UnityGameFramework.Extension;

namespace Game
{
    /// <summary>
    /// 游戏入口。
    /// </summary>
    public partial class GameEntry
    {
        public static CodeRunnerComponent CodeRunner
        {
            get;
            private set;
        }

        public static NetworkServiceComponent NetworkService
        {
            get;
            private set;
        }

        private static void InitExtensionComponents()
        {
            CodeRunner = UnityGameFramework.Runtime.GameEntry.GetComponent<CodeRunnerComponent>();
            NetworkService = UnityGameFramework.Runtime.GameEntry.GetComponent<NetworkServiceComponent>();
        }
    }
}
