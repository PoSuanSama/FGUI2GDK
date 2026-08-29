using System;
using Game;
using UnityGameFramework.Runtime;

namespace ET
{
#if UNITY_EDITOR
    public class UnityTimeNow : ITimeNow
    {
        private long m_UtcRealTicks;
        private long m_UtcNowTicks;
        private float m_GameSpeed;
        
        public UnityTimeNow()
        {
            BaseComponent baseComponent = Game.GameEntry.Base;
            this.m_GameSpeed = baseComponent != null ? baseComponent.GameSpeed : 1f;
            this.m_UtcNowTicks = this.m_UtcRealTicks = DateTime.UtcNow.Ticks;
        }
        
        //要保证线程安全
        public long GetUtcNowTicks()
        {
            return (DateTime.UtcNow.Ticks - this.m_UtcRealTicks) * (long)(this.m_GameSpeed * 10000000) / 10000000 + this.m_UtcNowTicks;
        }

        public void Update()
        {
            BaseComponent baseComponent = Game.GameEntry.Base;
            if (baseComponent != null)
            {
                this.m_GameSpeed = baseComponent.GameSpeed;
            }

            this.m_UtcNowTicks = GetUtcNowTicks();
            this.m_UtcRealTicks = DateTime.UtcNow.Ticks;
        }
    }
#else
    public class UnityTimeNow : ITimeNow
    {
        public long GetUtcNowTicks()
        {
            return DateTime.UtcNow.Ticks;
        }

        public void Update()
        {
            
        }
    }
#endif
}