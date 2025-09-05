// PriorityFlow.Core.Behaviors - Performance Monitoring Behavior
// Monitors request execution time and logs warnings for slow operations

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PriorityFlow.Behaviors
{
    /// <summary>
    /// Pipeline behavior that monitors request execution performance
    /// Logs warnings when requests exceed configured time thresholds
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    public class PerformanceMonitoringBehavior<TRequest> : IPipelineBehavior<TRequest>
        where TRequest : IRequest
    {
        private readonly ILogger<PerformanceMonitoringBehavior<TRequest>> _logger;
        private readonly PriorityFlowConfiguration _configuration;

        public PerformanceMonitoringBehavior(
            ILogger<PerformanceMonitoringBehavior<TRequest>> logger,
            PriorityFlowConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Handle performance monitoring for requests without response
        /// </summary>
        public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken)
        {
            var requestTypeName = typeof(TRequest).Name;
            var requestId = Guid.NewGuid();
            
            if (_configuration.EnablePerformanceTracking)
            {
                _logger.LogDebug("⏱️ Starting performance monitoring for {RequestType} [{RequestId}]", 
                    requestTypeName, requestId);
            }

            var stopwatch = Stopwatch.StartNew();
            var startTime = DateTime.UtcNow;
            Exception? exception = null;

            try
            {
                await next();
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;
                var endTime = DateTime.UtcNow;

                await LogPerformanceMetrics(requestTypeName, requestId, elapsedMs, startTime, endTime, exception);
            }
        }

        /// <summary>
        /// Log performance metrics and warnings
        /// </summary>
        private async Task LogPerformanceMetrics(
            string requestTypeName, 
            Guid requestId, 
            long elapsedMs, 
            DateTime startTime, 
            DateTime endTime,
            Exception? exception)
        {
            if (!_configuration.EnablePerformanceTracking)
                return;

            var priority = PriorityConventions.GetConventionBasedPriority(typeof(TRequest));
            var status = exception == null ? "Success" : "Failed";
            var statusEmoji = exception == null ? "✅" : "❌";

            // Always log basic performance info in debug mode
            _logger.LogDebug("{StatusEmoji} {RequestType} [{RequestId}] completed in {ElapsedMs}ms (Status: {Status}, Priority: {Priority})",
                statusEmoji, requestTypeName, requestId, elapsedMs, status, priority);

            // Log warning if the request exceeded the slow threshold
            if (_configuration.EnablePerformanceAlerts && elapsedMs > _configuration.SlowCommandThresholdMs)
            {
                var thresholdMs = _configuration.SlowCommandThresholdMs;
                var overageMs = elapsedMs - thresholdMs;
                
                _logger.LogWarning("🐌 SLOW REQUEST: {RequestType} [{RequestId}] took {ElapsedMs}ms, which exceeds the {ThresholdMs}ms threshold by {OverageMs}ms. " +
                                   "Priority: {Priority}, Status: {Status}, Started: {StartTime:HH:mm:ss.fff}, Ended: {EndTime:HH:mm:ss.fff}",
                    requestTypeName, requestId, elapsedMs, thresholdMs, overageMs, priority, status, startTime, endTime);

                // Log additional context for failed slow requests
                if (exception != null)
                {
                    _logger.LogError("💥 Slow request {RequestType} [{RequestId}] also failed with exception: {ExceptionMessage}",
                        requestTypeName, requestId, exception.Message);
                }
            }
            else if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("⚡ {RequestType} [{RequestId}] completed in {ElapsedMs}ms (within {ThresholdMs}ms threshold)",
                    requestTypeName, requestId, elapsedMs, _configuration.SlowCommandThresholdMs);
            }

            // Record metrics for potential collection by monitoring systems
            await RecordPerformanceMetrics(requestTypeName, priority, elapsedMs, status, startTime);
        }

        /// <summary>
        /// Record performance metrics for monitoring systems
        /// This method can be extended to integrate with application metrics systems
        /// </summary>
        private async Task RecordPerformanceMetrics(
            string requestTypeName, 
            Priority priority, 
            long elapsedMs, 
            string status,
            DateTime startTime)
        {
            // This is a hook for future metrics integration
            // Could integrate with:
            // - Application Insights
            // - Prometheus
            // - Custom metrics collection
            // - Performance counters

            await Task.CompletedTask; // Placeholder for async metrics recording

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogDebug("📊 Metrics recorded: {RequestType} | Priority: {Priority} | Duration: {ElapsedMs}ms | Status: {Status} | Time: {StartTime:yyyy-MM-dd HH:mm:ss}",
                    requestTypeName, priority, elapsedMs, status, startTime);
            }
        }
    }

    /// <summary>
    /// Pipeline behavior that monitors performance for requests with response
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public class PerformanceMonitoringBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<PerformanceMonitoringBehavior<TRequest, TResponse>> _logger;
        private readonly PriorityFlowConfiguration _configuration;

        public PerformanceMonitoringBehavior(
            ILogger<PerformanceMonitoringBehavior<TRequest, TResponse>> logger,
            PriorityFlowConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Handle performance monitoring for requests with response
        /// </summary>
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestTypeName = typeof(TRequest).Name;
            var responseTypeName = typeof(TResponse).Name;
            var requestId = Guid.NewGuid();
            
            if (_configuration.EnablePerformanceTracking)
            {
                _logger.LogDebug("⏱️ Starting performance monitoring for {RequestType} -> {ResponseType} [{RequestId}]", 
                    requestTypeName, responseTypeName, requestId);
            }

            var stopwatch = Stopwatch.StartNew();
            var startTime = DateTime.UtcNow;
            Exception? exception = null;
            TResponse result = default!;
            bool hasResponse = false;

            try
            {
                result = await next();
                hasResponse = true;
                return result;
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;
                var endTime = DateTime.UtcNow;

                await LogPerformanceMetrics(requestTypeName, responseTypeName, requestId, elapsedMs, startTime, endTime, 
                    exception, hasResponse ? result : default);
            }
        }

        /// <summary>
        /// Log performance metrics and warnings with response information
        /// </summary>
        private async Task LogPerformanceMetrics(
            string requestTypeName, 
            string responseTypeName,
            Guid requestId, 
            long elapsedMs, 
            DateTime startTime, 
            DateTime endTime,
            Exception? exception,
            TResponse? response)
        {
            if (!_configuration.EnablePerformanceTracking)
                return;

            var priority = PriorityConventions.GetConventionBasedPriority(typeof(TRequest));
            var status = exception == null ? "Success" : "Failed";
            var statusEmoji = exception == null ? "✅" : "❌";
            var hasResponse = response != null;

            // Always log basic performance info in debug mode
            _logger.LogDebug("{StatusEmoji} {RequestType} -> {ResponseType} [{RequestId}] completed in {ElapsedMs}ms " +
                            "(Status: {Status}, Priority: {Priority}, HasResponse: {HasResponse})",
                statusEmoji, requestTypeName, responseTypeName, requestId, elapsedMs, status, priority, hasResponse);

            // Log warning if the request exceeded the slow threshold
            if (_configuration.EnablePerformanceAlerts && elapsedMs > _configuration.SlowCommandThresholdMs)
            {
                var thresholdMs = _configuration.SlowCommandThresholdMs;
                var overageMs = elapsedMs - thresholdMs;
                
                _logger.LogWarning("🐌 SLOW REQUEST: {RequestType} -> {ResponseType} [{RequestId}] took {ElapsedMs}ms, " +
                                   "which exceeds the {ThresholdMs}ms threshold by {OverageMs}ms. " +
                                   "Priority: {Priority}, Status: {Status}, Started: {StartTime:HH:mm:ss.fff}, Ended: {EndTime:HH:mm:ss.fff}",
                    requestTypeName, responseTypeName, requestId, elapsedMs, thresholdMs, overageMs, priority, status, startTime, endTime);

                // Log additional context for failed slow requests
                if (exception != null)
                {
                    _logger.LogError("💥 Slow request {RequestType} -> {ResponseType} [{RequestId}] also failed with exception: {ExceptionMessage}",
                        requestTypeName, responseTypeName, requestId, exception.Message);
                }
                else if (hasResponse && _configuration.EnableDebugLogging)
                {
                    _logger.LogDebug("📤 Slow request {RequestType} -> {ResponseType} [{RequestId}] returned response of type {ResponseType}",
                        requestTypeName, responseTypeName, requestId, response?.GetType().Name ?? "null");
                }
            }
            else if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("⚡ {RequestType} -> {ResponseType} [{RequestId}] completed in {ElapsedMs}ms (within {ThresholdMs}ms threshold)",
                    requestTypeName, responseTypeName, requestId, elapsedMs, _configuration.SlowCommandThresholdMs);
            }

            // Record metrics for potential collection by monitoring systems
            await RecordPerformanceMetrics(requestTypeName, responseTypeName, priority, elapsedMs, status, startTime, hasResponse);
        }

        /// <summary>
        /// Record performance metrics for monitoring systems with response information
        /// </summary>
        private async Task RecordPerformanceMetrics(
            string requestTypeName,
            string responseTypeName,
            Priority priority, 
            long elapsedMs, 
            string status,
            DateTime startTime,
            bool hasResponse)
        {
            // This is a hook for future metrics integration
            await Task.CompletedTask; // Placeholder for async metrics recording

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogDebug("📊 Metrics recorded: {RequestType} -> {ResponseType} | Priority: {Priority} | " +
                                "Duration: {ElapsedMs}ms | Status: {Status} | HasResponse: {HasResponse} | Time: {StartTime:yyyy-MM-dd HH:mm:ss}",
                    requestTypeName, responseTypeName, priority, elapsedMs, status, hasResponse, startTime);
            }
        }
    }
}