using System;
using System.Collections.Generic;
using FairyGUI;

namespace Game
{
    public sealed class FairyUIWidgetContainer
    {
        private readonly List<IFairyUIWidget> m_Widgets = new List<IFairyUIWidget>();
        private readonly GComponent m_Owner;

        public FairyUIWidgetContainer(GComponent owner)
        {
            m_Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public int Count => m_Widgets.Count;

        public void AddWidget(IFairyUIWidget widget)
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
        }

        public void OpenWidget(IFairyUIWidget widget, object userData = null)
        {
            if (widget == null || !m_Widgets.Contains(widget))
            {
                throw new InvalidOperationException("FairyGUI widget is not in this container.");
            }

            if (widget.View != null && widget.View.parent != m_Owner)
            {
                m_Owner.AddChild(widget.View);
            }

            widget.OnOpen(userData);
        }

        public void CloseWidget(IFairyUIWidget widget, bool isShutdown = false, object userData = null)
        {
            if (widget == null || !m_Widgets.Contains(widget))
            {
                return;
            }

            widget.OnClose(isShutdown, userData);
        }

        public void CloseAllWidgets(bool isShutdown = false, object userData = null)
        {
            for (int i = m_Widgets.Count - 1; i >= 0; i--)
            {
                m_Widgets[i].OnClose(isShutdown, userData);
            }
        }

        public void PauseAllWidgets()
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                widget.OnPause();
            }
        }

        public void ResumeAllWidgets()
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                widget.OnResume();
            }
        }

        public void CoverAllWidgets()
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                widget.OnCover();
            }
        }

        public void RevealAllWidgets()
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                widget.OnReveal();
            }
        }

        public void RefocusAllWidgets(object userData)
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                widget.OnRefocus(userData);
            }
        }

        public void UpdateAllWidgets(float elapseSeconds, float realElapseSeconds)
        {
            foreach (IFairyUIWidget widget in m_Widgets)
            {
                widget.OnUpdate(elapseSeconds, realElapseSeconds);
            }
        }
    }
}
