using System;
using GameFramework;
using GameFramework.UI;

namespace Game
{
    /// <summary>
    /// FairyGUI 原生的界面辅助器：窗口实例就是 FairyUIForm，不再创建 UGUI GameObject 壳。
    /// 真正的视图/Presenter/包租约在 OnInit 里经 serialId 从 pending registry 采纳。
    /// </summary>
    public sealed class FairyUIFormHelper : IUIFormHelper
    {
        private readonly Action<object> m_ReleaseAsset;

        public FairyUIFormHelper(Action<object> releaseAsset)
        {
            m_ReleaseAsset = releaseAsset;
        }

        public object InstantiateUIForm(object uiFormAsset)
        {
            return new FairyUIForm();
        }

        public IUIForm CreateUIForm(object uiFormInstance, IUIGroup uiGroup, object userData)
        {
            return uiFormInstance as FairyUIForm
                ?? throw new GameFrameworkException("FairyUI form instance is invalid.");
        }

        public void ReleaseUIForm(object uiFormAsset, object uiFormInstance)
        {
            m_ReleaseAsset?.Invoke(uiFormAsset);
        }
    }
}