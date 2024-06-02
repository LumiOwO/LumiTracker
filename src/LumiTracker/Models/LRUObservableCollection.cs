using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LumiTracker.Models
{
    public class LRUObservableCollection<T> : ObservableCollection<T> where T : struct
    {
        private readonly Dictionary<T, (LinkedListNode<T> node, int accessCount)> _cache;
        private readonly LinkedList<T> _lruList;

        public LRUObservableCollection()
        {
            _cache = new Dictionary<T, (LinkedListNode<T>, int)>();
            _lruList = new LinkedList<T>();
        }

        public new void Add(T item)
        {
            if (_cache.TryGetValue(item, out var entry))
            {
                // Move the accessed node to the head of the list
                _lruList.Remove(entry.node);
                _lruList.AddFirst(entry.node);
                // Move item to the front in the ObservableCollection
                base.Remove(item);
                base.Insert(0, item);
            }
            else
            {
                // Add the new item
                var newNode = new LinkedListNode<T>(item);
                _lruList.AddFirst(newNode);
                _cache[item] = (newNode, 0); // Initialize access count to 0
                base.Insert(0, item);
            }
        }

        public new T this[int index]
        {
            get
            {
                var item = base[index];
                if (_cache.TryGetValue(item, out var entry))
                {
                    // Move the accessed node to the head of the list
                    _lruList.Remove(entry.node);
                    _lruList.AddFirst(entry.node);
                    // Move item to the front in the ObservableCollection
                    base.Move(index, 0);
                    // Increment access count
                    _cache[item] = (entry.node, entry.accessCount + 1);
                }
                return item;
            }
        }

        public void Reset()
        {
            _cache.Clear();
            _lruList.Clear();
            base.Clear();
        }

        public int GetAccessCount(T item)
        {
            return _cache.TryGetValue(item, out var entry) ? entry.accessCount : 0;
        }
    }
}
