// PriorityFlow.Core.Observability - Metrics Interface
// Defines the contract for collecting and exposing runtime metrics

using System;
using System.Collections.Generic;

namespace PriorityFlow.Observability
{
    /// <summary>
    /// Interface for collecting and exposing PriorityFlow runtime metrics
    /// Provides thread-safe metric collection for monitoring and diagnostics
    /// </summary>
    public interface IPriorityFlowMetrics
    {
        #region Queue Metrics

        /// <summary>
        /// Increment the count of items added to the queue
        /// </summary>
        void IncrementQueuedItems();

        /// <summary>
        /// Decrement the count of items removed from the queue
        /// </summary>
        void DecrementQueuedItems();

        /// <summary>
        /// Get the current number of items waiting in the queue
        /// </summary>
        /// <returns>Current queue length</returns>
        long GetCurrentQueueLength();

        /// <summary>
        /// Get the total number of items that have been enqueued since startup
        /// </summary>
        /// <returns>Total enqueued items</returns>
        long GetTotalEnqueuedItems();

        /// <summary>
        /// Get the total number of items that have been processed since startup
        /// </summary>
        /// <returns>Total processed items</returns>
        long GetTotalProcessedItems();

        /// <summary>
        /// Get the current queue length by priority level
        /// </summary>
        /// <returns>Dictionary with priority as key and count as value</returns>
        Dictionary<Priority, long> GetQueueLengthByPriority();

        #endregion

        #region Processing Time Metrics

        /// <summary>
        /// Record the processing time for a request
        /// </summary>
        /// <param name="priority">Priority level of the request</param>
        /// <param name="milliseconds">Processing time in milliseconds</param>
        void RecordProcessingTime(Priority priority, double milliseconds);

        /// <summary>
        /// Get the average processing time for all priorities
        /// </summary>
        /// <returns>Average processing time in milliseconds</returns>
        double GetAverageProcessingTime();

        /// <summary>
        /// Get the average processing time by priority level
        /// </summary>
        /// <returns>Dictionary with priority as key and average time as value</returns>
        Dictionary<Priority, double> GetAverageProcessingTimeByPriority();

        /// <summary>
        /// Get the maximum processing time recorded for each priority
        /// </summary>
        /// <returns>Dictionary with priority as key and max time as value</returns>
        Dictionary<Priority, double> GetMaxProcessingTimeByPriority();

        /// <summary>
        /// Get the minimum processing time recorded for each priority
        /// </summary>
        /// <returns>Dictionary with priority as key and min time as value</returns>
        Dictionary<Priority, double> GetMinProcessingTimeByPriority();

        #endregion

        #region Error Metrics

        /// <summary>
        /// Record a processing error
        /// </summary>
        /// <param name="priority">Priority level of the failed request</param>
        /// <param name="errorType">Type of error (exception type name)</param>
        void RecordError(Priority priority, string errorType);

        /// <summary>
        /// Get the total number of errors since startup
        /// </summary>
        /// <returns>Total error count</returns>
        long GetTotalErrors();

        /// <summary>
        /// Get the total number of errors by priority level
        /// </summary>
        /// <returns>Dictionary with priority as key and error count as value</returns>
        Dictionary<Priority, long> GetErrorsByPriority();

        /// <summary>
        /// Get the most common error types and their counts
        /// </summary>
        /// <returns>Dictionary with error type as key and count as value</returns>
        Dictionary<string, long> GetErrorsByType();

        /// <summary>
        /// Get the error rate (errors per total processed items) as a percentage
        /// </summary>
        /// <returns>Error rate percentage</returns>
        double GetErrorRate();

        #endregion

        #region Throughput Metrics

        /// <summary>
        /// Get the current throughput (items processed per second)
        /// Based on a rolling window of recent activity
        /// </summary>
        /// <returns>Items processed per second</returns>
        double GetCurrentThroughput();

        /// <summary>
        /// Get the peak throughput recorded
        /// </summary>
        /// <returns>Peak items processed per second</returns>
        double GetPeakThroughput();

        /// <summary>
        /// Get throughput by priority level
        /// </summary>
        /// <returns>Dictionary with priority as key and throughput as value</returns>
        Dictionary<Priority, double> GetThroughputByPriority();

        #endregion

        #region Wait Time Metrics

        /// <summary>
        /// Record how long a request waited in the queue before processing
        /// </summary>
        /// <param name="priority">Priority level of the request</param>
        /// <param name="waitTimeMilliseconds">Wait time in milliseconds</param>
        void RecordWaitTime(Priority priority, double waitTimeMilliseconds);

        /// <summary>
        /// Get the average wait time for all priorities
        /// </summary>
        /// <returns>Average wait time in milliseconds</returns>
        double GetAverageWaitTime();

        /// <summary>
        /// Get the average wait time by priority level
        /// </summary>
        /// <returns>Dictionary with priority as key and average wait time as value</returns>
        Dictionary<Priority, double> GetAverageWaitTimeByPriority();

        /// <summary>
        /// Get the maximum wait time recorded by priority
        /// </summary>
        /// <returns>Dictionary with priority as key and max wait time as value</returns>
        Dictionary<Priority, double> GetMaxWaitTimeByPriority();

        #endregion

        #region System Health Metrics

        /// <summary>
        /// Record when the system started
        /// </summary>
        void RecordSystemStart();

        /// <summary>
        /// Get the system uptime
        /// </summary>
        /// <returns>System uptime timespan</returns>
        TimeSpan GetUptime();

        /// <summary>
        /// Get the last activity timestamp
        /// </summary>
        /// <returns>DateTime of last processed request</returns>
        DateTime GetLastActivityTime();

        /// <summary>
        /// Check if the system is considered healthy based on configurable criteria
        /// </summary>
        /// <param name="maxQueueLength">Maximum acceptable queue length</param>
        /// <param name="maxErrorRate">Maximum acceptable error rate percentage</param>
        /// <param name="maxAverageWaitTime">Maximum acceptable average wait time in milliseconds</param>
        /// <returns>True if system is healthy, false otherwise</returns>
        bool IsHealthy(long maxQueueLength = 1000, double maxErrorRate = 5.0, double maxAverageWaitTime = 5000.0);

        #endregion

        #region Utility Methods

        /// <summary>
        /// Reset all metrics to their initial state
        /// Useful for testing or system restart scenarios
        /// </summary>
        void Reset();

        /// <summary>
        /// Get a snapshot of all current metrics
        /// </summary>
        /// <returns>Dictionary containing all metric values</returns>
        Dictionary<string, object> GetSnapshot();

        /// <summary>
        /// Export metrics in a structured format for monitoring systems
        /// </summary>
        /// <returns>Structured metrics data</returns>
        PriorityFlowMetricsSnapshot GetMetricsSnapshot();

        #endregion
    }

    /// <summary>
    /// Structured snapshot of all PriorityFlow metrics at a point in time
    /// </summary>
    public class PriorityFlowMetricsSnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public long CurrentQueueLength { get; set; }
        public long TotalEnqueued { get; set; }
        public long TotalProcessed { get; set; }
        public long TotalErrors { get; set; }
        public double ErrorRate { get; set; }
        public double AverageProcessingTime { get; set; }
        public double AverageWaitTime { get; set; }
        public double CurrentThroughput { get; set; }
        public double PeakThroughput { get; set; }
        public TimeSpan Uptime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public bool IsHealthy { get; set; }

        // Priority-specific metrics
        public Dictionary<Priority, long> QueueLengthByPriority { get; set; } = new();
        public Dictionary<Priority, double> AverageProcessingTimeByPriority { get; set; } = new();
        public Dictionary<Priority, double> AverageWaitTimeByPriority { get; set; } = new();
        public Dictionary<Priority, long> ErrorsByPriority { get; set; } = new();
        public Dictionary<Priority, double> ThroughputByPriority { get; set; } = new();
        
        // Error breakdown
        public Dictionary<string, long> ErrorsByType { get; set; } = new();

        /// <summary>
        /// Get a human-readable summary of the metrics
        /// </summary>
        public string GetSummary()
        {
            return $"PriorityFlow Metrics [{Timestamp:yyyy-MM-dd HH:mm:ss}]: " +
                   $"Queue: {CurrentQueueLength}, Processed: {TotalProcessed}, " +
                   $"Errors: {TotalErrors} ({ErrorRate:F2}%), " +
                   $"Avg Processing: {AverageProcessingTime:F1}ms, " +
                   $"Throughput: {CurrentThroughput:F1}/sec, " +
                   $"Healthy: {IsHealthy}";
        }
    }
}