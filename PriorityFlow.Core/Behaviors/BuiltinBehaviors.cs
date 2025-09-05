// PriorityFlow.Core.Behaviors - Built-in Pipeline Behaviors
// Production-ready cross-cutting concerns

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PriorityFlow.Behaviors;

namespace PriorityFlow.BuiltinBehaviors
{
    /// <summary>
    /// Logging pipeline behavior - logs all command executions
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public class LoggingBehavior<TRequest, TResponse> : IGenericPipelineBehavior<TRequest, TResponse>
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var requestId = Guid.NewGuid();
            
            _logger.LogInformation("🔄 [{RequestId}] Handling {RequestName}", requestId, requestName);
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var response = await next();
                
                stopwatch.Stop();
                _logger.LogInformation("✅ [{RequestId}] {RequestName} completed in {ElapsedMs}ms", 
                    requestId, requestName, stopwatch.ElapsedMilliseconds);
                
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "❌ [{RequestId}] {RequestName} failed after {ElapsedMs}ms: {ErrorMessage}", 
                    requestId, requestName, stopwatch.ElapsedMilliseconds, ex.Message);
                throw;
            }
        }
    }

    /// <summary>
    /// Performance monitoring behavior - tracks execution times and alerts on slow commands
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public class PerformanceMonitoringBehavior<TRequest, TResponse> : IGenericPipelineBehavior<TRequest, TResponse>
    {
        private readonly ILogger<PerformanceMonitoringBehavior<TRequest, TResponse>> _logger;
        private readonly PriorityFlowConfiguration _configuration;

        public PerformanceMonitoringBehavior(
            ILogger<PerformanceMonitoringBehavior<TRequest, TResponse>> logger,
            PriorityFlowConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var stopwatch = Stopwatch.StartNew();
            
            var response = await next();
            
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            
            // Alert on slow commands
            if (_configuration.EnablePerformanceAlerts && elapsedMs > _configuration.SlowCommandThresholdMs)
            {
                _logger.LogWarning("⚠️ SLOW COMMAND: {RequestName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                    requestName, elapsedMs, _configuration.SlowCommandThresholdMs);
            }
            
            // Track metrics (in real implementation, this would go to metrics collector)
            if (_configuration.EnablePerformanceTracking)
            {
                _logger.LogDebug("📊 METRICS: {RequestName} executed in {ElapsedMs}ms", requestName, elapsedMs);
            }
            
            return response;
        }
    }

    /// <summary>
    /// Validation behavior - validates requests before processing
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public class ValidationBehavior<TRequest, TResponse> : IGenericPipelineBehavior<TRequest, TResponse>
        where TRequest : class
    {
        private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

        public ValidationBehavior(ILogger<ValidationBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            
            // Basic null validation
            if (request == null)
            {
                _logger.LogError("❌ VALIDATION: {RequestName} is null", requestName);
                throw new ArgumentNullException(nameof(request), $"{requestName} cannot be null");
            }

            // Custom validation logic can be added here
            // For example: FluentValidation integration
            
            _logger.LogDebug("✅ VALIDATION: {RequestName} passed validation", requestName);
            
            return await next();
        }
    }

    /// <summary>
    /// Retry behavior - retries failed commands based on configuration
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public class RetryBehavior<TRequest, TResponse> : IGenericPipelineBehavior<TRequest, TResponse>
    {
        private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;
        private const int MaxRetryAttempts = 3;
        private static readonly TimeSpan DelayBetweenRetries = TimeSpan.FromMilliseconds(500);

        public RetryBehavior(ILogger<RetryBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            
            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    var response = await next();
                    
                    if (attempt > 1)
                    {
                        _logger.LogInformation("✅ RETRY SUCCESS: {RequestName} succeeded on attempt {Attempt}", requestName, attempt);
                    }
                    
                    return response;
                }
                catch (Exception ex) when (attempt < MaxRetryAttempts && IsRetryableException(ex))
                {
                    _logger.LogWarning("⚠️ RETRY: {RequestName} failed on attempt {Attempt}, retrying in {DelayMs}ms. Error: {ErrorMessage}",
                        requestName, attempt, DelayBetweenRetries.TotalMilliseconds, ex.Message);
                    
                    await Task.Delay(DelayBetweenRetries, cancellationToken);
                }
                catch (Exception ex)
                {
                    if (attempt < MaxRetryAttempts)
                    {
                        _logger.LogError("❌ RETRY FAILED: {RequestName} failed on attempt {Attempt} with non-retryable error: {ErrorMessage}",
                            requestName, attempt, ex.Message);
                    }
                    throw;
                }
            }
            
            throw new InvalidOperationException("This should never be reached");
        }

        private static bool IsRetryableException(Exception exception)
        {
            // Define which exceptions are retryable
            return exception is not ArgumentException 
                && exception is not ArgumentNullException
                && exception is not InvalidOperationException;
        }
    }

    /// <summary>
    /// Priority-aware caching behavior - caches responses based on priority and configuration
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public class CachingBehavior<TRequest, TResponse> : IGenericPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;
        // In real implementation, this would be IMemoryCache or IDistributedCache
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (TResponse Response, DateTime Expiry)> _cache = new();

        public CachingBehavior(ILogger<CachingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var priority = PriorityConventions.GetConventionBasedPriority(typeof(TRequest));
            
            // Only cache Low priority requests (reports, analytics, etc.)
            if (priority != Priority.Low)
            {
                _logger.LogDebug("🚫 CACHE SKIP: {RequestName} has {Priority} priority, not caching", requestName, priority);
                return await next();
            }

            var cacheKey = GenerateCacheKey(request);
            
            // Try to get from cache
            if (_cache.TryGetValue(cacheKey, out var cachedItem) && cachedItem.Expiry > DateTime.UtcNow)
            {
                _logger.LogDebug("🎯 CACHE HIT: {RequestName} served from cache", requestName);
                return cachedItem.Response;
            }

            // Execute and cache result
            var response = await next();
            
            var expiry = DateTime.UtcNow.AddMinutes(GetCacheExpiryMinutes(priority));
            _cache.TryAdd(cacheKey, (response, expiry));
            
            _logger.LogDebug("💾 CACHE SET: {RequestName} cached until {Expiry}", requestName, expiry);
            
            return response;
        }

        private string GenerateCacheKey(TRequest request)
        {
            // Simple cache key generation - in real implementation, use more sophisticated approach
            return $"{typeof(TRequest).Name}:{request.GetHashCode()}";
        }

        private int GetCacheExpiryMinutes(Priority priority)
        {
            return priority switch
            {
                Priority.Low => 30,    // Cache reports for 30 minutes
                Priority.Normal => 5,  // Cache normal operations for 5 minutes
                Priority.High => 1,    // Cache high priority for 1 minute (if ever cached)
                _ => 5
            };
        }
    }
}