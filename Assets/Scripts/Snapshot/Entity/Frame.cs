using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ibc.Survival
{
    /// <summary>
    /// World state information that captures all entities at any given time.
    /// </summary>
    [Serializable]
    public class Frame
    {
        /// <summary>
        /// Tick at which frame was taken.
        /// </summary>
        public int Tick;
        
        /// <summary>
        /// Maximum number of entities that can be present in the frame.
        /// </summary>
        public int MaxEntities;
        
        /// <summary>
        /// List of entity states.
        /// </summary>
        [SerializeReference]
        public List<EntityStateBase> States;
        
        /// <summary>
        /// Map between entity identifier and IndexMapToStateIndex.
        /// </summary>
        public Dictionary<EntityId, EntityStateBase> EntityMap;
        
        /// <summary>
        /// Number of entities in the frame.
        /// </summary>
        public int Count => States.Count;

        /// <summary>
        /// Map between prefab id and state id.
        /// </summary>
        private readonly Dictionary<PrefabId, StateId> _prefabIdToStateIdMap;

        /// <summary>
        /// Create a new frame.
        /// </summary>
        public Frame(int tick, int maxEntities, Dictionary<PrefabId, StateId> prefabIdToStateIdMap)
        {
            Tick = tick;
            States = new List<EntityStateBase>(maxEntities);
            EntityMap = new Dictionary<EntityId, EntityStateBase>();
            MaxEntities = maxEntities;
            _prefabIdToStateIdMap = prefabIdToStateIdMap;
        }

        /// <summary>
        /// Returns whether the frame contains entity with specific entity identifier.
        /// </summary>
        public bool TryGetEntity(EntityId id, out EntityStateBase stateBase)
        {
            return EntityMap.TryGetValue(id, out stateBase);
        }

        /// <summary>
        /// Returns whether the frame contains entity with specific entity identifier.
        /// </summary>
        public bool ContainsEntity(EntityId id)
        {
            return EntityMap.ContainsKey(id);
        }

        
        /// <summary>
        /// Add new entity, with given state to the frame.
        /// </summary>
        /// <returns>Copy of the entity state that is stored in the frame</returns>
        public EntityStateBase AddEntity(EntityStateBase state)
        {
            Debug.Assert(state.Id.Value != 0);
            var stateCopy = EntityStatePool.Get(state.InstanceStateId);
            stateCopy.CopyFromInternal(state);
            
            EntityMap.Add(stateCopy.Entity.Id, stateCopy);
            States.Add(stateCopy);
            return stateCopy;
        }

        /// <summary>
        /// Add entity with default state to the frame.
        /// </summary>
        public EntityStateBase AddEntity(Entity entity)
        {
            Debug.Assert(entity.Id.Value != 0);
            var state = EntityStatePool.Get(_prefabIdToStateIdMap[entity.PrefabId]);
            state.Entity = entity; 
            EntityMap.Add(state.Entity.Id, state);
            States.Add(state);
            return state;
        }

        /// <summary>
        /// Remove entity. Decrement counter.
        /// </summary>
        public bool RemoveEntity(EntityId id)
        {
            if (EntityMap.Remove(id, out var state))
            {
                States.Remove(state);
                EntityStatePool.Return(state);
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Empty the frame.
        /// </summary>
        public void Clear()
        {
            foreach (var state in States)
                EntityStatePool.Return(state);
            
            EntityMap.Clear();
            States.Clear();
        }


        public void AddCreatedEntities(DeltaFrame deltaFrame)
        {
            for (int i = 0; i < deltaFrame.HeaderData.CreatedEntitiesCount; i++)
            {
                AddEntity(deltaFrame.CreatedEntities[i]);
            }
        }

        public void AddDestroyedEntities(DeltaFrame deltaFrame)
        {
            for (int i = 0; i < deltaFrame.HeaderData.DestroyedEntitiesCount; i++)
            {
                RemoveEntity(deltaFrame.DestroyedEntities[i].Id);
            }
        }

        public void AddChangedEntities(DeltaFrame deltaFrame)
        {
            for (int i = 0; i < deltaFrame.HeaderData.ChangedEntitiesCount; i++)
            {
                var changedDelta = deltaFrame.ChangedEntities[i];
                var changedEntity = changedDelta.DestState.Entity;
                var changedState = changedDelta.DestState;
                
                //entity must be present(see AddCreatedEntities)
                TryGetEntity(changedEntity.Id, out var srcFrameState);
                srcFrameState.CopyFromInternal(changedState);
            }
        }
        

        public void CopyFrom(Frame frame)
        {
            Copy(this, frame);
        }
        
        public void CopyTo(Frame frame)
        {
            Copy(frame, this);
        }
        
        private static void Copy(Frame dest, Frame src)
        {
            dest.Clear();
            dest.Tick = src.Tick;
            for (int i = 0; i < src.Count; i++)
                dest.AddEntity(src.States[i]);
        }

        public StateId GetStateId(PrefabId prefabId)
        {
            return _prefabIdToStateIdMap[prefabId];
        }

        public Entity this[int i] => States[i].Entity;
    }
}