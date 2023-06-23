using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;

namespace Ibc.Survival
{
    /// <summary>
    /// Internal <see cref="EntityStateBase"/> pool. Thread safe.
    /// </summary>
    public static class EntityStatePool
    {
        private class EntityStateCache
        {
            public Type Type;
            public Func<object> New;
            public StateId StateId;
        }

        
        private static readonly Dictionary<StateId, EntityStateCache> EntityStateCacheMap;
        private static readonly Queue<EntityStateBase>[] Dictionary;

        private static StateId _nextStateId;

#if DEBUG_ENTITY_STATE_POOL
        private static readonly List<EntityStateBase> AllocatedStates = new();
#endif

        static EntityStatePool()
        {
            EntityStateCacheMap = new();
            Dictionary = new Queue<EntityStateBase>[PrefabId.MaxValue];

            _nextStateId.Value = 0;
        }

        internal static StateId RegisterState<T>() where T : EntityStateBase<T>, new()
        {

            {
                _nextStateId.Value++;

                Func<object> newStateFunc = Expression.Lambda<Func<object>>(Expression.New(typeof(T))).Compile();
                EntityStateCache entityStateCache = new EntityStateCache()
                {
                    Type = typeof(T),
                    New = newStateFunc,
                    StateId = _nextStateId
                };

                Queue<EntityStateBase> concurrentQueue = new Queue<EntityStateBase>();
                Dictionary[_nextStateId.Value] = concurrentQueue;
                EntityStateCacheMap.Add(_nextStateId, entityStateCache);
                return _nextStateId;
            }
        }

        internal static EntityStateBase Get(StateId stateId)
        {
            EntityStateBase stateBase;
            bool found;
            {
                var queue = Dictionary[stateId.Value];
                found = queue.TryDequeue(out stateBase);
            }

            if (!found)
                stateBase = (EntityStateBase)EntityStateCacheMap[stateId].New();

            stateBase.ClearInternal();
#if DEBUG_ENTITY_STATE_POOL
                lock(Lock)
                    AllocatedStates.Add(stateBase);
#endif

            return stateBase;

        }

        internal static void Return(EntityStateBase stateBase)
        {
            stateBase.ClearInternal();

            {
                var queue = Dictionary[stateBase.InstanceStateId.Value];
#if DEBUG_ENTITY_STATE_POOL
                int index = AllocatedStates.IndexOf(stateBase);
                if (index != -1)
                    AllocatedStates.RemoveAt(index);
                else
                    throw new Exception("Return called, but state was not taken from state pool! Leak?");
#endif

                queue.Enqueue(stateBase);
            }
        }
    }
}