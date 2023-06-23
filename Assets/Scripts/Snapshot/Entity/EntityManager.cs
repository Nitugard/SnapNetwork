using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ibc.Survival
{
    /// <summary>
    /// Class that keeps track of all entities in the unity world.
    /// Provides methods to capture them in a frame <see cref="Frame"/> or
    /// create empty frames for future storage.
    /// </summary>
    public sealed class EntityManager : MonoBehaviour
    {
        public Action<EntityBehaviour> OnEntityAdded;
        public Action<EntityBehaviour> OnEntityRemoved;
        
        [SerializeField, Tooltip("Maximum number of entities that can be present in the world at any given time")]
        private int _maxEntities = 1024;
        
        [SerializeField, Tooltip("Network prefabs")]
        public GameObject[] EntityPrefabs;
        
        /// <summary>
        /// Next entity identifier.
        /// </summary>
        [HideInInspector]
        [SerializeField]
        private EntityId _nextEntityId;
        
        /// <summary>
        /// Map between network prefab id and entity state identifier.
        /// </summary>
        private Dictionary<PrefabId, StateId> _prefabIdToStateIdMap;
        
        /// <summary>
        /// Entity Id to Registered entity instance map.
        /// </summary>
        private Dictionary<EntityId, EntityBehaviour> _entityMap;

        /// <summary>
        /// Contiguous list of entities.
        /// </summary>
        [SerializeField] private List<EntityBehaviour> _entityList;
        

        private void Awake()
        {
            _prefabIdToStateIdMap = new Dictionary<PrefabId, StateId>(EntityPrefabs.Length);
            _entityMap = new Dictionary<EntityId, EntityBehaviour>(_maxEntities);
            _entityList = new List<EntityBehaviour>(_maxEntities);
            
            if (EntityPrefabs.Length >= PrefabId.MaxValue)
                throw new Exception("Capacity reached");

            //check prefabs
            var entities = EntityPrefabs
                .Select((t, index) => new { entity = t.GetComponent<EntityBehaviour>(), index }).ToList();
            foreach (var tuple in entities)
            {
                if (tuple.entity == null)
                    throw new Exception("Invalid prefab");
            }

            //set up map between prefab id and state id
            var stateGroups = entities.GroupBy(t => t.entity.GetStateType());
            foreach (var stateGroup in stateGroups)
            {
                foreach (var tuple in stateGroup)
                    _prefabIdToStateIdMap.Add(new PrefabId(tuple.index), tuple.entity.GetStateId());
            }
            
        }
        
        public EntityBehaviour GetPrefab(PrefabId prefabId)
        {
            return EntityPrefabs[prefabId.Value].GetComponent<EntityBehaviour>();
        }

        
        public EntityBehaviour GetPrefab(string entityName)
        {
            return EntityPrefabs.FirstOrDefault(t => t.GetComponent<EntityBehaviour>().EntityName == entityName)?.GetComponent<EntityBehaviour>();
        }
        
        internal EntityId GetNewEntityIdentifier()
        {
            if (_nextEntityId.Value >= EntityId.MaxValue) _nextEntityId.Value = 1;
            else _nextEntityId.Value++;
            return _nextEntityId;
        }
        
        public void DestroyEntity(EntityBehaviour entityInstance)
        {
            DestroyEntityInternal(entityInstance.Id);
        }

        public void RegisterEntity(EntityBehaviour entityBehaviour)
        {
            if (_entityMap.TryAdd(entityBehaviour.Entity.Id, entityBehaviour))
            {
                _entityList.Add(entityBehaviour);
                OnEntityAdded?.Invoke(entityBehaviour);
                
                Debug.Log($"Entity registered: {entityBehaviour.name}");
            }
            else
            {
                Debug.LogWarning($"Entity already registered: {entityBehaviour.name}");
            }
        }

        public void UnregisterEntity(EntityBehaviour entityBehaviour)
        {
            if (_entityMap.Remove(entityBehaviour.Entity.Id))
            {
                _entityList.Remove(entityBehaviour);
                Debug.Log($"Entity unregistered: {entityBehaviour.name}");

                OnEntityRemoved?.Invoke(entityBehaviour);
            }
            else
            {
                Debug.LogWarning($"Entity not registered: {entityBehaviour.name}");
            }
        }

        internal EntityBehaviour SpawnEntityInternal(Entity entity, EntityStateBase entityStateBase, Vector3 pos,
            Quaternion rot)
        {
            var entityPrefab = GetPrefab(entity.PrefabId);
            var obj = Instantiate(entityPrefab.gameObject, pos, rot);
            var entityBehaviour = obj.GetComponent<EntityBehaviour>();
            entityBehaviour.InitializeInternal(entity, entityStateBase, this);
            RegisterEntity(entityBehaviour);
            return entityBehaviour;
        }


        internal void DestroyEntityInternal(EntityId entityId)
        {
            if (_entityMap.TryGetValue(entityId, out EntityBehaviour entityInstance))
            {
                UnregisterEntity(entityInstance);
                entityInstance.DeInitialize();
                Destroy(entityInstance.gameObject);
            }
        }

        
        public Frame NewFrame(int tick)
        {
            return new Frame(tick, _maxEntities, _prefabIdToStateIdMap);
        }
        
        public DeltaFrame NewDeltaFrame()
        {
            return new DeltaFrame(_maxEntities);
        }

        public PrefabId GetPrefabId(EntityBehaviour entityBehaviour)
        {
            var entities = EntityPrefabs.Select((obj, index) =>
                new { entity = obj.GetComponent<EntityBehaviour>(), index });
            var target = entities.SingleOrDefault((group) => group.entity.GetType() == entityBehaviour.GetType());
            if (target == null)
                throw new Exception($"Could not find entity prefab {entityBehaviour.name}");
            return new PrefabId(target.index);
        }
        
        public List<EntityBehaviour> GetAllEntities()
        {
            return _entityList;
        }
        
        public bool TryGetEntity(EntityId entityId, out EntityBehaviour entity)
        {
            return _entityMap.TryGetValue(entityId, out entity);
        }
    }
}