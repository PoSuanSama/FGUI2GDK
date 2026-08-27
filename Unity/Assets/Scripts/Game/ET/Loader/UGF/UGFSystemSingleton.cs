using System;
using System.Collections.Generic;
using UnityEngine;

namespace ET
{
    [Code]
    public class UGFSystemSingleton : Singleton<UGFSystemSingleton>, ISingletonAwake
    {
        private TypeSystems m_TypeSystems { get; set; }

        private readonly DoubleMap<Type, long> m_UGFTypeLongHashCode = new();
        private readonly DoubleMap<Type, Type> m_MonoTypeWidgetType = new();
        
        public void Awake()
        {
            this.m_TypeSystems = new(InstanceQueueIndex.Max);
            foreach (Type type in CodeTypes.Instance.GetTypes(typeof (UGFEntitySystemAttribute)))
            {
                SystemObject obj = (SystemObject)Activator.CreateInstance(type);

                if (obj is not ISystemType iSystemType)
                {
                    continue;
                }

                TypeSystems.OneTypeSystems oneTypeSystems = this.m_TypeSystems.GetOrCreateOneTypeSystems(iSystemType.Type());
                oneTypeSystems.Map.Add(iSystemType.SystemType(), obj);
                int index = iSystemType.GetInstanceQueueIndex();
                if (index > InstanceQueueIndex.None && index < InstanceQueueIndex.Max)
                {
                    oneTypeSystems.QueueFlag[index] = true;
                }
            }

            foreach (var kv in CodeTypes.Instance.GetTypes())
            {
                Type type = kv.Value;
                if (typeof(UGFEntity).IsAssignableFrom(type))
                {
                    long hash = type.FullName.GetLongHashCode();
                    try
                    {
                        this.m_UGFTypeLongHashCode.Add(type, type.FullName.GetLongHashCode());
                    }
                    catch (Exception e)
                    {
                        Type sameHashType = this.m_UGFTypeLongHashCode.GetKeyByValue(hash);
                        throw new Exception($"long hash add to ugfTypeLongHashCode fail: {type.FullName} {sameHashType.FullName}", e);
                    }
                    
                }
            }
        }
        
        public long GetLongHashCode(Type type)
        {
            return this.m_UGFTypeLongHashCode.GetValueByKey(type);
        }

        public Type GetWidgetType(Type monoType)
        {
            return this.m_MonoTypeWidgetType.GetValueByKey(monoType);
        }

        public TypeSystems.OneTypeSystems GetOneTypeSystems(Type type)
        {
            return this.m_TypeSystems.GetOneTypeSystems(type);
        }

        public void UGFEntityOnShow(UGFEntity ugfEntity)
        {
            if (ugfEntity is not IUGFEntityOnShow)
            {
                return;
            }

            List<SystemObject> systems = this.m_TypeSystems.GetSystems(ugfEntity.GetType(), typeof(IUGFEntityOnShowSystem));
            if (systems == null)
            {
                return;
            }

            foreach (IUGFEntityOnShowSystem system in systems)
            {
                if (system == null)
                {
                    continue;
                }

                try
                {
                    system.Run(ugfEntity);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void UGFEntityOnHide(UGFEntity ugfEntity, bool isShutdown)
        {
            if (ugfEntity is not IUGFEntityOnHide)
            {
                return;
            }

            List<SystemObject> systems = this.m_TypeSystems.GetSystems(ugfEntity.GetType(), typeof(IUGFEntityOnHideSystem));
            if (systems == null)
            {
                return;
            }

            foreach (IUGFEntityOnHideSystem system in systems)
            {
                if (system == null)
                {
                    continue;
                }

                try
                {
                    system.Run(ugfEntity, isShutdown);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void UGFEntityOnRecycle(UGFEntity ugfEntity)
        {
            if (ugfEntity is not IUGFEntityOnRecycle)
            {
                return;
            }

            List<SystemObject> systems = this.m_TypeSystems.GetSystems(ugfEntity.GetType(), typeof(IUGFEntityOnRecycleSystem));
            if (systems == null)
            {
                return;
            }

            foreach (IUGFEntityOnRecycleSystem system in systems)
            {
                if (system == null)
                {
                    continue;
                }

                try
                {
                    system.Run(ugfEntity);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void UGFEntityOnAttached(UGFEntity ugfEntity, UGFEntity childEntity, Transform parentTransform)
        {
            if (ugfEntity is not IUGFEntityOnAttached)
            {
                return;
            }

            List<SystemObject> systems = this.m_TypeSystems.GetSystems(ugfEntity.GetType(), typeof(IUGFEntityOnAttachedSystem));
            if (systems == null)
            {
                return;
            }

            foreach (IUGFEntityOnAttachedSystem system in systems)
            {
                if (system == null)
                {
                    continue;
                }

                try
                {
                    system.Run(ugfEntity, childEntity, parentTransform);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void UGFEntityOnDetached(UGFEntity ugfEntity, UGFEntity childEntity)
        {
            if (ugfEntity is not IUGFEntityOnDetached)
            {
                return;
            }

            List<SystemObject> systems = this.m_TypeSystems.GetSystems(ugfEntity.GetType(), typeof(IUGFEntityOnDetachedSystem));
            if (systems == null)
            {
                return;
            }

            foreach (IUGFEntityOnDetachedSystem system in systems)
            {
                if (system == null)
                {
                    continue;
                }

                try
                {
                    system.Run(ugfEntity, childEntity);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void UGFEntityOnAttachTo(UGFEntity ugfEntity, UGFEntity parentEntity, Transform parentTransform)
        {
            if (ugfEntity is not IUGFEntityOnAttachTo)
            {
                return;
            }

            List<SystemObject> systems = this.m_TypeSystems.GetSystems(ugfEntity.GetType(), typeof(IUGFEntityOnAttachToSystem));
            if (systems == null)
            {
                return;
            }

            foreach (IUGFEntityOnAttachToSystem system in systems)
            {
                if (system == null)
                {
                    continue;
                }

                try
                {
                    system.Run(ugfEntity, parentEntity, parentTransform);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void UGFEntityOnDetachFrom(UGFEntity ugfEntity, UGFEntity parentEntity)
        {
            if (ugfEntity is not IUGFEntityOnDetachFrom)
            {
                return;
            }

            List<SystemObject> systems = this.m_TypeSystems.GetSystems(ugfEntity.GetType(), typeof(IUGFEntityOnDetachFromSystem));
            if (systems == null)
            {
                return;
            }

            foreach (IUGFEntityOnDetachFromSystem system in systems)
            {
                if (system == null)
                {
                    continue;
                }

                try
                {
                    system.Run(ugfEntity, parentEntity);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void UGFEntityOnUpdate(UGFEntity ugfEntity, float elapseSeconds, float realElapseSeconds)
        {
            if (ugfEntity is not IUGFEntityOnUpdate)
            {
                return;
            }

            List<SystemObject> systems = this.m_TypeSystems.GetSystems(ugfEntity.GetType(), typeof(IUGFEntityOnUpdateSystem));
            if (systems == null)
            {
                return;
            }

            foreach (IUGFEntityOnUpdateSystem system in systems)
            {
                if (system == null)
                {
                    continue;
                }

                try
                {
                    system.Run(ugfEntity, elapseSeconds, realElapseSeconds);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }
    }
}
