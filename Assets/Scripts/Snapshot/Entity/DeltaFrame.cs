using System;
using UnityEngine;

namespace Ibc.Survival
{
    
    /// <summary>
    /// Represents delta between two frames.
    /// </summary>
    public class DeltaFrame
    {

        public struct TickHeader
        {
            public int SourceTick;

            public TickHeader(int srcFrameTick)
            {
                SourceTick = srcFrameTick;
            }

            public void Exchange<TStream>(ref TStream stream, int targetTick)
                where TStream : struct, IStream
            {
                stream.ExchangeDelta(ref SourceTick, targetTick);
            }
        }
        
        /// <summary>
        /// Delta frame information.
        /// </summary>
        public struct Header
        {
            public byte CreatedEntitiesCount;
            public byte DestroyedEntitiesCount;
            public byte ChangedEntitiesCount;


            public void Exchange<TStream>(ref TStream stream)
                where TStream : struct, IStream
            {
                bool created = CreatedEntitiesCount != 0;
                bool destroyed = DestroyedEntitiesCount != 0;
                bool changed = ChangedEntitiesCount != 0;
                bool empty = !created && !destroyed && !changed;

                stream.Exchange(ref empty);

                if (!empty)
                {
                    stream.Exchange(ref created);
                    stream.Exchange(ref destroyed);
                    stream.Exchange(ref changed);

                    if (created)
                        stream.Exchange(ref CreatedEntitiesCount);
                    if (destroyed)
                        stream.Exchange(ref DestroyedEntitiesCount);
                    if (changed)
                        stream.Exchange(ref ChangedEntitiesCount);
                }
            }
        }
        
        /// <summary>
        /// Caches src and destination states for the same entity together with precalculated priority value.
        /// Implements comparison between entity-state tuple based on priority.
        /// </summary>
        public struct EntityStateDelta : ISortable
        {
            public EntityStateBase DestState;
            public EntityStateBase SrcState;
            public int Priority { get; set; }
        }

        public TickHeader TickHeaderData;
        public Header HeaderData;
        public readonly EntityStateBase[] CreatedEntities;
        public readonly EntityStateBase[] DestroyedEntities;
        public readonly EntityStateDelta[] ChangedEntities;
        
        private readonly EntityStateBase[] _entitiesTempArray;
        private readonly EntityStateDelta[] _changedEntitiesTempArray;
        private readonly int[] _radixCount;
        
        public DeltaFrame(int maxEntitiesInTheWorld)
        {
            CreatedEntities = new EntityStateBase[maxEntitiesInTheWorld];
            DestroyedEntities = new EntityStateBase[maxEntitiesInTheWorld];
            ChangedEntities = new EntityStateDelta[maxEntitiesInTheWorld];

            _entitiesTempArray = new EntityStateBase[maxEntitiesInTheWorld];
            _changedEntitiesTempArray = new EntityStateDelta[maxEntitiesInTheWorld];
            _radixCount = new int[10];
        }

        /// <summary>
        /// Clear header information.
        /// </summary>
        public void Clear()
        {
            HeaderData = default;
            TickHeaderData = default;
        }
        
        /// <summary>
        /// Calculates the delta between two frames and store result.
        /// </summary>
        public void CalculateDelta(Frame src, Frame dest, Connection connection)
        {
            TickHeaderData.SourceTick = src.Tick;
            
            StoreCreatedEntities(dest, src);
            StoreDestroyedAndChangedEntities(src, dest, connection);
            
            ListExtensions.RadixSort(CreatedEntities, _entitiesTempArray, _radixCount, HeaderData.CreatedEntitiesCount);
            ListExtensions.RadixSort(DestroyedEntities, _entitiesTempArray, _radixCount, HeaderData.DestroyedEntitiesCount);
            ListExtensions.RadixSort(ChangedEntities, _changedEntitiesTempArray, _radixCount, HeaderData.ChangedEntitiesCount);
            
            Array.Reverse(CreatedEntities, 0, HeaderData.CreatedEntitiesCount);
            Array.Reverse(DestroyedEntities, 0, HeaderData.DestroyedEntitiesCount);
            Array.Reverse(ChangedEntities, 0, HeaderData.ChangedEntitiesCount);
        }

        private void StoreCreatedEntities(Frame dest, Frame src)
        {
            for (int i = 0; i < dest.Count; i++)
            {
                if (!src.ContainsEntity(dest.States[i].Id))
                {
                    CreatedEntities[HeaderData.CreatedEntitiesCount++] = dest.States[i];
                }
            }
        }

        private void StoreDestroyedAndChangedEntities(Frame src, Frame dest, Connection connection)
        {
            for (int i = 0; i < src.Count; i++)
            {
                if (dest.TryGetEntity(src.States[i].Id, out var destState))
                {
                    if (destState.HasChangedInternal(src.States[i], connection))
                    {
                        int tickPriority = destState.Tick - src.States[i].Tick;
                        Debug.Assert(tickPriority >= 0);
                        destState.Tick = dest.Tick;
                        
                        var entityStateDelta = new EntityStateDelta()
                        {
                            DestState = destState,
                            SrcState = src.States[i],
                            Priority = src.States[i].Priority + tickPriority,
                        };

                        ChangedEntities[HeaderData.ChangedEntitiesCount++] = entityStateDelta;
                    }
                }
                else
                {
                    DestroyedEntities[HeaderData.DestroyedEntitiesCount++] = src.States[i];
                }
            }
        }
    }
}