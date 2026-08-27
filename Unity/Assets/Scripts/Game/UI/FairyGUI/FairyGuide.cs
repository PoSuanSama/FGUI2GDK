using System;
using System.Collections.Generic;
using GameFramework;

namespace Game
{
    public sealed class FairyGuide
    {
        private readonly List<FairyGuideStep> m_Steps = new List<FairyGuideStep>();
        private ResourceContainer m_Resources;
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

            EnsureResources();
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

        public void LoadResource<T>(string assetName, Action<T> onLoadSuccess, Action onLoadFailure = null, int priority = 0,
            Action<float> updateEvent = null, Action<string> dependencyAssetEvent = null) where T : UnityEngine.Object
        {
            EnsureResources();
            m_Resources.LoadAsset(assetName, onLoadSuccess, onLoadFailure, priority, updateEvent, dependencyAssetEvent);
        }

        public void UnloadResource(UnityEngine.Object asset)
        {
            m_Resources?.UnloadAsset(asset);
        }

        public void Dispose()
        {
            Close();
            m_Resources?.UnloadAllAssets(false);
            m_Resources?.Clear();
            m_Resources = null;

            foreach (FairyGuideStep step in m_Steps)
            {
                step.Dispose();
            }

            m_Steps.Clear();
        }

        private void EnsureResources()
        {
            if (m_Resources == null)
            {
                m_Resources = ResourceContainer.Create(this);
            }
        }
    }
}
