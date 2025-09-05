// PriorityFlow.Core.Streaming - Advanced Stream Processing Support
// Real-time data streaming with priority awareness

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PriorityFlow.Streaming
{
    /// <summary>
    /// Priority-aware stream request interface
    /// </summary>
    /// <typeparam name="TResponse">Stream item type</typeparam>
    public interface IPriorityStreamRequest<out TResponse> : IStreamRequest<TResponse>
    {
        /// <summary>
        /// Stream priority level
        /// </summary>
        Priority StreamPriority { get; }
        
        /// <summary>
        /// Maximum items to stream (0 = unlimited)
        /// </summary>
        int MaxItems { get; }
        
        /// <summary>
        /// Batch size for streaming
        /// </summary>
        int BatchSize { get; }
    }

    /// <summary>
    /// Enhanced stream handler with priority and batching support
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IPriorityStreamRequestHandler<in TRequest, TResponse> 
        where TRequest : IPriorityStreamRequest<TResponse>
    {
        /// <summary>
        /// Handle stream request with priority awareness
        /// </summary>
        IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Stream processing pipeline behavior
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IStreamPipelineBehavior<TRequest, TResponse> where TRequest : IStreamRequest<TResponse>
    {
        /// <summary>
        /// Handle stream pipeline behavior
        /// </summary>
        IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Stream handler delegate
    /// </summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>();

    /// <summary>
    /// Built-in stream processing behaviors
    /// </summary>
    public static class StreamBehaviors
    {
        /// <summary>
        /// Throttling stream behavior - limits stream rate based on priority
        /// </summary>
        public class ThrottlingStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
            where TRequest : IStreamRequest<TResponse>
        {
            private readonly ILogger<ThrottlingStreamBehavior<TRequest, TResponse>> _logger;

            public ThrottlingStreamBehavior(ILogger<ThrottlingStreamBehavior<TRequest, TResponse>> logger)
            {
                _logger = logger;
            }

            public async IAsyncEnumerable<TResponse> Handle(
                TRequest request, 
                StreamHandlerDelegate<TResponse> next, 
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var requestName = typeof(TRequest).Name;
                var priority = GetStreamPriority(request);
                var delay = GetThrottleDelay(priority);

                _logger.LogDebug("🎛️ STREAM THROTTLE: {RequestName} with {Priority} priority, delay: {DelayMs}ms", 
                    requestName, priority, delay.TotalMilliseconds);

                await foreach (var item in next().WithCancellation(cancellationToken))
                {
                    yield return item;
                    
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                }
            }

            private Priority GetStreamPriority(TRequest request)
            {
                return request is IPriorityStreamRequest<TResponse> priorityRequest 
                    ? priorityRequest.StreamPriority 
                    : PriorityConventions.GetConventionBasedPriority(typeof(TRequest));
            }

            private TimeSpan GetThrottleDelay(Priority priority)
            {
                return priority switch
                {
                    Priority.High => TimeSpan.FromMilliseconds(10),    // Fast streaming for critical data
                    Priority.Normal => TimeSpan.FromMilliseconds(50),  // Standard rate
                    Priority.Low => TimeSpan.FromMilliseconds(100),    // Slower for background streams
                    _ => TimeSpan.FromMilliseconds(50)
                };
            }
        }

        /// <summary>
        /// Buffering stream behavior - batches stream items for efficiency
        /// </summary>
        public class BufferingStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
            where TRequest : IStreamRequest<TResponse>
        {
            private readonly ILogger<BufferingStreamBehavior<TRequest, TResponse>> _logger;

            public BufferingStreamBehavior(ILogger<BufferingStreamBehavior<TRequest, TResponse>> logger)
            {
                _logger = logger;
            }

            public async IAsyncEnumerable<TResponse> Handle(
                TRequest request, 
                StreamHandlerDelegate<TResponse> next, 
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var batchSize = GetBatchSize(request);
                var requestName = typeof(TRequest).Name;

                _logger.LogDebug("📦 STREAM BUFFER: {RequestName} with batch size {BatchSize}", requestName, batchSize);

                var buffer = new List<TResponse>(batchSize);
                
                await foreach (var item in next().WithCancellation(cancellationToken))
                {
                    buffer.Add(item);
                    
                    if (buffer.Count >= batchSize)
                    {
                        foreach (var bufferedItem in buffer)
                        {
                            yield return bufferedItem;
                        }
                        buffer.Clear();
                    }
                }

                // Yield remaining items in buffer
                foreach (var remainingItem in buffer)
                {
                    yield return remainingItem;
                }
            }

            private int GetBatchSize(TRequest request)
            {
                return request is IPriorityStreamRequest<TResponse> priorityRequest 
                    ? priorityRequest.BatchSize 
                    : 10; // Default batch size
            }
        }

        /// <summary>
        /// Filtering stream behavior - filters stream items based on criteria
        /// </summary>
        public class FilteringStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
            where TRequest : IStreamRequest<TResponse>
        {
            private readonly ILogger<FilteringStreamBehavior<TRequest, TResponse>> _logger;
            private readonly Func<TResponse, bool>? _filter;

            public FilteringStreamBehavior(
                ILogger<FilteringStreamBehavior<TRequest, TResponse>> logger,
                Func<TResponse, bool>? filter = null)
            {
                _logger = logger;
                _filter = filter;
            }

            public async IAsyncEnumerable<TResponse> Handle(
                TRequest request, 
                StreamHandlerDelegate<TResponse> next, 
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var requestName = typeof(TRequest).Name;
                var itemCount = 0;
                var filteredCount = 0;

                _logger.LogDebug("🔍 STREAM FILTER: {RequestName} filtering enabled", requestName);

                await foreach (var item in next().WithCancellation(cancellationToken))
                {
                    itemCount++;
                    
                    if (_filter?.Invoke(item) != false)
                    {
                        filteredCount++;
                        yield return item;
                    }
                }

                _logger.LogDebug("✅ STREAM FILTER: {RequestName} processed {ItemCount} items, yielded {FilteredCount}",
                    requestName, itemCount, filteredCount);
            }
        }
    }

    /// <summary>
    /// Sample priority stream requests for common scenarios
    /// </summary>
    public static class SampleStreamRequests
    {
        /// <summary>
        /// Real-time metrics streaming request
        /// </summary>
        public record MetricsStreamRequest(string MetricName, TimeSpan Interval) : IPriorityStreamRequest<MetricValue>
        {
            public Priority StreamPriority => Priority.High; // Real-time metrics are high priority
            public int MaxItems => 0; // Unlimited
            public int BatchSize => 1; // Real-time, no batching
        }

        /// <summary>
        /// Log streaming request
        /// </summary>
        public record LogStreamRequest(string LogLevel, DateTime StartTime) : IPriorityStreamRequest<LogEntry>
        {
            public Priority StreamPriority => Priority.Normal;
            public int MaxItems => 1000; // Limit to prevent memory issues
            public int BatchSize => 50; // Batch for efficiency
        }

        /// <summary>
        /// Analytics data streaming request
        /// </summary>
        public record AnalyticsStreamRequest(string DataSet, string[] Filters) : IPriorityStreamRequest<AnalyticsData>
        {
            public Priority StreamPriority => Priority.Low; // Background analytics
            public int MaxItems => 0; // Unlimited
            public int BatchSize => 100; // Large batches for efficiency
        }

        // Sample data types
        public record MetricValue(string Name, double Value, DateTime Timestamp);
        public record LogEntry(string Level, string Message, DateTime Timestamp);
        public record AnalyticsData(string Key, object Value, DateTime Timestamp);
    }
}