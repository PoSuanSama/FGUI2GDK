using System;
using System.Collections.Generic;

namespace Game
{
    public sealed class FairyGuide
    {
        private readonly List<FairyGuideStep> m_Steps = new List<FairyGuideStep>();
        private int m_CurrentIndex;

        public event Action<FairyGuideStep> StepStarted;
        public event Action<FairyGuideStep> StepFinished;
        public event Action Completed;

        public bool Active { get; private set; }

        public int CurrentIndex => m_CurrentIndex;

        public FairyGuideStep CurrentStep =>
            Active && m_CurrentIndex >= 0 && m_CurrentIndex < m_Steps.Count
                ? m_Steps[m_CurrentIndex]
                : null;

        public void AddStep(FairyGuideStep step)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            m_Steps.Add(step);
        }

        public void Begin()
        {
            if (m_Steps.Count == 0)
            {
                throw new InvalidOperationException("FairyGuide has no steps.");
            }

            m_CurrentIndex = 0;
            Active = true;
            StepStarted?.Invoke(CurrentStep);
        }

        public void Next()
        {
            if (!Active)
            {
                return;
            }

            StepFinished?.Invoke(CurrentStep);

            if (m_CurrentIndex + 1 >= m_Steps.Count)
            {
                Active = false;
                Completed?.Invoke();
                return;
            }

            m_CurrentIndex++;
            StepStarted?.Invoke(CurrentStep);
        }

        public void Close()
        {
            if (!Active)
            {
                return;
            }

            StepFinished?.Invoke(CurrentStep);
            Active = false;
            Completed?.Invoke();
        }
    }
}
