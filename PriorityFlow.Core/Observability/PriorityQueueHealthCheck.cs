// PriorityFlow.Core.Observability - Health Check Implementation
// ASP.NET Core Health Check integration for monitoring queue health

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace PriorityFlow.Observability
{
    /// <summary>
    /// Health check implementation for PriorityFlow queue monitoring
    /// Integrates with ASP.NET Core Health Checks to monitor queue health
    /// </summary>
    public class PriorityQueueHealthCheck : IHealthCheck
    {
        private readonly IPriorityFlowMetrics _metrics;
        private readonly PriorityFlowConfiguration _configuration;
        private readonly ILogger<PriorityQueueHealthCheck> _logger;

        public PriorityQueueHealthCheck(
            IPriorityFlowMetrics metrics,
            PriorityFlowConfiguration configuration,
            ILogger<PriorityQueueHealthCheck> logger)
        {
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Performs the health check evaluation
        /// </summary>
        /// <param name="context">Health check context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var snapshot = _metrics.GetMetricsSnapshot();
                var healthData = new Dictionary<string, object>();

                // Collect basic metrics for health check response
                healthData["timestamp"] = snapshot.Timestamp;
                healthData["queueLength"] = snapshot.CurrentQueueLength;
                healthData["totalProcessed"] = snapshot.TotalProcessed;
                healthData["totalErrors"] = snapshot.TotalErrors;
                healthData["errorRate"] = $"{snapshot.ErrorRate:F2}%";
                healthData["averageProcessingTime"] = $"{snapshot.AverageProcessingTime:F1}ms";
                healthData["averageWaitTime"] = $"{snapshot.AverageWaitTime:F1}ms";
                healthData["currentThroughput"] = $"{snapshot.CurrentThroughput:F1}/sec";
                healthData["uptime"] = snapshot.Uptime.ToString(@"dd\.hh\:mm\:ss");
                healthData["lastActivity"] = snapshot.LastActivityTime;

                // Add priority-specific data
                healthData["queueByPriority"] = snapshot.QueueLengthByPriority;
                healthData["processingTimeByPriority"] = FormatProcessingTimes(snapshot.AverageProcessingTimeByPriority);
                healthData["errorsByPriority"] = snapshot.ErrorsByPriority;
                healthData["throughputByPriority"] = FormatThroughput(snapshot.ThroughputByPriority);

                // Perform health evaluation
                var healthResult = await EvaluateHealthAsync(snapshot, healthData, cancellationToken);

                _logger.LogDebug("Health check completed: {Status} - Queue: {QueueLength}, Errors: {ErrorRate}%, " +
                                "Processing: {ProcessingTime}ms, Wait: {WaitTime}ms",
                    healthResult.Status, snapshot.CurrentQueueLength, snapshot.ErrorRate,
                    snapshot.AverageProcessingTime, snapshot.AverageWaitTime);

                return healthResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed with exception: {ErrorMessage}", ex.Message);
                
                return HealthCheckResult.Unhealthy(
                    "Health check failed due to exception",
                    ex,
                    new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["timestamp"] = DateTime.UtcNow
                    });
            }
        }

        /// <summary>
        /// Evaluates the health status based on metrics and configuration
        /// </summary>
        private async Task<HealthCheckResult> EvaluateHealthAsync(
            PriorityFlowMetricsSnapshot snapshot,
            Dictionary<string, object> healthData,
            CancellationToken cancellationToken)
        {
            var issues = new List<string>();
            var warnings = new List<string>();

            // Check queue length
            var maxQueueLength = _configuration.QueueLengthThreshold;
            if (snapshot.CurrentQueueLength > maxQueueLength)
            {
                issues.Add($"Queue length ({snapshot.CurrentQueueLength}) exceeds threshold ({maxQueueLength})");
            }
            else if (snapshot.CurrentQueueLength > maxQueueLength * 0.8)
            {
                warnings.Add($"Queue length ({snapshot.CurrentQueueLength}) is approaching threshold ({maxQueueLength})");
            }

            // Check error rate
            var maxErrorRate = 5.0; // 5% error rate threshold
            if (snapshot.ErrorRate > maxErrorRate)
            {
                issues.Add($"Error rate ({snapshot.ErrorRate:F2}%) exceeds threshold ({maxErrorRate}%)");
            }
            else if (snapshot.ErrorRate > maxErrorRate * 0.8)
            {
                warnings.Add($"Error rate ({snapshot.ErrorRate:F2}%) is approaching threshold ({maxErrorRate}%)");
            }

            // Check average processing time
            var maxProcessingTime = _configuration.SlowCommandThresholdMs;
            if (snapshot.AverageProcessingTime > maxProcessingTime)
            {
                issues.Add($"Average processing time ({snapshot.AverageProcessingTime:F1}ms) exceeds threshold ({maxProcessingTime}ms)");
            }
            else if (snapshot.AverageProcessingTime > maxProcessingTime * 0.8)
            {
                warnings.Add($"Average processing time ({snapshot.AverageProcessingTime:F1}ms) is approaching threshold ({maxProcessingTime}ms)");
            }

            // Check average wait time
            var maxWaitTime = maxProcessingTime * 2; // Wait time should not be more than 2x processing time
            if (snapshot.AverageWaitTime > maxWaitTime)
            {
                issues.Add($"Average wait time ({snapshot.AverageWaitTime:F1}ms) exceeds threshold ({maxWaitTime}ms)");
            }
            else if (snapshot.AverageWaitTime > maxWaitTime * 0.8)
            {
                warnings.Add($"Average wait time ({snapshot.AverageWaitTime:F1}ms) is approaching threshold ({maxWaitTime}ms)");
            }

            // Check system inactivity
            var timeSinceLastActivity = DateTime.UtcNow - snapshot.LastActivityTime;
            var maxInactivityTime = TimeSpan.FromMinutes(10);
            if (timeSinceLastActivity > maxInactivityTime && snapshot.CurrentQueueLength > 0)
            {
                issues.Add($"System has been inactive for {timeSinceLastActivity:mm\\:ss} with {snapshot.CurrentQueueLength} items in queue");
            }

            // Check for priority imbalance (high priority items waiting too long)
            await CheckPriorityImbalanceAsync(snapshot, issues, warnings, cancellationToken);

            // Add warnings and issues to health data
            if (warnings.Any())
            {
                healthData["warnings"] = warnings;
            }

            if (issues.Any())
            {
                healthData["issues"] = issues;
                var description = "PriorityFlow health issues detected: " + string.Join("; ", issues);
                
                _logger.LogWarning("PriorityFlow unhealthy: {Description}", description);
                
                return HealthCheckResult.Unhealthy(description, data: healthData);
            }

            if (warnings.Any())
            {
                var description = "PriorityFlow has warnings: " + string.Join("; ", warnings);
                
                _logger.LogInformation("PriorityFlow degraded: {Description}", description);
                
                return HealthCheckResult.Degraded(description, data: healthData);
            }

            _logger.LogDebug("PriorityFlow healthy: Queue: {QueueLength}, Throughput: {Throughput}/sec",
                snapshot.CurrentQueueLength, snapshot.CurrentThroughput);

            return HealthCheckResult.Healthy("PriorityFlow is operating normally", healthData);
        }

        /// <summary>
        /// Check for priority imbalance issues
        /// </summary>
        private async Task CheckPriorityImbalanceAsync(
            PriorityFlowMetricsSnapshot snapshot,
            List<string> issues,
            List<string> warnings,
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask; // Placeholder for async operations

            // Check if high priority items are waiting too long
            if (snapshot.QueueLengthByPriority.TryGetValue(Priority.High, out var highPriorityCount) && highPriorityCount > 0)
            {
                if (snapshot.AverageWaitTimeByPriority.TryGetValue(Priority.High, out var highPriorityWaitTime))
                {
                    var maxHighPriorityWait = _configuration.SlowCommandThresholdMs / 2; // High priority should be processed faster
                    if (highPriorityWaitTime > maxHighPriorityWait)
                    {
                        issues.Add($"High priority items waiting too long ({highPriorityWaitTime:F1}ms avg, {highPriorityCount} items)");
                    }
                }
            }

            // Check for throughput imbalance
            var totalThroughput = snapshot.ThroughputByPriority.Values.Sum();
            if (totalThroughput > 0)
            {
                var highPriorityThroughputRatio = snapshot.ThroughputByPriority.GetValueOrDefault(Priority.High, 0) / totalThroughput;
                var normalPriorityThroughputRatio = snapshot.ThroughputByPriority.GetValueOrDefault(Priority.Normal, 0) / totalThroughput;
                var lowPriorityThroughputRatio = snapshot.ThroughputByPriority.GetValueOrDefault(Priority.Low, 0) / totalThroughput;

                // High priority should have reasonable representation unless there are no high priority items
                if (snapshot.QueueLengthByPriority.GetValueOrDefault(Priority.High, 0) > 0 && highPriorityThroughputRatio < 0.1)
                {
                    warnings.Add($"Low high-priority throughput ratio ({highPriorityThroughputRatio:P1})");
                }
            }
        }

        /// <summary>
        /// Format processing times for health check response
        /// </summary>
        private Dictionary<string, string> FormatProcessingTimes(Dictionary<Priority, double> processingTimes)
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in processingTimes)
            {
                result[kvp.Key.ToString()] = $"{kvp.Value:F1}ms";
            }
            return result;
        }

        /// <summary>
        /// Format throughput for health check response
        /// </summary>
        private Dictionary<string, string> FormatThroughput(Dictionary<Priority, double> throughput)
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in throughput)
            {
                result[kvp.Key.ToString()] = $"{kvp.Value:F1}/sec";
            }
            return result;
        }
    }
}