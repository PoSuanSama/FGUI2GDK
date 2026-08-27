using System;
using System.Collections.Generic;
using GameFramework;

namespace Game
{
    public sealed class FairyEntityContainer : IReference
    {
        private readonly List<IFairyEntity> m_Entities = new List<IFairyEntity>();

        public IFairyEntity Owner { get; private set; }

        public int Count => m_Entities.Count;

        public static FairyEntityContainer Create(IFairyEntity owner)
        {
            FairyEntityContainer container = ReferencePool.Acquire<FairyEntityContainer>();
            container.Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            return container;
        }

        public void Clear()
        {
            m_Entities.Clear();
            Owner = null;
        }

        public void AddEntity(IFairyEntity entity, object userData = null)
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
            entity.OnInit(userData);
        }

        public bool HasEntity(IFairyEntity entity) => m_Entities.Contains(entity);

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
            if (entity == null || !m_Entities.Contains(entity) || !entity.Available)
            {
                return;
            }

            entity.OnHide(isShutdown, userData);
        }

        public void HideAllEntities(bool isShutdown = false, object userData = null)
        {
            for (int i = m_Entities.Count - 1; i >= 0; i--)
            {
                if (m_Entities[i].Available)
                {
                    m_Entities[i].OnHide(isShutdown, userData);
                }
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
                if (entity.Available)
                {
                    entity.OnUpdate(elapseSeconds, realElapseSeconds);
                }
            }
        }

        public void Dispose()
        {
            ReferencePool.Release(this);
        }
    }
}
