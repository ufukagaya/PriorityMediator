// PriorityFlow.Core - Queued Priority Mediator with Async Background Processing
// Enqueues requests for priority-based processing instead of immediate execution

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PriorityFlow.Queuing;

namespace PriorityFlow
{
    /// <summary>
    /// Priority-aware mediator that enqueues requests for asynchronous background processing
    /// Maintains API compatibility with MediatR while providing true priority-based execution
    /// </summary>
    public class QueuedPriorityMediator : IMediator
    {
        private readonly IPriorityQueueChannel _queueChannel;
        private readonly PriorityFlowConfiguration _configuration;
        private readonly ILogger<QueuedPriorityMediator> _logger;

        public QueuedPriorityMediator(
            IPriorityQueueChannel queueChannel,
            PriorityFlowConfiguration configuration,
            ILogger<QueuedPriorityMediator> logger)
        {
            _queueChannel = queueChannel ?? throw new ArgumentNullException(nameof(queueChannel));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region ISender Implementation with Queuing

        /// <summary>
        /// Send a request without response - enqueues for priority-based processing
        /// </summary>
        public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) 
            where TRequest : IRequest
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var priority = PriorityConventions.GetConventionBasedPriority(typeof(TRequest));
            var requestTypeName = typeof(TRequest).Name;

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("📨 Enqueueing {RequestType} with Priority.{Priority}", requestTypeName, priority);
            }

            var priorityRequest = PriorityRequest.Create(request, priority, cancellationToken);

            if (!_queueChannel.TryAdd(priorityRequest))
            {
                throw new InvalidOperationException($"Failed to enqueue {requestTypeName}. Queue may be full or closed.");
            }

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("⏳ Awaiting completion of {RequestType} [{RequestId}]", 
                    requestTypeName, priorityRequest.RequestId);
            }

            // Wait for the background worker to process the request
            try
            {
                await priorityRequest.GetTask();

                if (_configuration.EnableDebugLogging)
                {
                    _logger.LogInformation("🎯 {RequestType} [{RequestId}] completed successfully", 
                        requestTypeName, priorityRequest.RequestId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 {RequestType} [{RequestId}] failed: {ErrorMessage}", 
                    requestTypeName, priorityRequest.RequestId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Send a request with response - enqueues for priority-based processing and returns result
        /// </summary>
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var requestType = request.GetType();
            var priority = PriorityConventions.GetConventionBasedPriority(requestType);
            var requestTypeName = requestType.Name;

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("📨 Enqueueing {RequestType} with Priority.{Priority} (expects response)", 
                    requestTypeName, priority);
            }

            var priorityRequest = PriorityRequest.Create(request, priority, cancellationToken);

            if (!_queueChannel.TryAdd(priorityRequest))
            {
                throw new InvalidOperationException($"Failed to enqueue {requestTypeName}. Queue may be full or closed.");
            }

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("⏳ Awaiting response from {RequestType} [{RequestId}]", 
                    requestTypeName, priorityRequest.RequestId);
            }

            try
            {
                var result = await priorityRequest.GetResult<TResponse>();

                if (_configuration.EnableDebugLogging)
                {
                    _logger.LogInformation("🎯 {RequestType} [{RequestId}] completed with response", 
                        requestTypeName, priorityRequest.RequestId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 {RequestType} [{RequestId}] failed: {ErrorMessage}", 
                    requestTypeName, priorityRequest.RequestId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Send a request (object-based) - for advanced scenarios with reflection
        /// </summary>
        public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var requestType = request.GetType();
            var priority = PriorityConventions.GetConventionBasedPriority(requestType);
            var requestTypeName = requestType.Name;

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("📨 Enqueueing {RequestType} with Priority.{Priority} (object-based)", 
                    requestTypeName, priority);
            }

            var priorityRequest = PriorityRequest.Create(request, priority, cancellationToken);

            if (!_queueChannel.TryAdd(priorityRequest))
            {
                throw new InvalidOperationException($"Failed to enqueue {requestTypeName}. Queue may be full or closed.");
            }

            try
            {
                var result = await priorityRequest.GetTask();

                if (_configuration.EnableDebugLogging)
                {
                    _logger.LogInformation("🎯 {RequestType} [{RequestId}] completed (object-based)", 
                        requestTypeName, priorityRequest.RequestId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 {RequestType} [{RequestId}] failed: {ErrorMessage}", 
                    requestTypeName, priorityRequest.RequestId, ex.Message);
                throw;
            }
        }

        #endregion

        #region IPublisher Implementation - Direct Processing (No Queuing)

        /// <summary>
        /// Publish notification - processed directly (notifications are typically fire-and-forget)
        /// </summary>
        public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) 
            where TNotification : INotification
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            // For notifications, we don't queue them since they're typically fire-and-forget
            // This maintains consistency with MediatR's behavior for notifications
            
            var handlers = GetNotificationHandlers<TNotification>();
            var notificationTypeName = typeof(TNotification).Name;

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("📢 Publishing {NotificationType} directly to {HandlerCount} handlers (not queued)", 
                    notificationTypeName, handlers.Count());
            }

            var tasks = handlers.Select(handler => handler.Handle(notification, cancellationToken));
            await Task.WhenAll(tasks);

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("✅ {NotificationType} published to all handlers", notificationTypeName);
            }
        }

        /// <summary>
        /// Publish notification (object-based) - processed directly
        /// </summary>
        public async Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            var notificationType = notification.GetType();
            var handlers = GetNotificationHandlers(notificationType);
            var notificationTypeName = notificationType.Name;

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("📢 Publishing {NotificationType} directly to {HandlerCount} handlers (object-based, not queued)", 
                    notificationTypeName, handlers.Count());
            }

            var handleMethod = typeof(INotificationHandler<>).MakeGenericType(notificationType).GetMethod("Handle");
            var tasks = handlers.Select(handler => (Task)handleMethod!.Invoke(handler, new[] { notification, cancellationToken })!);
            await Task.WhenAll(tasks);

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("✅ {NotificationType} published to all handlers (object-based)", notificationTypeName);
            }
        }

        #endregion

        #region Stream Support - Not Queued (Real-time Streams)

        /// <summary>
        /// Create stream - processed directly (streams need real-time processing)
        /// </summary>
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // Streams are processed directly - queuing would break the streaming nature
            var handler = GetStreamHandler<IStreamRequest<TResponse>, TResponse>();
            return handler.Handle(request, cancellationToken);
        }

        /// <summary>
        /// Create stream (object-based) - simplified implementation
        /// </summary>
        public async IAsyncEnumerable<object?> CreateStream(object request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // Simplified implementation - would need reflection for full support
            yield return await Task.FromResult<object?>(request);
            await Task.Delay(1, cancellationToken);
        }

        #endregion

        #region Helper Methods - Service Resolution

        /// <summary>
        /// Get notification handlers for strongly-typed notifications (using fake service resolution for demo)
        /// In real implementation, this would resolve from IServiceProvider
        /// </summary>
        private IEnumerable<INotificationHandler<TNotification>> GetNotificationHandlers<TNotification>() 
            where TNotification : INotification
        {
            // This is a simplified implementation - in reality, we'd resolve from IServiceProvider
            // But since notifications are processed directly (not queued), we don't have access to the scoped provider here
            // The PriorityRequestWorker handles the actual service resolution for queued requests
            
            _logger.LogWarning("GetNotificationHandlers<{NotificationType}> - Service resolution not implemented in QueuedPriorityMediator", 
                typeof(TNotification).Name);
            
            return Enumerable.Empty<INotificationHandler<TNotification>>();
        }

        /// <summary>
        /// Get notification handlers for object-based notifications (using fake service resolution for demo)
        /// </summary>
        private IEnumerable<object> GetNotificationHandlers(Type notificationType)
        {
            _logger.LogWarning("GetNotificationHandlers({NotificationType}) - Service resolution not implemented in QueuedPriorityMediator", 
                notificationType.Name);
            
            return Enumerable.Empty<object>();
        }

        /// <summary>
        /// Get stream handler (using fake service resolution for demo)
        /// </summary>
        private IStreamRequestHandler<TRequest, TResponse> GetStreamHandler<TRequest, TResponse>() 
            where TRequest : IStreamRequest<TResponse>
        {
            _logger.LogWarning("GetStreamHandler<{RequestType}, {ResponseType}> - Service resolution not implemented in QueuedPriorityMediator", 
                typeof(TRequest).Name, typeof(TResponse).Name);
            
            throw new NotImplementedException("Stream handler resolution not implemented in QueuedPriorityMediator");
        }

        #endregion

        #region Queue Inspection Methods (for Monitoring)

        /// <summary>
        /// Get current queue length (for monitoring and diagnostics)
        /// </summary>
        public int GetQueueLength()
        {
            return _queueChannel.GetQueueLength();
        }

        /// <summary>
        /// Get queue distribution by priority (for monitoring and diagnostics)
        /// </summary>
        public Dictionary<Priority, int> GetQueueDistribution()
        {
            return _queueChannel.GetQueueDistribution();
        }

        /// <summary>
        /// Check if queue is accepting new requests
        /// </summary>
        public bool IsQueueAcceptingRequests()
        {
            return !_queueChannel.IsAddingCompleted;
        }

        #endregion
    }
}