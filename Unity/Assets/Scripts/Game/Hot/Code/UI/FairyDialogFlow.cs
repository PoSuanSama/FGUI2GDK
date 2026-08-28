using System;
using Cysharp.Threading.Tasks;

namespace Game.Hot
{
    /// <summary>
    /// DialogForm 打开/关闭流(阶段 E 批次):
    /// 多实例(multi=true)允许同时存在多个对话框;每次打开传独立 DialogParams。
    /// </summary>
    public static class FairyDialogFlow
    {
        public static async UniTask<FairyUIForm> OpenAsync(DialogParams dialogParams)
        {
            if (dialogParams == null)
            {
                throw new ArgumentNullException(nameof(dialogParams));
            }

            FairyUIForm uiForm = await FairyUIFormService.OpenFairyUIFormAsync(
                UIFormId.DialogForm,
                dialogParams);
            return uiForm;
        }

        /// <summary>
        /// 回调后关闭:Presenter 在触发用户回调后调用。
        /// </summary>
        public static void Close(FairyUIForm uiForm)
        {
            if (uiForm == null)
            {
                return;
            }

            if (FairyUIManager.Instance.HasUIForm(uiForm.SerialId))
            {
                FairyUIManager.Instance.CloseUIForm(uiForm.SerialId);
            }
        }
    }
}
