using System;
using System.Collections.Generic;

namespace Game
{
    public sealed class FairyEntityContainer
    {
        private readonly List<IFairyEntity> m_Entities = new List<IFairyEntity>();

        public int Count => m_Entities.Count;

        public void AddEntity(IFairyEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (m_Entities.Contains(entity))
            {
                throw new InvalidOperationException("FairyGUI entity is already added.");
            }

            m_Entities.Add(entity);
        }

        public void ShowEntity(IFairyEntity entity, object userData = null)
        {
            if (entity == null || !m_Entities.Contains(entity))
            {
                throw new InvalidOperationException("FairyGUI entity is not in this container.");
            }

            entity.OnShow(userData);
        }

        public void HideEntity(IFairyEntity entity, bool isShutdown = false, object userData = null)
        {
            if (entity == null || !m_Entities.Contains(entity))
            {
                return;
            }

            entity.OnHide(isShutdown, userData);
        }

        public void HideAllEntities(bool isShutdown = false, object userData = null)
        {
            for (int i = m_Entities.Count - 1; i >= 0; i--)
            {
                m_Entities[i].OnHide(isShutdown, userData);
            }
        }

        public void RecycleAllEntities()
        {
            for (int i = m_Entities.Count - 1; i >= 0; i--)
            {
                m_Entities[i].OnRecycle();
            }
        }

        public void UpdateAllEntities(float elapseSeconds, float realElapseSeconds)
        {
            foreach (IFairyEntity entity in m_Entities)
            {
                entity.OnUpdate(elapseSeconds, realElapseSeconds);
            }
        }
    }
}
