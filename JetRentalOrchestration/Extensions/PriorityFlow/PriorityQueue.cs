// Extensions/PriorityFlow/PriorityQueue.cs
// Bu dosyayı Visual Studio'da Extensions/PriorityFlow klasörüne oluştur

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace JetRentalOrchestration.Extensions.PriorityFlow
{
    /// <summary>
    /// Thread-safe priority queue implementation
    /// Yüksek priority'li item'lar önce çıkar (Critical → High → Normal → Low → Background)
    /// </summary>
    public class PriorityQueue
    {
        // Her priority seviyesi için ayrı queue - thread-safe
        private readonly SortedDictionary<Priority, Queue<PriorityQueueItem>> _queues;
        private readonly object _lock = new object();

        public PriorityQueue()
        {
            // Priority enum değerlerine göre descending sort (10, 7, 5, 3, 1)
            // Bu sayede Critical (10) önce gelir, Background (1) en son gelir
            _queues = new SortedDictionary<Priority, Queue<PriorityQueueItem>>(
                Comparer<Priority>.Create((x, y) => y.CompareTo(x)) // Descending order
            );
        }

        /// <summary>
        /// Queue'ya yeni item ekle
        /// Item'ın priority'sine göre doğru queue'ya eklenir
        /// </summary>
        public void Enqueue(PriorityQueueItem item)
        {
            lock (_lock)
            {
                // Bu priority için queue yoksa oluştur
                if (!_queues.ContainsKey(item.Priority))
                {
                    _queues[item.Priority] = new Queue<PriorityQueueItem>();
                }

                // Uygun priority queue'suna ekle
                _queues[item.Priority].Enqueue(item);
            }
        }

        /// <summary>
        /// En yüksek priority'li item'ı al
        /// Priority sırası: Critical → High → Normal → Low → Background
        /// </summary>
        public PriorityQueueItem? Dequeue()
        {
            lock (_lock)
            {
                // Yüksek priority'den başlayarak kontrol et
                foreach (var kvp in _queues)
                {
                    var queue = kvp.Value;
                    if (queue.Count > 0)
                    {
                        return queue.Dequeue();
                    }
                }

                return null; // Hiçbir queue'da item yok
            }
        }

        /// <summary>
        /// Queue'da bekleyen toplam item sayısı
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _queues.Values.Sum(q => q.Count);
                }
            }
        }

        /// <summary>
        /// Belirli bir priority'de kaç item var
        /// </summary>
        public int GetCountForPriority(Priority priority)
        {
            lock (_lock)
            {
                return _queues.ContainsKey(priority) ? _queues[priority].Count : 0;
            }
        }

        /// <summary>
        /// Queue durumu hakkında bilgi (monitoring için)
        /// </summary>
        public Dictionary<Priority, int> GetQueueStatus()
        {
            lock (_lock)
            {
                var status = new Dictionary<Priority, int>();

                // Tüm priority seviyelerini dahil et (boş olanlar da)
                foreach (Priority priority in Enum.GetValues<Priority>())
                {
                    status[priority] = _queues.ContainsKey(priority) ? _queues[priority].Count : 0;
                }

                return status;
            }
        }

        /// <summary>
        /// Queue'yu temizle (shutdown için)
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _queues.Clear();
            }
        }
    }
}