using System;
using UnityEngine;

namespace Ibc.Survival
{
    [RequireComponent(typeof(EntityBehaviour))]
    public abstract class EntityModule : MonoBehaviour
    {
        public EntityBehaviour EntityBehaviour { get; protected set; }

        public StateId StateId => EntityBehaviour.GetStateId();
        public Type StateType => EntityBehaviour.GetStateType();
        public EntityStateBase State => EntityBehaviour.GetState();

        /// <summary>
        /// Identifies this entity with the unique name. This value is not sync.
        /// </summary>
        public string EntityName => EntityBehaviour.EntityName;


        /// <summary>
        /// Entity associated with this entity behaviour and is unique across network. Valid on both server and client.
        /// </summary>
        public Entity Entity => EntityBehaviour.Entity;
        
        /// <summary>
        /// Entity identifier is unique across network. Valid on both server and client.
        /// </summary>
        public EntityId Id => Entity.Id;
        
        /// <summary>
        /// Prefab id.
        /// </summary>
        public PrefabId PrefabId => Entity.PrefabId;

        /// <summary>
        /// Connection manager reference, entity can not and should not be spawned without host.
        /// </summary>
        public ConnectionManager ConnectionManager => EntityBehaviour.ConnectionManager;

        /// <summary>
        /// Indicates whether the entity is initialized. Initialized entity has its values set and is ready to be used.
        /// </summary>
        protected bool Initialized => EntityBehaviour.Initialized;

        /// <summary>
        /// Entity manager reference cached value.
        /// </summary>
        private EntityManager EntityManager => EntityBehaviour.EntityManager;
        
        public bool IsServer => ConnectionManager.IsServer;

        public bool IsClient => !ConnectionManager.IsServer;

        /// <summary>
        /// Associated controller with this entity if any. Valid on both server and client.
        /// </summary>
        public Controller AssociatedController => EntityBehaviour.AssociatedController;

        /// <summary>
        /// Whether the entity has associated controller set.
        /// </summary>
        public bool HasController => AssociatedController != null;


        /// <summary>
        /// Invoked when entity behaviour is setup and ready to be used.
        /// </summary>
        public virtual void OnInitialized()
        {
            EntityBehaviour = GetComponent<EntityBehaviour>();
        }


        /// <summary>
        /// Called when controller gets associated with entity.
        /// </summary>
        /// <param name="controller">Target controller</param>
        public virtual void OnControllerGained(Controller controller)
        {
            
        }


        /// <summary>
        /// Called when controller loses specific entity association.
        /// </summary>
        public virtual void OnControllerLost()
        {
            
        }


        public virtual void OnSetState()
        {
            
        }

        public virtual void Sv_NetworkTick(int tick)
        {
            
        }

        public virtual void Cl_NetworkTick(int tick)
        {
            
        }


        public virtual void Cl_CommandReceived(EntityCommandType cmdType, CommandId id, StreamReader streamReader, int remoteTick)
        {
            
        }

        public virtual void Cl_CommandDropped(EntityCommandType cmdType, CommandId id, StreamReader streamReader, int remoteTick)
        {
            
        }

        public virtual void Cl_CommandAcked(EntityCommandType cmdType, CommandId id, StreamReader streamReader, int remoteTick)
        {
            
        }

        public virtual void Sv_CommandReceived(Connection connection, EntityCommandType cmdType, CommandId id, StreamReader streamReader, int remoteTick)
        {
            
        }

        public virtual void Sv_CommandAcked(Connection connection, EntityCommandType cmdType, CommandId id, StreamReader streamReader, int remoteTick)
        {
            
        }

        public virtual void Sv_CommandDropped(Connection connection, EntityCommandType cmdType, CommandId id, StreamReader streamReader, int remoteTick)
        {
            
        }
    }
}