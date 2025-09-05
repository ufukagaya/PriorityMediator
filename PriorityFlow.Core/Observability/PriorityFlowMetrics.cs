// PriorityFlow.Core.Observability - Metrics Implementation
// Thread-safe metrics collection using concurrent data structures

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace PriorityFlow.Observability
{
    /// <summary>
    /// Thread-safe implementation of PriorityFlow metrics collection
    /// Uses atomic operations and concurrent collections for high-performance metric recording
    /// </summary>
    public class PriorityFlowMetrics : IPriorityFlowMetrics
    {
        private readonly ILogger<PriorityFlowMetrics>? _logger;
        private readonly object _lockObject = new();

        // System metrics
        private DateTime _systemStartTime;
        private DateTime _lastActivityTime;

        // Queue metrics - using Interlocked for atomic operations
        private long _currentQueueLength;
        private long _totalEnqueuedItems;
        private long _totalProcessedItems;
        private readonly ConcurrentDictionary<Priority, long> _queueLengthByPriority;

        // Processing time metrics
        private readonly ConcurrentDictionary<Priority, ConcurrentQueue<double>> _processingTimes;
        private readonly ConcurrentDictionary<Priority, double> _totalProcessingTime;
        private readonly ConcurrentDictionary<Priority, long> _processingTimeCount;
        private readonly ConcurrentDictionary<Priority, double> _maxProcessingTime;
        private readonly ConcurrentDictionary<Priority, double> _minProcessingTime;

        // Wait time metrics
        private readonly ConcurrentDictionary<Priority, ConcurrentQueue<double>> _waitTimes;
        private readonly ConcurrentDictionary<Priority, double> _totalWaitTime;
        private readonly ConcurrentDictionary<Priority, long> _waitTimeCount;
        private readonly ConcurrentDictionary<Priority, double> _maxWaitTime;

        // Error metrics
        private long _totalErrors;
        private readonly ConcurrentDictionary<Priority, long> _errorsByPriority;
        private readonly ConcurrentDictionary<string, long> _errorsByType;

        // Throughput metrics - rolling window
        private readonly ConcurrentQueue<(DateTime timestamp, Priority priority)> _recentActivity;
        private double _peakThroughput;
        private readonly TimeSpan _throughputWindowSize = TimeSpan.FromMinutes(1);

        public PriorityFlowMetrics(ILogger<PriorityFlowMetrics>? logger = null)
        {
            _logger = logger;
            var now = DateTime.UtcNow;
            _systemStartTime = now;
            _lastActivityTime = now;

            // Initialize concurrent collections
            _queueLengthByPriority = new ConcurrentDictionary<Priority, long>();
            _processingTimes = new ConcurrentDictionary<Priority, ConcurrentQueue<double>>();
            _totalProcessingTime = new ConcurrentDictionary<Priority, double>();
            _processingTimeCount = new ConcurrentDictionary<Priority, long>();
            _maxProcessingTime = new ConcurrentDictionary<Priority, double>();
            _minProcessingTime = new ConcurrentDictionary<Priority, double>();
            _waitTimes = new ConcurrentDictionary<Priority, ConcurrentQueue<double>>();
            _totalWaitTime = new ConcurrentDictionary<Priority, double>();
            _waitTimeCount = new ConcurrentDictionary<Priority, long>();
            _maxWaitTime = new ConcurrentDictionary<Priority, double>();
            _errorsByPriority = new ConcurrentDictionary<Priority, long>();
            _errorsByType = new ConcurrentDictionary<string, long>();
            _recentActivity = new ConcurrentQueue<(DateTime, Priority)>();

            // Initialize priority-specific collections
            foreach (Priority priority in Enum.GetValues<Priority>())
            {
                _queueLengthByPriority[priority] = 0;
                _processingTimes[priority] = new ConcurrentQueue<double>();
                _totalProcessingTime[priority] = 0.0;
                _processingTimeCount[priority] = 0;
                _maxProcessingTime[priority] = 0.0;
                _minProcessingTime[priority] = double.MaxValue;
                _waitTimes[priority] = new ConcurrentQueue<double>();
                _totalWaitTime[priority] = 0.0;
                _waitTimeCount[priority] = 0;
                _maxWaitTime[priority] = 0.0;
                _errorsByPriority[priority] = 0;
            }

            _logger?.LogInformation("PriorityFlowMetrics initialized at {StartTime}", _systemStartTime);
        }

        #region Queue Metrics

        public void IncrementQueuedItems()
        {
            Interlocked.Increment(ref _currentQueueLength);
            Interlocked.Increment(ref _totalEnqueuedItems);
            
            lock (_lockObject)
            {
                _lastActivityTime = DateTime.UtcNow;
            }

            _logger?.LogDebug("Queue length incremented to {QueueLength}", _currentQueueLength);
        }

        public void DecrementQueuedItems()
        {
            Interlocked.Decrement(ref _currentQueueLength);
            Interlocked.Increment(ref _totalProcessedItems);
            
            lock (_lockObject)
            {
                _lastActivityTime = DateTime.UtcNow;
            }

            _logger?.LogDebug("Queue length decremented to {QueueLength}", _currentQueueLength);
        }

        public long GetCurrentQueueLength() => Interlocked.Read(ref _currentQueueLength);

        public long GetTotalEnqueuedItems() => Interlocked.Read(ref _totalEnqueuedItems);

        public long GetTotalProcessedItems() => Interlocked.Read(ref _totalProcessedItems);

        public Dictionary<Priority, long> GetQueueLengthByPriority()
        {
            return _queueLengthByPriority.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        #endregion

        #region Processing Time Metrics

        public void RecordProcessingTime(Priority priority, double milliseconds)
        {
            if (milliseconds < 0)
            {
                _logger?.LogWarning("Negative processing time recorded: {Time}ms for priority {Priority}", milliseconds, priority);
                return;
            }

            // Add to rolling collection (keeping last 1000 entries per priority for memory efficiency)
            var queue = _processingTimes[priority];
            queue.Enqueue(milliseconds);

            // Keep only recent entries (memory management)
            while (queue.Count > 1000)
            {
                queue.TryDequeue(out _);
            }

            // Update aggregate statistics
            lock (_lockObject)
            {
                _totalProcessingTime[priority] += milliseconds;
                _processingTimeCount[priority]++;

                if (milliseconds > _maxProcessingTime[priority])
                    _maxProcessingTime[priority] = milliseconds;

                if (milliseconds < _minProcessingTime[priority])
                    _minProcessingTime[priority] = milliseconds;
            }

            // Record activity for throughput calculation
            var now = DateTime.UtcNow;
            _recentActivity.Enqueue((now, priority));
            
            lock (_lockObject)
            {
                _lastActivityTime = now;
            }

            // Update peak throughput
            var currentThroughput = GetCurrentThroughput();
            if (currentThroughput > _peakThroughput)
                _peakThroughput = currentThroughput;

            _logger?.LogDebug("Processing time recorded: {Time}ms for priority {Priority}", milliseconds, priority);
        }

        public double GetAverageProcessingTime()
        {
            var totalTime = _totalProcessingTime.Values.Sum();
            var totalCount = _processingTimeCount.Values.Sum();
            return totalCount > 0 ? totalTime / totalCount : 0.0;
        }

        public Dictionary<Priority, double> GetAverageProcessingTimeByPriority()
        {
            return _totalProcessingTime.ToDictionary(
                kvp => kvp.Key,
                kvp => _processingTimeCount[kvp.Key] > 0 ? kvp.Value / _processingTimeCount[kvp.Key] : 0.0
            );
        }

        public Dictionary<Priority, double> GetMaxProcessingTimeByPriority()
        {
            return _maxProcessingTime.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value == 0.0 ? 0.0 : kvp.Value
            );
        }

        public Dictionary<Priority, double> GetMinProcessingTimeByPriority()
        {
            return _minProcessingTime.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value == double.MaxValue ? 0.0 : kvp.Value
            );
        }

        #endregion

        #region Error Metrics

        public void RecordError(Priority priority, string errorType)
        {
            Interlocked.Increment(ref _totalErrors);
            _errorsByPriority.AddOrUpdate(priority, 1, (key, value) => value + 1);
            _errorsByType.AddOrUpdate(errorType, 1, (key, value) => value + 1);
            
            lock (_lockObject)
            {
                _lastActivityTime = DateTime.UtcNow;
            }

            _logger?.LogDebug("Error recorded: {ErrorType} for priority {Priority}", errorType, priority);
        }

        public long GetTotalErrors() => Interlocked.Read(ref _totalErrors);

        public Dictionary<Priority, long> GetErrorsByPriority()
        {
            return _errorsByPriority.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public Dictionary<string, long> GetErrorsByType()
        {
            return _errorsByType.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public double GetErrorRate()
        {
            var totalProcessed = GetTotalProcessedItems();
            var totalErrors = GetTotalErrors();
            return totalProcessed > 0 ? (double)totalErrors / totalProcessed * 100.0 : 0.0;
        }

        #endregion

        #region Throughput Metrics

        public double GetCurrentThroughput()
        {
            CleanupOldActivity();
            
            var cutoffTime = DateTime.UtcNow - _throughputWindowSize;
            var recentCount = 0;

            // Count recent activity within the window
            foreach (var (timestamp, _) in _recentActivity)
            {
                if (timestamp >= cutoffTime)
                    recentCount++;
            }

            return recentCount / _throughputWindowSize.TotalSeconds;
        }

        public double GetPeakThroughput() => _peakThroughput;

        public Dictionary<Priority, double> GetThroughputByPriority()
        {
            CleanupOldActivity();
            
            var cutoffTime = DateTime.UtcNow - _throughputWindowSize;
            var priorityCounts = new Dictionary<Priority, int>();

            foreach (Priority priority in Enum.GetValues<Priority>())
            {
                priorityCounts[priority] = 0;
            }

            // Count recent activity by priority within the window
            foreach (var (timestamp, priority) in _recentActivity)
            {
                if (timestamp >= cutoffTime)
                    priorityCounts[priority]++;
            }

            return priorityCounts.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value / _throughputWindowSize.TotalSeconds
            );
        }

        private void CleanupOldActivity()
        {
            var cutoffTime = DateTime.UtcNow - _throughputWindowSize;
            
            while (_recentActivity.TryPeek(out var activity) && activity.timestamp < cutoffTime)
            {
                _recentActivity.TryDequeue(out _);
            }
        }

        #endregion

        #region Wait Time Metrics

        public void RecordWaitTime(Priority priority, double waitTimeMilliseconds)
        {
            if (waitTimeMilliseconds < 0)
            {
                _logger?.LogWarning("Negative wait time recorded: {Time}ms for priority {Priority}", waitTimeMilliseconds, priority);
                return;
            }

            // Add to rolling collection
            var queue = _waitTimes[priority];
            queue.Enqueue(waitTimeMilliseconds);

            // Keep only recent entries (memory management)
            while (queue.Count > 1000)
            {
                queue.TryDequeue(out _);
            }

            // Update aggregate statistics
            lock (_lockObject)
            {
                _totalWaitTime[priority] += waitTimeMilliseconds;
                _waitTimeCount[priority]++;

                if (waitTimeMilliseconds > _maxWaitTime[priority])
                    _maxWaitTime[priority] = waitTimeMilliseconds;
            }

            _logger?.LogDebug("Wait time recorded: {Time}ms for priority {Priority}", waitTimeMilliseconds, priority);
        }

        public double GetAverageWaitTime()
        {
            var totalWaitTime = _totalWaitTime.Values.Sum();
            var totalCount = _waitTimeCount.Values.Sum();
            return totalCount > 0 ? totalWaitTime / totalCount : 0.0;
        }

        public Dictionary<Priority, double> GetAverageWaitTimeByPriority()
        {
            return _totalWaitTime.ToDictionary(
                kvp => kvp.Key,
                kvp => _waitTimeCount[kvp.Key] > 0 ? kvp.Value / _waitTimeCount[kvp.Key] : 0.0
            );
        }

        public Dictionary<Priority, double> GetMaxWaitTimeByPriority()
        {
            return _maxWaitTime.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        #endregion

        #region System Health Metrics

        public void RecordSystemStart()
        {
            var now = DateTime.UtcNow;
            lock (_lockObject)
            {
                _systemStartTime = now;
                _lastActivityTime = now;
            }
            _logger?.LogInformation("System start time recorded: {StartTime}", now);
        }

        public TimeSpan GetUptime() => DateTime.UtcNow - _systemStartTime;

        public DateTime GetLastActivityTime() 
        {
            lock (_lockObject)
            {
                return _lastActivityTime;
            }
        }

        public bool IsHealthy(long maxQueueLength = 1000, double maxErrorRate = 5.0, double maxAverageWaitTime = 5000.0)
        {
            var currentQueueLength = GetCurrentQueueLength();
            var errorRate = GetErrorRate();
            var averageWaitTime = GetAverageWaitTime();

            var isHealthy = currentQueueLength <= maxQueueLength &&
                           errorRate <= maxErrorRate &&
                           averageWaitTime <= maxAverageWaitTime;

            _logger?.LogDebug("Health check: Queue: {QueueLength}/{MaxQueue}, Error Rate: {ErrorRate:F2}%/{MaxError}%, " +
                             "Avg Wait: {AvgWait:F1}/{MaxWait}ms - {Status}",
                currentQueueLength, maxQueueLength, errorRate, maxErrorRate, 
                averageWaitTime, maxAverageWaitTime, isHealthy ? "HEALTHY" : "UNHEALTHY");

            return isHealthy;
        }

        #endregion

        #region Utility Methods

        public void Reset()
        {
            _logger?.LogInformation("Resetting all metrics");

            lock (_lockObject)
            {
                _systemStartTime = DateTime.UtcNow;
                _lastActivityTime = _systemStartTime;
                _currentQueueLength = 0;
                _totalEnqueuedItems = 0;
                _totalProcessedItems = 0;
                _totalErrors = 0;
                _peakThroughput = 0.0;

                _queueLengthByPriority.Clear();
                _errorsByPriority.Clear();
                _errorsByType.Clear();

                foreach (Priority priority in Enum.GetValues<Priority>())
                {
                    _queueLengthByPriority[priority] = 0;
                    _processingTimes[priority].Clear();
                    _totalProcessingTime[priority] = 0.0;
                    _processingTimeCount[priority] = 0;
                    _maxProcessingTime[priority] = 0.0;
                    _minProcessingTime[priority] = double.MaxValue;
                    _waitTimes[priority].Clear();
                    _totalWaitTime[priority] = 0.0;
                    _waitTimeCount[priority] = 0;
                    _maxWaitTime[priority] = 0.0;
                    _errorsByPriority[priority] = 0;
                }

                // Clear recent activity
                while (_recentActivity.TryDequeue(out _)) { }
            }
        }

        public Dictionary<string, object> GetSnapshot()
        {
            return new Dictionary<string, object>
            {
                ["Timestamp"] = DateTime.UtcNow,
                ["CurrentQueueLength"] = GetCurrentQueueLength(),
                ["TotalEnqueued"] = GetTotalEnqueuedItems(),
                ["TotalProcessed"] = GetTotalProcessedItems(),
                ["TotalErrors"] = GetTotalErrors(),
                ["ErrorRate"] = GetErrorRate(),
                ["AverageProcessingTime"] = GetAverageProcessingTime(),
                ["AverageWaitTime"] = GetAverageWaitTime(),
                ["CurrentThroughput"] = GetCurrentThroughput(),
                ["PeakThroughput"] = GetPeakThroughput(),
                ["Uptime"] = GetUptime(),
                ["LastActivityTime"] = GetLastActivityTime(),
                ["IsHealthy"] = IsHealthy()
            };
        }

        public PriorityFlowMetricsSnapshot GetMetricsSnapshot()
        {
            return new PriorityFlowMetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                CurrentQueueLength = GetCurrentQueueLength(),
                TotalEnqueued = GetTotalEnqueuedItems(),
                TotalProcessed = GetTotalProcessedItems(),
                TotalErrors = GetTotalErrors(),
                ErrorRate = GetErrorRate(),
                AverageProcessingTime = GetAverageProcessingTime(),
                AverageWaitTime = GetAverageWaitTime(),
                CurrentThroughput = GetCurrentThroughput(),
                PeakThroughput = GetPeakThroughput(),
                Uptime = GetUptime(),
                LastActivityTime = GetLastActivityTime(),
                IsHealthy = IsHealthy(),
                QueueLengthByPriority = GetQueueLengthByPriority(),
                AverageProcessingTimeByPriority = GetAverageProcessingTimeByPriority(),
                AverageWaitTimeByPriority = GetAverageWaitTimeByPriority(),
                ErrorsByPriority = GetErrorsByPriority(),
                ThroughputByPriority = GetThroughputByPriority(),
                ErrorsByType = GetErrorsByType()
            };
        }

        #endregion
    }
}