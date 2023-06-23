using System;
using System.Collections.Generic;

namespace Ibc.Survival
{
    
    /// <summary>
    /// Thread safe pool of array pool. <see cref="ArrayPool{T}"/>
    /// </summary>
    public class ArrayPoolPool<T> where T : unmanaged
    {
        private readonly List<ArrayPool<T>> _cellPool;

        public ArrayPoolPool(int initMaxSizePow2 = 10, int initCapacity = 1)
        {
            _cellPool = new List<ArrayPool<T>>();
            for (int i = 0; i <= initMaxSizePow2; ++i)
            {
                _cellPool.Add(new ArrayPool<T>(1 << i, initCapacity));
            }
        }

        private ArrayPool<T> GetBufferCellPool(int size)
        {
            if (size <= 0) throw new Exception($"Alloc failed, invalid size: {size}!");

            //gets the minimum size of power of 2 that can hold the required size
            int reqExp = (int) Math.Ceiling(Math.Log(size, 2));
            return _cellPool[reqExp];
        }

        public T[] Alloc(int size)
        {
            if (size <= 0)
                throw new Exception($"Alloc failed. Invalid buffer size {size}");

            var bufferCell = GetBufferCellPool(size);
            if(bufferCell == null)
                throw new Exception($"Could not find buffer pool cell that fits the size: {size}");

            return bufferCell.Alloc();
        }

        public void Free(T[] data)
        {
            if (data == null)
                return;
            var cellPool = GetBufferCellPool(data.Length);
            cellPool.Free(data);
        }

        public void Dispose()
        {
            foreach(var cell in _cellPool)
                cell.Dispose();
        }
        
        public int Allocations()
        {
            int counter = 0;
            foreach (var cell in _cellPool)
                counter += cell.Allocations;
            return counter;
        }
    }
}