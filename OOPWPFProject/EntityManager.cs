using System;
using System.Collections.Generic;

namespace OOPWPFProject
{
    public class EntityManager<T>
    {
        private List<T> _items = new List<T>();

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _items.Count)
                    throw new IndexOutOfRangeException($"Індекс {index} виходить за межі списку.");
                return _items[index];
            }
            set
            {
                if (index < 0 || index >= _items.Count)
                    throw new IndexOutOfRangeException($"Індекс {index} виходить за межі списку.");
                _items[index] = value;
            }
        }

        public void Add(T item) => _items.Add(item);

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _items.Count)
                throw new IndexOutOfRangeException($"Індекс {index} виходить за межі списку.");
            _items.RemoveAt(index);
        }

        public int Count => _items.Count;
    }
}