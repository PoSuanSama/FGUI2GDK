using System;
using System.Collections.Generic;
using FairyGUI;
using GameFramework;

namespace Game
{
    public sealed class FairyUIWidgetContainer : IReference
    {
        private readonly List<IFairyUIWidget> m_Widgets = new List<IFairyUIWidget>();

        public GComponent Owner { get; private set; }

        public int Count => m_Widgets.Count;

        public static FairyUIWidgetContainer Create(GComponent owner)
        {
            FairyUIWidgetContainer container = ReferencePool.Acquire<FairyUIWidgetContainer>();
            container.Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            return container;
        }

        public void Clear()
        {
            m_Widgets.Clear();
            Owner = null;
        }

        public void AddWidget(IFairyUIWidget widget, object userData = null)
        {
            if (widget == null)
            {
                throw new ArgumentNullException(nameof(widget));
            }

            if (m_Widgets.Contains(widget))
            {
                throw new InvalidOperationException("FairyGUI widget is already added.");
            }

            m_Widgets.Add(widget);
            widget.OnInit(userData);
        }

        public bool HasWidget(IFairyUIWidget widget) => m_Widgets.Contains(widget);

        public void OpenWidget(IFairyUIWidget widget, object userData = null)
        {
            if (widget == null || !m_Widgets.Contains(widget))
            {
                throw new InvalidOperationException("FairyGUI widget is not in this container.");
            }

            if (widget.Opened)
            {
                throw new InvalidOperationException("FairyGUI widget is already opened.");
            }

            if (widget.View != null && widget.View.parent != Owner)
            {
                Owner.AddChild(widget.View);
            }

            widget.OnOpen(userData);
        }

        public void CloseWidget(IFairyUIWidget widget, bool isShutdown = false, object userData = null)
        {
            if (widget == null || !m_Widgets.Contains(widget) || !widget.Opened)
            {
                return;
            }

            widget.OnClose(isShutdown, userData);
        }

        public void CloseAllWidgets(bool isShutdown = false, object userData = null)
        {
            for (int i = m_Widgets.Count - 1; i >= 0; i--)
            {
                if (m_Widgets[i].Opened)
                {
                    m_Widgets[i].OnClose(isShutdown, userData);
                }
            }
        }

        public void RecycleAllWidgets()
        {
            for (int i = m_Widgets.Count - 1; i >= 0; i--)
            {
                m_Widgets[i].OnRecycle();
            }
        }

        public void PauseAllWidgets()
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                if (widget.Opened)
                {
                    widget.OnPause();
                }
            }
        }

        public void ResumeAllWidgets()
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                if (widget.Opened)
                {
                    widget.OnResume();
                }
            }
        }

        public void CoverAllWidgets()
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                if (widget.Opened)
                {
                    widget.OnCover();
                }
            }
        }

        public void RevealAllWidgets()
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                if (widget.Opened)
                {
                    widget.OnReveal();
                }
            }
        }

        public void RefocusAllWidgets(object userData)
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                if (widget.Opened)
                {
                    widget.OnRefocus(userData);
                }
            }
        }

        public void UpdateAllWidgets(float elapseSeconds, float realElapseSeconds)
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                if (widget.Opened)
                {
                    widget.OnUpdate(elapseSeconds, realElapseSeconds);
                }
            }
        }

        public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                if (widget.Opened)
                {
                    widget.OnDepthChanged(uiGroupDepth, depthInUIGroup);
                }
            }
        }

        public void Dispose()
        {
            ReferencePool.Release(this);
        }
    }
}
