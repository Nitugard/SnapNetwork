using System;
using UnityEngine;

namespace Ibc.Survival
{
    
    /// <summary>
    /// Snapshot message provides a way to write/read delta frame information across network.
    /// </summary>
    public static class SnapshotMessage
    {
        public static byte WriteCreatedEntities(ref StreamWriter stream, Connection connectionHandle, EntityStateBase[] entities, byte count)
        {
            if (stream.Fail)
                return 0;

            byte sent = 0;
            Entity previousEntity = default, currentEntity = default;
            for (int i = 0; i < count; i++)
            {
                int currentBitPosition = stream.BitPosition;
                
                currentEntity = entities[i].Entity;
                stream.ExchangeDelta(ref currentEntity.Id.Value, previousEntity.Id.Value);
                stream.ExchangeDelta(ref currentEntity.PrefabId.Value, previousEntity.PrefabId.Value);
                Debug.Assert(currentEntity.Id.Value != 0);

                //exchange state delta
                var defaultEntityState = EntityStatePool.Get(entities[i].InstanceStateId);
                entities[i].ExchangeDeltaInternal(ref stream, defaultEntityState, connectionHandle);
                EntityStatePool.Return(defaultEntityState);

                //used for compressing ids and prefab ids
                //todo: sort ids in order!
                previousEntity = currentEntity;
                
                if (stream.Fail)
                {
                    //reset bit position and stop writing
                    stream.BitPosition = currentBitPosition;
                    stream.Fail = false;
                    break;
                }
                
                sent++;
            }

            return sent;
        }

        public static void ReadCreatedEntities(ref StreamReader stream, Connection connectionHandle, Frame destFrame, ushort count)
        {
            Entity previousEntity = default, currentEntity = default;
            for (int i = 0; i < count; i++)
            {
                stream.ExchangeDelta(ref currentEntity.Id.Value, previousEntity.Id.Value);
                stream.ExchangeDelta(ref currentEntity.PrefabId.Value, previousEntity.PrefabId.Value);
                
                //add entity and exchange state
                var stateBase = destFrame.AddEntity(currentEntity);
                var defaultEntityState = EntityStatePool.Get(stateBase.InstanceStateId);
                stateBase.ExchangeDeltaInternal(ref stream, defaultEntityState, connectionHandle);
                EntityStatePool.Return(defaultEntityState);

                Debug.Assert(currentEntity.Id.Value != 0);
                
                //used for compressing ids and prefab ids
                previousEntity = currentEntity;

                if (stream.Fail)
                    Debug.LogError("Stream serializer is corrupted!");
            }
        }
        
        public static byte WriteDestroyedEntities(ref StreamWriter stream, EntityStateBase[] entities, byte count)
        {
            //stream failed elsewhere
            if (stream.Fail)
                return 0;

            byte sent = 0;
            Entity previousEntity = default, currentEntity = default;
            for (int i = 0; i < count; i++)
            {
                int currentBitPosition = stream.BitPosition;
                currentEntity = entities[i].Entity;
                stream.ExchangeDelta(ref currentEntity.Id.Value, previousEntity.Id.Value);
                Debug.Assert(currentEntity.Id.Value != 0);
                previousEntity = currentEntity;

                if (stream.Fail)
                {
                    //reset bit position and stop writing
                    stream.BitPosition = currentBitPosition;
                    stream.Fail = false;
                    break;
                }

                sent++;
            }

            return sent;
        }
        
        public static void ReadDestroyedEntities(ref StreamReader stream, Frame destFrame, ushort count) 
        {
            Entity previousEntity = default, currentEntity = default;
            for (int i = 0; i < count; i++)
            {
                stream.ExchangeDelta(ref currentEntity.Id.Value, previousEntity.Id.Value);
                Debug.Assert(currentEntity.Id.Value != 0);

                bool removed = destFrame.RemoveEntity(currentEntity.Id);
                if(!removed)
                    throw new Exception($"Could not find entity {currentEntity.Id}");
                
                if (stream.Fail)
                    Debug.LogError("Stream serializer is corrupted!");
                previousEntity = currentEntity;
            }
        }

        public static byte WriteChangedEntities(ref StreamWriter stream, Connection connectionHandle, DeltaFrame.EntityStateDelta[] entities, byte count)
        {
            //stream failed elsewhere
            if (stream.Fail)
                return 0;

            byte sent = 0;
            Entity previousEntity = default, currentEntity = default;
            for (int i = 0; i < count; i++)
            {
                int currentBitPosition = stream.BitPosition;
                currentEntity = entities[i].DestState.Entity;
                
                stream.ExchangeDelta(ref currentEntity.Id.Value, previousEntity.Id.Value);

                var changedEntity = entities[i];
                var destEntityState = changedEntity.DestState;
                var srcEntityState = changedEntity.SrcState;
                destEntityState.ExchangeDeltaInternal(ref stream, srcEntityState, connectionHandle);
                previousEntity = currentEntity;
                
                if (stream.Fail)
                {
                    //reset bit position and stop writing
                    stream.BitPosition = currentBitPosition;
                    stream.Fail = false;
                    break;
                }
                
                sent++;
            }

            return sent;
        }
        
        public static void ReadChangedEntities(ref StreamReader stream, Connection connectionHandle, Frame srcFrame, Frame destFrame, ushort count) 
        {
            Entity previousEntity = default;
            for (int i = 0; i < count; i++)
            {
                //read entity id
                Entity destEntity = default;
                stream.ExchangeDelta(ref destEntity.Id.Value, previousEntity.Id.Value);
                
                //get destination state
                destFrame.TryGetEntity(destEntity.Id, out var destEntityState);
                
                //try and get source state
                bool isTakenFromPool = false;
                if (!srcFrame.TryGetEntity(destEntity.Id, out var srcEntityState))
                {
                    //src state not found so just take it from the pool
                    srcEntityState = EntityStatePool.Get(destFrame.GetStateId(destEntity.PrefabId));
                    isTakenFromPool = true;
                }
                    
                //exchange entity state(read)
                destEntityState.ExchangeDeltaInternal(ref stream, srcEntityState, connectionHandle);
                //destEntityState.TimeMs = values.TimeMs;

                //if the state was taken from pool, return it
                if (isTakenFromPool)
                    EntityStatePool.Return(srcEntityState);
                
                previousEntity = destEntityState.Entity;
                
                if (stream.Fail)
                    Debug.LogError("Stream serializer is corrupted!");
            }
        }

        public static void WriteDelta(ref StreamWriter stream, Connection connectionHandle, DeltaFrame deltaFrame)
        {
            var newHeader = deltaFrame.HeaderData;
            newHeader.CreatedEntitiesCount = WriteCreatedEntities(ref stream, connectionHandle, deltaFrame.CreatedEntities, deltaFrame.HeaderData.CreatedEntitiesCount);  
            newHeader.DestroyedEntitiesCount = WriteDestroyedEntities(ref stream, deltaFrame.DestroyedEntities, deltaFrame.HeaderData.DestroyedEntitiesCount);   
            newHeader.ChangedEntitiesCount = WriteChangedEntities(ref stream, connectionHandle, deltaFrame.ChangedEntities, deltaFrame.HeaderData.ChangedEntitiesCount);
            deltaFrame.HeaderData = newHeader;
        }

        public static void ReadSnapshot(ref StreamReader stream, Connection connectionHandle, DeltaFrame.Header header, Frame srcFrame,
            Frame destFrame)
        {
            ReadCreatedEntities(ref stream, connectionHandle, destFrame, header.CreatedEntitiesCount);
            ReadDestroyedEntities(ref stream, destFrame, header.DestroyedEntitiesCount);
            ReadChangedEntities(ref stream, connectionHandle, srcFrame, destFrame, header.ChangedEntitiesCount);
        }
    }
}