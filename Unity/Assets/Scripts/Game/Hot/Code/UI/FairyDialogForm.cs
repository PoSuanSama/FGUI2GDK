using System;
using FairyGUI;
using Game.FairyGUI.Package1;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    /// <summary>
    /// DialogForm 的 GameHot Presenter(阶段 E 批次):
    /// 三模式对话框(仅确认/确认+取消/确认+取消+中立),与旧 UGUI DialogParams 契约等价。
    /// 按钮文本由 DialogParams 提供(缺省用组件内置标题),回调触发后关闭窗体。
    /// </summary>
    [FairyUIPresenter(UIFormId.DialogForm)]
    public sealed class FairyDialogForm : IFairyUIPresenter
    {
        private const int ConfirmMode = 1;
        private const int CancelMode = 2;
        private const int OtherMode = 3;

        private UIDialog m_View;
        private FairyUIFormContext m_Context;
        private DialogParams m_DialogParams;
        private bool m_PauseGame;
        private object m_UserData;
        private GameFramework.GameFrameworkAction<object> m_OnClickConfirm;
        private GameFramework.GameFrameworkAction<object> m_OnClickCancel;
        private GameFramework.GameFrameworkAction<object> m_OnClickOther;

        public int DialogMode => m_DialogParams?.Mode ?? ConfirmMode;

        public void OnViewReady(FairyUIFormContext context)
        {
            m_View = context.View as UIDialog;
            if (m_View == null)
            {
                throw new InvalidOperationException(
                    $"FairyGUI dialog requires '{typeof(UIDialog).FullName}', found '{context?.View?.GetType().FullName}'.");
            }

            // context.Form 在 GF OpenUIForm(OnInit)之后才回填,
            // OnViewReady 阶段还不能读;保存 context,关闭时再取。
            m_Context = context;
            m_View.ConfirmButton.onClick.Add(OnConfirmClick);
            m_View.CancelButton.onClick.Add(OnCancelClick);
            m_View.OtherButton.onClick.Add(OnOtherClick);
        }

        public void OnOpen(object userData)
        {
            m_DialogParams = userData as DialogParams;
            if (m_DialogParams == null)
            {
                throw new InvalidOperationException("FairyGUI dialog requires DialogParams.");
            }

            m_View.TitleText.text = m_DialogParams.Title ?? string.Empty;
            m_View.MessageText.text = m_DialogParams.Message ?? string.Empty;
            RefreshMode();

            m_PauseGame = m_DialogParams.PauseGame;
            RefreshPauseGame();

            m_UserData = m_DialogParams.UserData;

            RefreshConfirmText(m_DialogParams.ConfirmText);
            m_OnClickConfirm = m_DialogParams.OnClickConfirm;

            RefreshCancelText(m_DialogParams.CancelText);
            m_OnClickCancel = m_DialogParams.OnClickCancel;

            RefreshOtherText(m_DialogParams.OtherText);
            m_OnClickOther = m_DialogParams.OnClickOther;
        }

        public void OnClose(bool isShutdown, object userData)
        {
            if (m_PauseGame)
            {
                GameEntry.Base.ResumeGame();
            }

            if (m_View != null)
            {
                m_View.ConfirmButton.onClick.Remove(OnConfirmClick);
                m_View.CancelButton.onClick.Remove(OnCancelClick);
                m_View.OtherButton.onClick.Remove(OnOtherClick);
                m_View = null;
            }

            m_Context = null;
            m_DialogParams = null;
            m_PauseGame = false;
            m_UserData = null;
            m_OnClickConfirm = null;
            m_OnClickCancel = null;
            m_OnClickOther = null;
        }

        public void OnPause()
        {
        }

        public void OnResume()
        {
        }

        public void OnCover()
        {
        }

        public void OnReveal()
        {
        }

        public void OnRefocus(object userData)
        {
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        private void RefreshMode()
        {
            switch (m_DialogParams.Mode)
            {
                case ConfirmMode:
                    m_View.ConfirmButton.visible = true;
                    m_View.CancelButton.visible = false;
                    m_View.OtherButton.visible = false;
                    break;
                case CancelMode:
                    m_View.ConfirmButton.visible = true;
                    m_View.CancelButton.visible = true;
                    m_View.OtherButton.visible = false;
                    break;
                default:
                    m_View.ConfirmButton.visible = true;
                    m_View.CancelButton.visible = true;
                    m_View.OtherButton.visible = true;
                    break;
            }
        }

        private void RefreshPauseGame()
        {
            if (m_PauseGame)
            {
                GameEntry.Base.PauseGame();
            }
        }

        private void RefreshConfirmText(string confirmText)
        {
            if (!string.IsNullOrEmpty(confirmText))
            {
                m_View.ConfirmButton.title = confirmText;
            }
        }

        private void RefreshCancelText(string cancelText)
        {
            if (!string.IsNullOrEmpty(cancelText))
            {
                m_View.CancelButton.title = cancelText;
            }
        }

        private void RefreshOtherText(string otherText)
        {
            if (!string.IsNullOrEmpty(otherText))
            {
                m_View.OtherButton.title = otherText;
            }
        }

        private void OnConfirmClick(EventContext context)
        {
            m_OnClickConfirm?.Invoke(m_UserData);
            FairyDialogFlow.Close(m_Context?.Form);
        }

        private void OnCancelClick(EventContext context)
        {
            m_OnClickCancel?.Invoke(m_UserData);
            FairyDialogFlow.Close(m_Context?.Form);
        }

        private void OnOtherClick(EventContext context)
        {
            m_OnClickOther?.Invoke(m_UserData);
            FairyDialogFlow.Close(m_Context?.Form);
        }
    }
}
