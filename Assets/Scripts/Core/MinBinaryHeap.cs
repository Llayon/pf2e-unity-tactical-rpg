using System;
using System.Collections.Generic;

namespace PF2e.Core
{
    /// <summary>
    /// Min binary heap (priority queue). Lowest priority dequeued first.
    /// No decrease-key — use lazy deletion pattern instead.
    /// </summary>
    public class MinBinaryHeap<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private readonly List<(TElement element, TPriority priority)> heap = new();

        public int Count => heap.Count;

        public void Clear() => heap.Clear();

        public void Enqueue(TElement element, TPriority priority)
        {
            heap.Add((element, priority));
            BubbleUp(heap.Count - 1);
        }

        public TElement Dequeue()
        {
            if (heap.Count == 0)
                throw new InvalidOperationException("Heap is empty");

            var result = heap[0].element;
            int last = heap.Count - 1;

            heap[0] = heap[last];
            heap.RemoveAt(last);

            if (heap.Count > 0)
                BubbleDown(0);

            return result;
        }

        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (heap[index].priority.CompareTo(heap[parent].priority) < 0)
                {
                    (heap[index], heap[parent]) = (heap[parent], heap[index]);
                    index = parent;
                }
                else break;
            }
        }

        private void BubbleDown(int index)
        {
            int count = heap.Count;
            while (true)
            {
                int left = 2 * index + 1;
                int right = 2 * index + 2;
                int smallest = index;

                if (left < count && heap[left].priority.CompareTo(heap[smallest].priority) < 0)
                    smallest = left;
                if (right < count && heap[right].priority.CompareTo(heap[smallest].priority) < 0)
                    smallest = right;

                if (smallest == index) break;

                (heap[index], heap[smallest]) = (heap[smallest], heap[index]);
                index = smallest;
            }
        }
    }
}
