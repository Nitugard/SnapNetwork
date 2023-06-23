using System;
using UnityEngine;

namespace Ibc.Survival
{
    /// <summary>
    /// Entity state that is sync across the network.
    /// </summary>
    [Serializable]
    public abstract class EntityStateBase : IComparable<EntityStateBase>, ISortable
    {
        /// <summary>
        /// Entity priority when sending data across network. Higher priority takes precedence over other entities.
        /// </summary>
        public abstract int Priority { get; }

        /// <summary>
        /// Server (connection) tick when this state was captured.
        /// </summary>
        protected internal int Tick { get; internal set; }

        /// <summary>
        /// Internal entity identifier sync across network.
        /// </summary>
        [SerializeField]
        internal Entity Entity;
        
        /// <summary>
        /// Entity identifier sync across network.
        /// </summary>
        protected internal EntityId Id => Entity.Id;
        
        /// <summary>
        /// Prefab identifier sync across network.
        /// </summary>
        protected internal PrefabId PrefabId => Entity.PrefabId;
        
        /// <summary>
        /// Entity behaviour cached.
        /// </summary>
        protected EntityBehaviour EntityBehaviour { get; private set; }
        
        /// <summary>
        /// Entity transform cached.
        /// </summary>
        protected Transform Transform { get; private set; }
        
        /// <summary>
        /// Instance state identifier that is wrapper around <see cref="EntityStateBase{T}.StaticStateId"/>
        /// </summary>
        internal abstract StateId InstanceStateId { get; }

        public bool IsServer => EntityBehaviour.ConnectionManager.IsServer;

        public bool IsClient => !EntityBehaviour.ConnectionManager.IsServer;
        
        /// <summary>
        /// Creates association between state and entity behaviour.
        /// </summary>
        /// <param name="entityBehaviour">Target entity</param>
        protected abstract void Initialize(EntityBehaviour entityBehaviour);

        
        public virtual void Sv_NetworkTick(int tick)
        {
            
        }

        public virtual void Cl_NetworkTick(int tick)
        {
            
        }

        internal void InitializeInternal(EntityBehaviour entityBehaviour)
        {
            Transform = entityBehaviour.transform;
            EntityBehaviour = entityBehaviour;
            Initialize(EntityBehaviour);
        }
        
        internal abstract void ExchangeDeltaInternal<TStream>(ref TStream stream, EntityStateBase baseLine, Connection connection) where TStream : struct, IStream;
        internal abstract void InterpolateInternal(EntityStateBase src, EntityStateBase dest, int tick, float t);
        internal abstract bool HasChangedInternal(EntityStateBase src, Connection connectionHandle);

        internal virtual void ClearInternal()
        {
            Entity = default;
            Tick = 0;
        }

        internal virtual void CopyFromInternal(EntityStateBase src)
        {
            Entity = src.Entity;
            Tick = src.Tick;
            
            Debug.Assert(src.GetType() == GetType());
            Debug.Assert(src.InstanceStateId.Value == InstanceStateId.Value);
        }

        public void CopyTo(EntityStateBase dest)
        {
            dest.CopyFromInternal(this);
        }
        

        public int CompareTo(EntityStateBase other)
        {
            return Id.CompareTo(other.Id);
        }
    }



    /// <summary>
    /// Wrapper around <see cref="EntityStateBase"/> for specific state type. This class abstracts methods from
    /// <see cref="EntityStateBase"/> to be better suited for specific type. 
    /// </summary>
    /// <typeparam name="T">EntityStateBase type</typeparam>
    [Serializable]
    public abstract class EntityStateBase<T> : EntityStateBase, IComparable<EntityStateBase<T>>
        where T : EntityStateBase<T>, new()
    {
        /// <summary>
        /// Unique identifier for <see cref="EntityStatePool"/>
        /// </summary>
        public static readonly StateId StaticStateId;

        /// <summary>
        /// Wrapper around <see cref="StaticStateId"/> for state instances.
        /// </summary>
        internal override StateId InstanceStateId => StaticStateId;

        static EntityStateBase()
        {
            StaticStateId = EntityStatePool.RegisterState<T>();
        }

        internal sealed override void ExchangeDeltaInternal<TStream>(ref TStream stream, EntityStateBase baseLine, Connection connection)
        {
            ExchangeDelta(ref stream, (T) baseLine, connection);
        }

        internal sealed override void InterpolateInternal(EntityStateBase src, EntityStateBase dest, int tick, float t)
        {
            Interpolate((T) src, (T) dest, tick, t);
        }

        internal sealed override bool HasChangedInternal(EntityStateBase src, Connection connectionHandle)
        {
            return HasChanged((T) src, connectionHandle);
        }


        internal sealed override void CopyFromInternal(EntityStateBase src)
        {
            CopyFrom((T) src);
            base.CopyFromInternal(src);
        }

        internal sealed override void ClearInternal()
        {
            Clear();
            base.ClearInternal();
        }

        /// <summary>
        /// Returns true if connection controller is associated with this entity.
        /// </summary>
        public bool IsAssociatedWithConnection(Connection connection)
        {
            var state = connection.GetState();
            if (state is GameConnectionState gameState)
            {
                var controller = gameState.Controller;
                return controller.IsEntityAssociated(Entity.Id);
            }

            return false;
        }

        /// <summary>
        /// Exchange state information with the network.
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        /// <param name="baseLine">Previous state</param>
        /// <param name="connection">State is sent over this connection</param>
        /// <typeparam name="TStream">Type of the stream</typeparam>
        protected abstract void ExchangeDelta<TStream>(ref TStream stream, T baseLine, Connection connection)
            where TStream : struct, IStream;

        /// <summary>
        /// Interpolates this entity state between src and dest state based on normalized time.
        /// </summary>
        /// <param name="src">Source entity state</param>
        /// <param name="dest">Destination entity state</param>
        /// <param name="t">Normalized time</param>
        protected abstract void Interpolate(T src, T dest, int tick, float t);

        /// <summary>
        /// Indicates whether this state has changed.
        /// </summary>
        /// <param name="src">Source entity state to compare with</param>
        /// <param name="connectionHandle">Target connection</param>
        /// <returns>True if changed</returns>
        protected abstract bool HasChanged(T src, Connection connectionHandle);

        /// <summary>
        /// Copy data from source state.
        /// </summary>
        /// <param name="src">Source state</param>
        protected abstract void CopyFrom(T src);

        /// <summary>
        /// Clear data to default values.
        /// </summary>
        protected abstract void Clear();


        public int CompareTo(EntityStateBase<T> other)
        {
            return Id.CompareTo(other.Id);
        }
    }
}