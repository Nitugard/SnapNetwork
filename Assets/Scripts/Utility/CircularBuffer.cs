using System;

namespace Ibc.Survival
{
    [Serializable]
    public class CircularBuffer<T>
    {
        public int Capacity => _buffer.Length;
        public bool IsFull => _count == Capacity;
        public bool IsEmpty => _count == 0;
        public int Count => _count;
        
#if DEBUG_ENABLE_SERIALIZATION
        [SerializeField] 
#endif
        private T[] _buffer;
#if DEBUG_ENABLE_SERIALIZATION
        [SerializeField] 
#endif
        private int _start;
#if DEBUG_ENABLE_SERIALIZATION
        [SerializeField] 
#endif
        private int _end;
#if DEBUG_ENABLE_SERIALIZATION
        [SerializeField] 
#endif
        private int _count;

        public CircularBuffer(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentException("Circular buffer cannot have negative or zero capacity.", nameof(capacity));

            _buffer = new T[capacity];
            _count = 0;
            _start = 0;
            _end = 0;
        }

        public T Front()
        {
            ThrowIfEmpty();
            return _buffer[_start];
        }

        public T Back()
        {
            ThrowIfEmpty();
            return _buffer[(_end != 0 ? _end : Capacity) - 1];
        }

        public T this[int index]
        {
            get
            {
                if (IsEmpty || index >= _count)
                    throw new IndexOutOfRangeException();
                int actualIndex = InternalIndex(index);
                return _buffer[actualIndex];
            }
            set
            {
                if (IsEmpty || index >= _count)
                    throw new IndexOutOfRangeException();
                int actualIndex = InternalIndex(index);
                _buffer[actualIndex] = value;
            }
        }

        public void PushBack(T item)
        {
            if (IsFull)
            {
                _buffer[_end] = item;
                Increment(ref _end);
                _start = _end;
            }
            else
            {
                _buffer[_end] = item;
                Increment(ref _end);
                ++_count;
            }
        }

        public void PushFront(T item)
        {
            if (IsFull)
            {
                Decrement(ref _start);
                _end = _start;
                _buffer[_start] = item;
            }
            else
            {
                Decrement(ref _start);
                _buffer[_start] = item;
                ++_count;
            }
        }
        
        public void PopBack()
        {
            ThrowIfEmpty("Cannot take elements from an empty buffer.");
            Decrement(ref _end);
            _buffer[_end] = default(T);
            --_count;
        }

        public void PopFront()
        {
            ThrowIfEmpty("Cannot take elements from an empty buffer.");
            _buffer[_start] = default(T);
            Increment(ref _start);
            --_count;
        }

        
        public void Clear()
        {
            _start = 0;
            _end = 0;
            _count = 0;
        }
        
        private void ThrowIfEmpty(string message = "Cannot access an empty buffer.")
        {
            if (IsEmpty)
                throw new InvalidOperationException(message);
        }

        private void Increment(ref int index)
        {
            if (++index == Capacity)
            {
                index = 0;
            }
        }

        private void Decrement(ref int index)
        {
            if (index == 0)
            {
                index = Capacity;
            }
            index--;
        }

        private int InternalIndex(int index)
        {
            return _start + (index < (Capacity - _start) ? index : index - Capacity);
        }
    }
}