// Extensions/PriorityFlow/PriorityMediator.cs  
// Simplified Developer-Friendly Priority MediatR Wrapper

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JetRentalOrchestration.Extensions.PriorityFlow
{
    /// <summary>
    /// Simple priority-aware MediatR wrapper
    /// - MediatR compatible (drop-in replacement)
    /// - In-process priority queue (no background threads)
    /// - Easy debugging and testing
    /// - Clear error messages
    /// </summary>
    public class PriorityMediator : IMediator
    {
        private readonly IMediator _innerMediator;
        private readonly ILogger<PriorityMediator> _logger;
        private readonly List<PriorityCommandItem> _commandQueue = new();
        private readonly SemaphoreSlim _queueSemaphore = new(1, 1);

        public PriorityMediator(IServiceProvider serviceProvider, ILogger<PriorityMediator> logger)
        {
            // Get the original MediatR instance (registered as Mediator concrete type)
            _innerMediator = serviceProvider.GetRequiredService<Mediator>();
            _logger = logger;
        }

        /// <summary>
        /// Send with priority support - main entry point
        /// </summary>
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var priority = GetCommandPriority(request);
            var commandType = request.GetType().Name;

            #if DEBUG
            _logger.LogInformation("🎯 Executing {CommandType} with Priority.{Priority}", commandType, priority);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            #endif

            try
            {
                TResponse result;

                // If no other commands are queued, execute directly
                if (!HasQueuedCommands())
                {
                    result = await _innerMediator.Send(request, cancellationToken);
                }
                else
                {
                    // Add to priority queue and process in order
                    result = await ExecuteWithPriorityQueue(request, priority, cancellationToken);
                }

                #if DEBUG
                stopwatch.Stop();
                _logger.LogInformation("✅ {CommandType} completed in {ElapsedMs}ms", commandType, stopwatch.ElapsedMilliseconds);
                #endif

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ {CommandType} failed: {ErrorMessage}", commandType, ex.Message);
                
                // Provide helpful error messages for common issues
                if (ex.Message.Contains("No service for type"))
                {
                    throw new PriorityMediatRException(
                        $"Handler not found for {commandType}. " +
                        $"Make sure you registered the handler: " +
                        $"services.AddScoped<IRequestHandler<{commandType}, {typeof(TResponse).Name}>, YourHandler>();", 
                        ex);
                }
                
                throw;
            }
        }

        /// <summary>
        /// Non-generic Send method for MediatR compatibility
        /// </summary>
        public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            // For non-generic calls, delegate to the inner mediator
            // This keeps the implementation simple and avoids complex reflection
            return await _innerMediator.Send(request, cancellationToken);
        }

        /// <summary>
        /// Void command Send method
        /// </summary>
        public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            var priority = GetCommandPriority(request);
            var commandType = request.GetType().Name;

            #if DEBUG
            _logger.LogInformation("🎯 Executing void command {CommandType} with Priority.{Priority}", commandType, priority);
            #endif

            try
            {
                // For void commands, always execute directly (simpler)
                await _innerMediator.Send(request, cancellationToken);
                
                #if DEBUG
                _logger.LogInformation("✅ Void command {CommandType} completed", commandType);
                #endif
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Void command {CommandType} failed", commandType);
                throw;
            }
        }

        /// <summary>
        /// Publish method - direct passthrough to MediatR
        /// Events don't need priority handling
        /// </summary>
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            return _innerMediator.Publish(notification, cancellationToken);
        }

        /// <summary>
        /// Generic Publish method
        /// </summary>
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return _innerMediator.Publish(notification, cancellationToken);
        }

        /// <summary>
        /// Streaming support - delegate to inner mediator
        /// </summary>
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return _innerMediator.CreateStream(request, cancellationToken);
        }

        /// <summary>
        /// Non-generic streaming support
        /// </summary>
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            return _innerMediator.CreateStream(request, cancellationToken);
        }

        // ===================================================================
        // PRIVATE HELPER METHODS
        // ===================================================================

        /// <summary>
        /// Execute command with priority queue (single-threaded, predictable)
        /// </summary>
        private async Task<TResponse> ExecuteWithPriorityQueue<TResponse>(IRequest<TResponse> request, Priority priority, CancellationToken cancellationToken)
        {
            await _queueSemaphore.WaitAsync(cancellationToken);

            try
            {
                // Add current command to queue
                var queueItem = new PriorityCommandItem
                {
                    Command = request,
                    Priority = priority,
                    QueuedAt = DateTime.UtcNow
                };

                _commandQueue.Add(queueItem);

                // Sort by priority (High -> Normal -> Low) then by QueuedAt
                _commandQueue.Sort((x, y) =>
                {
                    var priorityComparison = y.Priority.CompareTo(x.Priority); // Descending priority
                    return priorityComparison != 0 ? priorityComparison : x.QueuedAt.CompareTo(y.QueuedAt); // Ascending time
                });

                // Process all commands in priority order
                TResponse result = default!;
                var processedItems = new List<PriorityCommandItem>();

                foreach (var item in _commandQueue)
                {
                    if (ReferenceEquals(item.Command, request))
                    {
                        // This is our command
                        result = await _innerMediator.Send((IRequest<TResponse>)item.Command, cancellationToken);
                    }
                    else
                    {
                        // Other command - execute it too (fire and forget for void commands)
                        try
                        {
                            await _innerMediator.Send(item.Command, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Queued command {CommandType} failed", item.CommandType);
                            // Don't let other command failures affect our result
                        }
                    }

                    processedItems.Add(item);
                }

                // Remove processed items
                foreach (var item in processedItems)
                {
                    _commandQueue.Remove(item);
                }

                return result;
            }
            finally
            {
                _queueSemaphore.Release();
            }
        }

        /// <summary>
        /// Check if there are queued commands waiting
        /// </summary>
        private bool HasQueuedCommands()
        {
            return _commandQueue.Count > 0;
        }

        /// <summary>
        /// Get priority for a command using attribute and conventions
        /// </summary>
        private Priority GetCommandPriority(object command)
        {
            // 1. Check for explicit Priority attribute
            var attribute = command.GetType().GetCustomAttribute<PriorityAttribute>();
            if (attribute != null)
            {
                return attribute.Priority;
            }

            // 2. Check naming conventions (safe patterns only)
            var conventionPriority = PriorityConventions.GetConventionBasedPriority(command.GetType());
            if (conventionPriority != Priority.Normal)
            {
                return conventionPriority;
            }

            // 3. Default to Normal
            return Priority.Normal;
        }
    }

    /// <summary>
    /// Custom exception for PriorityMediatR with helpful messages
    /// </summary>
    public class PriorityMediatRException : Exception
    {
        public PriorityMediatRException(string message) : base(message) { }
        public PriorityMediatRException(string message, Exception innerException) : base(message, innerException) { }
    }
}