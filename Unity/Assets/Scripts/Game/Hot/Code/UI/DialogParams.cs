using GameFramework;

namespace Game.Hot
{
    /// <summary>
    /// 对话框显示数据(与旧 UGUI DialogParams 契约等价)。
    /// </summary>
    public class DialogParams
    {
        /// <summary>
        /// 模式,即按钮数量。取值 1(仅确认)、2(确认+取消)、3(确认+取消+中立)。
        /// </summary>
        public int Mode
        {
            get;
            set;
        }

        public string Title
        {
            get;
            set;
        }

        public string Message
        {
            get;
            set;
        }

        /// <summary>
        /// 弹出窗口时是否暂停游戏。
        /// </summary>
        public bool PauseGame
        {
            get;
            set;
        }

        public string ConfirmText
        {
            get;
            set;
        }

        public GameFrameworkAction<object> OnClickConfirm
        {
            get;
            set;
        }

        public string CancelText
        {
            get;
            set;
        }

        public GameFrameworkAction<object> OnClickCancel
        {
            get;
            set;
        }

        public string OtherText
        {
            get;
            set;
        }

        public GameFrameworkAction<object> OnClickOther
        {
            get;
            set;
        }

        public string UserData
        {
            get;
            set;
        }
    }
}
