// PriorityFlow.Core.Queuing - Priority Queue Channel Implementation
// Thread-safe priority-based request queuing with System.Threading.Channels

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PriorityFlow.Queuing
{
    /// <summary>
    /// Thread-safe priority queue implementation using System.Threading.Channels
    /// Maintains separate queues for each priority level and merges them during consumption
    /// </summary>
    public class PriorityQueueChannel : IPriorityQueueChannel, IDisposable
    {
        private readonly Channel<PriorityRequest> _highPriorityChannel;
        private readonly Channel<PriorityRequest> _normalPriorityChannel;
        private readonly Channel<PriorityRequest> _lowPriorityChannel;
        
        private readonly ChannelWriter<PriorityRequest> _highPriorityWriter;
        private readonly ChannelWriter<PriorityRequest> _normalPriorityWriter;
        private readonly ChannelWriter<PriorityRequest> _lowPriorityWriter;
        
        private readonly ChannelReader<PriorityRequest> _highPriorityReader;
        private readonly ChannelReader<PriorityRequest> _normalPriorityReader;
        private readonly ChannelReader<PriorityRequest> _lowPriorityReader;
        
        private readonly ConcurrentDictionary<Priority, int> _queueCounts;
        private readonly ILogger<PriorityQueueChannel>? _logger;
        private readonly int? _maxCapacity;
        
        private volatile bool _isDisposed;

        /// <summary>
        /// Creates a new priority queue channel with default unbounded capacity
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics</param>
        public PriorityQueueChannel(ILogger<PriorityQueueChannel>? logger = null)
            : this(null, logger)
        {
        }

        /// <summary>
        /// Creates a new priority queue channel with specified capacity
        /// </summary>
        /// <param name="maxCapacity">Maximum number of requests per priority level (null for unbounded)</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        public PriorityQueueChannel(int? maxCapacity, ILogger<PriorityQueueChannel>? logger = null)
        {
            _logger = logger;
            _maxCapacity = maxCapacity;
            _queueCounts = new ConcurrentDictionary<Priority, int>();
            
            // Initialize counts for all priority levels
            _queueCounts[Priority.High] = 0;
            _queueCounts[Priority.Normal] = 0;
            _queueCounts[Priority.Low] = 0;

            var channelOptions = maxCapacity.HasValue
                ? new BoundedChannelOptions(maxCapacity.Value)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                }
                : new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                } as ChannelOptions;

            // Create separate channels for each priority level
            _highPriorityChannel = Channel.CreateUnbounded<PriorityRequest>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            
            _normalPriorityChannel = Channel.CreateUnbounded<PriorityRequest>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            
            _lowPriorityChannel = Channel.CreateUnbounded<PriorityRequest>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

            // Get writers and readers
            _highPriorityWriter = _highPriorityChannel.Writer;
            _normalPriorityWriter = _normalPriorityChannel.Writer;
            _lowPriorityWriter = _lowPriorityChannel.Writer;
            
            _highPriorityReader = _highPriorityChannel.Reader;
            _normalPriorityReader = _normalPriorityChannel.Reader;
            _lowPriorityReader = _lowPriorityChannel.Reader;

            _logger?.LogInformation("PriorityQueueChannel initialized with capacity: {MaxCapacity}", 
                maxCapacity?.ToString() ?? "unbounded");
        }

        /// <inheritdoc />
        public bool TryAdd(PriorityRequest request)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PriorityQueueChannel));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                var success = request.Priority switch
                {
                    Priority.High => _highPriorityWriter.TryWrite(request),
                    Priority.Normal => _normalPriorityWriter.TryWrite(request),
                    Priority.Low => _lowPriorityWriter.TryWrite(request),
                    _ => _normalPriorityWriter.TryWrite(request) // Default to normal
                };

                if (success)
                {
                    _queueCounts.AddOrUpdate(request.Priority, 1, (_, count) => count + 1);
                    
                    _logger?.LogDebug("Request {RequestId} ({RequestType}) enqueued with priority {Priority}. Queue length: {QueueLength}",
                        request.RequestId, request.RequestTypeName, request.Priority, GetQueueLength());
                }
                else
                {
                    _logger?.LogWarning("Failed to enqueue request {RequestId} ({RequestType}) with priority {Priority}. Queue may be full.",
                        request.RequestId, request.RequestTypeName, request.Priority);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error enqueueing request {RequestId} ({RequestType})", 
                    request.RequestId, request.RequestTypeName);
                return false;
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<PriorityRequest> GetConsumingAsyncEnumerable([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Starting priority queue consumption");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var request = await GetNextRequestAsync(cancellationToken);
                    if (request != null)
                    {
                        // Decrement the count for this priority
                        _queueCounts.AddOrUpdate(request.Priority, 0, (_, count) => Math.Max(0, count - 1));
                        
                        _logger?.LogDebug("Dequeued request {RequestId} ({RequestType}) with priority {Priority}. Wait time: {WaitTime}ms",
                            request.RequestId, request.RequestTypeName, request.Priority, request.WaitTime.TotalMilliseconds);
                        
                        yield return request;
                    }
                    else
                    {
                        // No more requests and all channels are completed
                        break;
                    }
                }
            }
            finally
            {
                _logger?.LogInformation("Priority queue consumption stopped");
            }
        }

        /// <summary>
        /// Gets the next request respecting priority order
        /// High priority items are always processed first, then normal, then low
        /// </summary>
        private async Task<PriorityRequest?> GetNextRequestAsync(CancellationToken cancellationToken)
        {
            // Create tasks for each priority level
            var highTask = _highPriorityReader.WaitToReadAsync(cancellationToken).AsTask();
            var normalTask = _normalPriorityReader.WaitToReadAsync(cancellationToken).AsTask();
            var lowTask = _lowPriorityReader.WaitToReadAsync(cancellationToken).AsTask();

            try
            {
                // Wait for any of the channels to have data available
                var completedTask = await Task.WhenAny(highTask, normalTask, lowTask);

                // Always check high priority first
                if (_highPriorityReader.TryRead(out var highRequest))
                    return highRequest;

                // Then normal priority
                if (_normalPriorityReader.TryRead(out var normalRequest))
                    return normalRequest;

                // Finally low priority
                if (_lowPriorityReader.TryRead(out var lowRequest))
                    return lowRequest;

                // Check if all channels are completed
                if (_highPriorityReader.Completion.IsCompleted && 
                    _normalPriorityReader.Completion.IsCompleted && 
                    _lowPriorityReader.Completion.IsCompleted)
                {
                    return null;
                }

                // Try again - this handles race conditions
                return await GetNextRequestAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public int GetQueueLength()
        {
            return _queueCounts.Values.Sum();
        }

        /// <inheritdoc />
        public Dictionary<Priority, int> GetQueueDistribution()
        {
            return _queueCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <inheritdoc />
        public void CompleteAdding()
        {
            _logger?.LogInformation("Completing priority queue - no more requests will be accepted");
            
            _highPriorityWriter.Complete();
            _normalPriorityWriter.Complete();
            _lowPriorityWriter.Complete();
        }

        /// <inheritdoc />
        public bool IsAddingCompleted => 
            _highPriorityWriter.TryComplete() && 
            _normalPriorityWriter.TryComplete() && 
            _lowPriorityWriter.TryComplete();

        /// <inheritdoc />
        public int? MaxCapacity => _maxCapacity;

        /// <summary>
        /// Dispose resources and complete all channels
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            
            try
            {
                CompleteAdding();
                _logger?.LogInformation("PriorityQueueChannel disposed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing PriorityQueueChannel");
            }
        }
    }
}