using System;
using System.Linq;
using Ibc.Game;
using UnityEngine;

namespace Ibc.Survival
{

    /// <summary>
    /// Entity synchronization type. Indicates how entity should update its state.
    /// </summary>
    public enum EntitySyncType
    {
        /// <summary>
        /// Predicted entity has its state set to last state received as soon as it arrives.
        /// </summary>
        Predicted,
        
        /// <summary>
        /// Interpolated entity has its state interpolated between two states that were previously received. 
        /// </summary>
        Interpolated,
    }


    /// <summary>
    /// Base class for the network object.
    /// Provides methods to get and set associated entity state <see cref="EntityStateBase"/>
    /// Provides methods to change visibility of the entity for connection/s
    /// </summary>
    public abstract class EntityBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Identifies this entity with the unique name. This value is not sync.
        /// </summary>
        public string EntityName;

        /// <summary>
        /// Synchronization type for this entity, valid only on client. Indicates how entity should update its state.
        /// </summary>
        public EntitySyncType SyncType;

        /// <summary>
        /// Entity associated with this entity behaviour and is unique across network. Valid on both server and client.
        /// </summary>
        public abstract Entity Entity { get; internal set; }

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
        public ConnectionManager ConnectionManager;

        /// <summary>
        /// Indicates whether the entity is initialized. Initialized entity has its values set and is ready to be used.
        /// </summary>
        public bool Initialized;

        /// <summary>
        /// Entity manager reference cached value.
        /// </summary>
        public EntityManager EntityManager;

        public bool IsServer => ConnectionManager.IsServer;

        public bool IsClient => !ConnectionManager.IsServer;

        /// <summary>
        /// Associated controller with this entity if any. Valid on both server and client.
        /// </summary>
        public Controller AssociatedController;

        /// <summary>
        /// Whether the entity has associated controller set.
        /// </summary>
        public bool HasController => AssociatedController != null;


        private EntityModule[] _modules;

        protected virtual void Start()
        {
            ForceInitialize();
        }


        internal void ForceInitialize()
        {
            if (!Initialized)
            {
                ConnectionManager = FindObjectsOfType<ConnectionManager>().FirstOrDefault(t => t.IsServer);
                if (ConnectionManager != null)
                {
                    EntityManager = ConnectionManager.GetComponent<EntityManager>();
                    if (EntityManager != null)
                    {
                        AutoRegisterEntity();
                    }
                    else
                    {
                        Destroy(gameObject);
                        Debug.LogWarning("Destroying entity: Entity manager not found");
                    }
                }
                else
                {
                    //client
                    Destroy(gameObject);
                    Debug.LogWarning("Destroying entity: Client prespawn not supported");
                }
            }
        }

        private void AutoRegisterEntity()
        {
            var id = EntityManager.GetNewEntityIdentifier();
            var prefabId = EntityManager.GetPrefabId(this);
            var entity = new Entity() {Id = id, PrefabId = prefabId};
            InitializeInternal(entity, null, EntityManager);
            EntityManager.RegisterEntity(this);
        }

        internal virtual void InitializeInternal(Entity entity, EntityStateBase state,
            EntityManager entityManager)
        {
            if (Initialized)
                throw new Exception("Entity already initialized");
            if (ConnectionManager == null)
                ConnectionManager = entityManager.GetComponent<ConnectionManager>();

            Entity = entity;
            EntityManager = entityManager;
            Initialized = true;
            _modules = GetComponents<EntityModule>();

            GetState().Entity = entity;
            if (state != null)
                GetState().CopyFromInternal(state);
            GetState().InitializeInternal(this);

            OnInitialized();

            foreach (var entityModule in _modules)
                entityModule.OnInitialized();

            Debug.Log("Initialized");
            gameObject.name = $"[{ConnectionManager.HostName}] Entity {Id}";
        }

        /// <summary>
        /// Invoked when entity behaviour is setup and ready to be used.
        /// </summary>
        protected virtual void OnInitialized()
        {

        }

        protected internal void DeInitialize()
        {
            EntityManager = null;
            Initialized = false;
        }


        /// <summary>
        /// Associate entity with the controller.
        /// </summary>
        /// <param name="controller">Controller, can be null in which case previous association(if any) is removed
        /// </param>
        internal void AssociateController(Controller controller)
        {
            if (controller == AssociatedController)
                return;
            if (AssociatedController != null && controller != null) OnControllerLost();
            AssociatedController = controller;
            if (controller != null)
            {
                OnControllerGained(controller);

                foreach (var entityModule in _modules)
                    entityModule.OnControllerGained(controller);

            }
            else
            {
                foreach (var entityModule in _modules)
                    entityModule.OnControllerLost();

                OnControllerLost();
            }
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

        protected virtual void OnDestroy()
        {
            if (gameObject != null && EntityManager != null && Initialized)
            {
                if (EntityManager)
                    EntityManager.UnregisterEntity(this);
                else Debug.LogWarning("Could not find entity manager");
            }
        }

        internal void SetState(EntityStateBase state)
        {
            GetState().CopyFromInternal(state);
            OnSetState();

            foreach (var entityModule in _modules)
                entityModule.OnSetState();
        }

        protected virtual void OnSetState()
        {

        }

        internal virtual void Sv_NetworkTick(int tick)
        {
            GetState().Sv_NetworkTick(tick);

            foreach (var entityModule in _modules)
                entityModule.Sv_NetworkTick(tick);

        }

        internal virtual void Cl_NetworkTick(int tick)
        {
            GetState().Cl_NetworkTick(tick);

            foreach (var entityModule in _modules)
                entityModule.Cl_NetworkTick(tick);

        }

        internal void Cl_CommandReceivedInternal(EntityCommandType cmdType, CommandId id, StreamReader streamReader,
            int remoteTick)
        {
            Cl_CommandReceived(cmdType, id, streamReader, remoteTick);
            foreach (var entityModule in _modules)
                entityModule.Cl_CommandReceived(cmdType, id, streamReader, remoteTick);
            Debug.Log(cmdType + " " + id);

        }

        internal void Cl_CommandDroppedInternal(EntityCommandType cmdType, CommandId id, StreamReader streamReader,
            int remoteTick)
        {
            Cl_CommandDropped(cmdType, id, streamReader, remoteTick);
            foreach (var entityModule in _modules)
                entityModule.Cl_CommandDropped(cmdType, id, streamReader, remoteTick);

        }

        internal void Cl_CommandAckedInternal(EntityCommandType cmdType, CommandId id, StreamReader streamReader,
            int remoteTick)
        {
            Cl_CommandAcked(cmdType, id, streamReader, remoteTick);
            foreach (var entityModule in _modules)
                entityModule.Cl_CommandAcked(cmdType, id, streamReader, remoteTick);

        }

        internal void Sv_CommandReceivedInternal(Connection connection, EntityCommandType cmdType, CommandId id,
            StreamReader streamReader, int remoteTick)
        {
            Sv_CommandReceived(connection, cmdType, id, streamReader, remoteTick);
            foreach (var entityModule in _modules)
                entityModule.Sv_CommandReceived(connection, cmdType, id, streamReader, remoteTick);

        }


        internal void Sv_CommandDroppedInternal(Connection connection, EntityCommandType cmdType, CommandId id,
            StreamReader streamReader, int remoteTick)
        {
            Sv_CommandDropped(connection, cmdType, id, streamReader, remoteTick);
            foreach (var entityModule in _modules)
                entityModule.Sv_CommandDropped(connection, cmdType, id, streamReader, remoteTick);
        }

        internal void Sv_CommandAckedInternal(Connection connection, EntityCommandType cmdType, CommandId id,
            StreamReader streamReader, int remoteTick)
        {
            Sv_CommandAcked(connection, cmdType, id, streamReader, remoteTick);
            foreach (var entityModule in _modules)
                entityModule.Sv_CommandAcked(connection, cmdType, id, streamReader, remoteTick);

        }

        public virtual void Cl_CommandReceived(EntityCommandType cmdType, CommandId id, StreamReader streamReader,
            int remoteTick)
        {

        }

        public virtual void Cl_CommandDropped(EntityCommandType cmdType, CommandId id, StreamReader streamReader,
            int remoteTick)
        {

        }

        public virtual void Cl_CommandAcked(EntityCommandType cmdType, CommandId id, StreamReader streamReader,
            int remoteTick)
        {

        }

        public virtual void Sv_CommandReceived(Connection connection, EntityCommandType cmdType, CommandId id,
            StreamReader streamReader, int remoteTick)
        {

        }

        public virtual void Sv_CommandAcked(Connection connection, EntityCommandType cmdType, CommandId id,
            StreamReader streamReader, int remoteTick)
        {

        }

        public virtual void Sv_CommandDropped(Connection connection, EntityCommandType cmdType, CommandId id,
            StreamReader streamReader, int remoteTick)
        {

        }

        public bool Cl_SendEntityCommand<T>(EntityCommandType cmdType, T cmd, out CommandId cmdId)
            where T : struct, IMessage
        {
            return Sv_SendEntityCommand(ConnectionManager.GetConnection(0), cmdType, cmd, out cmdId);
        }

        public void Sv_SendEntityCommandToAll<T>(EntityCommandType cmdType, T cmd) where T : struct, IMessage
        {
            for (int i = 0; i < ConnectionManager.GetConnectionCount(); ++i)
            {
                Sv_SendEntityCommand(ConnectionManager.GetConnection(i), cmdType, cmd, out _);
            }
        }

        public void Sv_SendEntityCommandToAllExceptController<T>(EntityCommandType cmdType, T cmd)
            where T : struct, IMessage
        {
            for (int i = 0; i < ConnectionManager.GetConnectionCount(); ++i)
            {
                var gameConnectionState = (GameConnectionState) ConnectionManager.GetConnection(i).GetState();
                if (!gameConnectionState.Controller.IsEntityAssociated(Entity.Id))
                {
                    Sv_SendEntityCommand(ConnectionManager.GetConnection(i), cmdType, cmd, out _);
                }
            }
        }

        public bool Sv_SendEntityCommand<T>(Connection connection, EntityCommandType cmdType, T cmd,
            out CommandId cmdId) where T : struct, IMessage
        {
            if (connection.GetState() is GameConnectionState gameConnectionState)
            {
                var controllerCmd = new EntityCommand<T>();
                controllerCmd.Header = new EntityCommandHeader()
                {
                    CommandType = cmdType,
                    EntityId = Entity.Id,
                    LocalTick = gameConnectionState.LocalTick
                };
                controllerCmd.Cmd = cmd;
                return gameConnectionState.SendCommand(CommandType.EntityCommand, controllerCmd, out cmdId);
            }

            cmdId = default;
            return false;
        }

        public abstract StateId GetStateId();
        public abstract Type GetStateType();
        public abstract EntityStateBase GetState();
       
    }

    /// <summary>
    /// Marks network object in the unity scene.
    /// Network object keeps track of entity that it represents and state that it holds.
    /// </summary>
    public abstract class EntityBehaviour<T> : EntityBehaviour, IComparable<EntityBehaviour<T>> where T : EntityStateBase<T>, new()
    {

        /// <inheritdoc cref="EntityBehaviour.Entity"/>
        public override Entity Entity
        {
            get
            {
                if (!Initialized)
                    throw new Exception($"Entity is not initialized");
                return State.Entity;
            }
            internal set => State.Entity = value;
        }
        
        /// <summary>
        /// Current entity state that is synchronized across network to all connections that see this entity.
        /// </summary>
        [SerializeField]
        public T State;

        protected virtual void Awake()
        {
            //since class may not be marked with serializable attribute
            if (State == null)
            {
                State = new T();
            }
        }

        /// <summary>
        /// Returns current entity state.
        /// </summary>
        public sealed override EntityStateBase GetState()
        {
            return State;
        }

        /// <summary>
        /// Returns state identifier associated with <see cref="T"/> type.
        /// </summary>
        public sealed override StateId GetStateId()
        {
            return EntityStateBase<T>.StaticStateId;
        }
        
        /// <summary>
        /// Returns state type.
        /// </summary>
        public sealed override Type GetStateType()
        {
            return typeof(T);
        }

        /// <summary>
        /// Compares entity identifiers for equality.
        /// </summary>
        public int CompareTo(EntityBehaviour<T> other)
        {
            return Id.CompareTo(other.Id);
        }

    }
}
