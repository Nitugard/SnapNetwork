#define DEBUG_ARRAY_POOL

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Ibc.Survival
{

    /// <summary>
    /// Generic array pool.
    /// </summary>
    /// <typeparam name="T">Unconstrained generic type</typeparam>
    public class ArrayPool<T> where T : unmanaged
    {
        public int BufferSize => _size;
        public bool IsEmpty => _buffer.Count != 0;
        public int Allocations => _allocCounter;

        private readonly Queue<T[]> _buffer;
        private readonly int _size;
        private int _allocCounter;
        
#if DEBUG_ARRAY_POOL
        private List<T[]> _trackedAllocations;
        private List<StackTrace> _trackedAllocationsStackTraces;
#endif
        
        public ArrayPool(int size, int capacity)
        {
            _buffer = new Queue<T[]>(capacity);
            for (int i = 0; i < capacity; ++i)
            {
                var data = new T[size];
                _buffer.Enqueue(data);
            }
#if DEBUG_ARRAY_POOL
            _trackedAllocations = new List<T[]>(capacity);
            _trackedAllocationsStackTraces = new List<StackTrace>();
#endif
            
            _size = size;
            _allocCounter = 0;
        }


        public T[] Alloc()
        {
            {
                if (_buffer.TryDequeue(out T[] data))
                {
                    ++_allocCounter;
                    Debug.Assert(_allocCounter < ushort.MaxValue);
#if DEBUG_ARRAY_POOL
                    _trackedAllocations.Add(data);
                    _trackedAllocationsStackTraces.Add(new StackTrace());
#endif

                    return data;
                }
            }

            {
                T[] data = new T[_size];
#if DEBUG_ARRAY_POOL
                _trackedAllocations.Add(data);
                _trackedAllocationsStackTraces.Add(new StackTrace());
#endif
                ++_allocCounter;
                return data;
            }
        }

        public void Free(T[] data)
        {
            if (data == null) return;
            if (data.Length != _size) throw new Exception($"Buffer Free failed. Buffer pool size mismatch: {data.Length}/{_size}");
            {
#if DEBUG_ARRAY_POOL
                int index = _trackedAllocations.IndexOf(data);
                if(index == -1)
                    throw new Exception($"Free invalid pointer");
                _trackedAllocationsStackTraces.RemoveAt(index);
                _trackedAllocations.RemoveAt(index);
#endif

                _buffer.Enqueue(data);
            }

            --_allocCounter;
            Debug.Assert(_allocCounter >= 0);
        }

        public void Dispose()
        {
            {
                while (_buffer.TryDequeue(out _))
                {
                }
            }

#if DEBUG_ARRAY_POOL
            for (int i = 0; i < _trackedAllocationsStackTraces.Count; i++)
            {
                Debug.Log(_trackedAllocationsStackTraces[i]);
            }
#endif
            
            
            if(Volatile.Read(ref _allocCounter) != 0)
                throw new Exception($"Buffer pool disposed but not all memory was deallocated!");

            Interlocked.Exchange(ref _allocCounter, 0);
        }
    }
}