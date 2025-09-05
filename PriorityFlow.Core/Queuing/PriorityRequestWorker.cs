// PriorityFlow.Core.Queuing - Background Worker for Priority Request Processing
// Processes requests from the priority queue in background using BackgroundService

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PriorityFlow.Queuing
{
    /// <summary>
    /// Background service that processes priority requests from the queue
    /// Creates new DI scopes for each request and handles both void and response requests
    /// </summary>
    public class PriorityRequestWorker : BackgroundService
    {
        private readonly IPriorityQueueChannel _queueChannel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PriorityRequestWorker> _logger;
        private readonly PriorityFlowConfiguration _configuration;

        public PriorityRequestWorker(
            IPriorityQueueChannel queueChannel,
            IServiceProvider serviceProvider,
            ILogger<PriorityRequestWorker> logger,
            PriorityFlowConfiguration configuration)
        {
            _queueChannel = queueChannel ?? throw new ArgumentNullException(nameof(queueChannel));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Main execution loop - consumes requests from priority queue and processes them
        /// </summary>
        /// <param name="stoppingToken">Cancellation token for graceful shutdown</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PriorityRequestWorker started - processing requests in priority order");

            try
            {
                await foreach (var priorityRequest in _queueChannel.GetConsumingAsyncEnumerable(stoppingToken))
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Shutdown requested - completing current request and stopping");
                        break;
                    }

                    // Check if the request was already cancelled
                    if (priorityRequest.CancellationToken.IsCancellationRequested)
                    {
                        priorityRequest.SetCanceled();
                        continue;
                    }

                    await ProcessPriorityRequestAsync(priorityRequest, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("PriorityRequestWorker cancelled during shutdown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in PriorityRequestWorker");
                throw;
            }
            finally
            {
                _logger.LogInformation("PriorityRequestWorker stopped");
            }
        }

        /// <summary>
        /// Processes a single priority request within a new dependency injection scope
        /// </summary>
        /// <param name="priorityRequest">The request to process</param>
        /// <param name="workerStoppingToken">Worker cancellation token</param>
        private async Task ProcessPriorityRequestAsync(PriorityRequest priorityRequest, CancellationToken workerStoppingToken)
        {
            var requestId = priorityRequest.RequestId;
            var requestType = priorityRequest.Request.GetType();
            var requestTypeName = priorityRequest.RequestTypeName;
            var startTime = DateTime.UtcNow;

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("🏗️ Processing {RequestType} [{RequestId}] with Priority.{Priority} (queued: {WaitTime}ms ago)",
                    requestTypeName, requestId, priorityRequest.Priority, priorityRequest.WaitTime.TotalMilliseconds);
            }

            try
            {
                // Create a new scope for dependency injection
                using var scope = _serviceProvider.CreateScope();
                var scopedServiceProvider = scope.ServiceProvider;

                // Get a scoped mediator instance for processing
                var mediator = scopedServiceProvider.GetRequiredService<IMediator>();

                // Combine cancellation tokens - respect both request and worker cancellation
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    priorityRequest.CancellationToken, workerStoppingToken);

                object? result = null;

                // Determine if this is a request with response or without response
                var requestInterface = requestType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

                if (requestInterface != null)
                {
                    // Request with response - call Send<TResponse>
                    var responseType = requestInterface.GetGenericArguments()[0];
                    var sendMethod = typeof(IMediator).GetMethods()
                        .Where(m => m.Name == nameof(IMediator.Send) && m.IsGenericMethodDefinition)
                        .FirstOrDefault(m => m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.IsGenericType);

                    if (sendMethod != null)
                    {
                        var genericMethod = sendMethod.MakeGenericMethod(responseType);
                        var task = (Task)genericMethod.Invoke(mediator, new[] { priorityRequest.Request, combinedCts.Token })!;
                        await task;
                        result = task.GetType().GetProperty("Result")?.GetValue(task);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Could not find appropriate Send method for {requestType.Name}");
                    }
                }
                else
                {
                    // Request without response - call Send(IRequest)
                    if (priorityRequest.Request is IRequest request)
                    {
                        await mediator.Send(request, combinedCts.Token);
                        result = null; // No response expected
                    }
                    else
                    {
                        throw new InvalidOperationException($"Request {requestType.Name} does not implement IRequest or IRequest<T>");
                    }
                }

                // Set successful result
                priorityRequest.SetResult(result);

                if (_configuration.EnableDebugLogging)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    _logger.LogInformation("✅ {RequestType} [{RequestId}] completed successfully in {ElapsedMs}ms",
                        requestTypeName, requestId, elapsed.TotalMilliseconds);
                }
            }
            catch (OperationCanceledException) when (priorityRequest.CancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("⏹️ {RequestType} [{RequestId}] was cancelled by caller",
                    requestTypeName, requestId);
                priorityRequest.SetCanceled();
            }
            catch (OperationCanceledException) when (workerStoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("⏹️ {RequestType} [{RequestId}] was cancelled due to worker shutdown",
                    requestTypeName, requestId);
                priorityRequest.SetCanceled();
            }
            catch (Exception ex)
            {
                var elapsed = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "❌ {RequestType} [{RequestId}] failed after {ElapsedMs}ms: {ErrorMessage}",
                    requestTypeName, requestId, elapsed.TotalMilliseconds, ex.Message);
                
                // Enrich the exception with context
                var enrichedException = new InvalidOperationException(
                    $"Request {requestTypeName} [{requestId}] failed during background processing: {ex.Message}", ex);
                
                priorityRequest.SetException(enrichedException);
            }
        }

        /// <summary>
        /// Called when the service is stopping - allows graceful shutdown
        /// </summary>
        /// <param name="cancellationToken">Shutdown cancellation token</param>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PriorityRequestWorker is stopping...");

            // Stop accepting new requests
            _queueChannel.CompleteAdding();

            // Call the base implementation to stop the background execution
            await base.StopAsync(cancellationToken);

            _logger.LogInformation("PriorityRequestWorker has stopped gracefully");
        }
    }
}