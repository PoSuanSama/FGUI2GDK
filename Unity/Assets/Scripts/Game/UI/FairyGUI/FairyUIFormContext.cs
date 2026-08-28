using System;
using FairyGUI;
using GameFramework.UI;

namespace Game
{
    /// <summary>
    /// 单个 FairyGUI 窗体的上下文所有者(design.md §8)。
    ///
    /// 与原 AUIForm 的随界面清理语义对齐:
    /// - Widget 容器由宿主(FairyUIForm)持有并自动级联 Pause/Cover/Refocus/Update 生命周期;
    /// - EventContainer/ResourceContainer 在窗体关闭时统一退订/卸载,业务不需要手动对称清理;
    /// - Presenter 经 <see cref="IFairyUIPresenter.OnViewReady"/> 拿到本上下文,不再自行持有容器。
    ///
    /// 生命周期:打开流程在 OnViewReady 前创建(serialId 尚未分配,为 0);
    /// FairyUIForm.OnInit 采纳后回填 serialId/UIId/分组等元数据;
    /// FairyUIForm.Release 调用 <see cref="Clear"/> 统一释放,窗体池化复用后由下一次 OnInit 重建。
    /// </summary>
    public sealed class FairyUIFormContext
    {
        private FairyUIWidgetContainer m_Widgets;
        private EventContainer m_Events;
        private ResourceContainer m_Resources;

        public GComponent View { get; internal set; }

        public FairyUIForm Form { get; internal set; }

        /// <summary>
        /// 当前窗体的 UI ID;尚未采纳 pending state 时为 0。
        /// </summary>
        public int UIId { get; internal set; }

        /// <summary>
        /// 当前窗体的 GF serial ID;GF 打开完成前为 0。
        /// </summary>
        public int SerialId { get; internal set; }

        public string UIGroupName { get; internal set; }

        public bool PauseCoveredUIForm { get; internal set; }

        /// <summary>
        /// 窗体的 Widget 容器(懒创建,owner 为当前视图)。宿主在生命周期回调里自动级联。
        /// </summary>
        public FairyUIWidgetContainer Widgets
        {
            get
            {
                if (m_Widgets == null)
                {
                    if (View == null)
                    {
                        throw new InvalidOperationException(
                            "FairyGUI form context has no view, cannot create widget container.");
                    }

                    m_Widgets = FairyUIWidgetContainer.Create(View);
                }

                return m_Widgets;
            }
        }

        /// <summary>
        /// 随窗体清理的事件订阅容器(懒创建,owner 为本上下文)。
        /// </summary>
        public EventContainer Events
        {
            get
            {
                m_Events ??= EventContainer.Create(this);
                return m_Events;
            }
        }

        /// <summary>
        /// 随窗体清理的资源容器(懒创建,owner 为本上下文)。
        /// </summary>
        public ResourceContainer Resources
        {
            get
            {
                m_Resources ??= ResourceContainer.Create(this);
                return m_Resources;
            }
        }

        public bool HasWidgets => m_Widgets != null;

        /// <summary>
        /// 宿主 Release 时调用:回收 Widget、退订事件、释放资源,并清空元数据。
        /// 不负责释放 <see cref="View"/>——视图由宿主自身的清理路径处理。
        /// </summary>
        internal void Clear()
        {
            // 释放顺序与 FairyEntity.OnRecycle 一致:先业务清理,再清引用。
            if (m_Widgets != null)
            {
                m_Widgets.RecycleAllWidgets();
                m_Widgets.Dispose();
                m_Widgets = null;
            }

            if (m_Events != null)
            {
                m_Events.UnsubscribeAll(false);
                m_Events.Clear();
                m_Events = null;
            }

            if (m_Resources != null)
            {
                m_Resources.UnloadAllAssets(false);
                m_Resources.Clear();
                m_Resources = null;
            }

            View = null;
            Form = null;
            UIId = 0;
            SerialId = 0;
            UIGroupName = null;
            PauseCoveredUIForm = true;
        }
    }
}
