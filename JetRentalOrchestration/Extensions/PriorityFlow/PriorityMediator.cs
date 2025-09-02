// Extensions/PriorityFlow/PriorityMediator.cs  
// Bu dosyayı Visual Studio'da Extensions/PriorityFlow klasörüne oluştur

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
    /// Priority-aware MediatR wrapper
    /// IMediator interface'ini implement eder ama priority + orchestration ekler
    /// Existing kod değişmeden çalışır, ama enhanced özellikler sağlar
    /// </summary>
    public class PriorityMediator : IMediator, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PriorityMediator> _logger;
        private readonly Lazy<WorkflowOrchestrator> _orchestrator;
        private readonly Lazy<IMediator> _innerMediator;
        
        // Thread-local flag to detect recursive calls
        private static readonly ThreadLocal<bool> _isProcessingInOrchestrator = new ThreadLocal<bool>(() => false);
        
        /// <summary>
        /// Set processing flag - used by WorkflowOrchestrator to indicate we're in execution context
        /// </summary>
        internal static IDisposable SetProcessingContext()
        {
            _isProcessingInOrchestrator.Value = true;
            return new ProcessingContext();
        }
        
        private class ProcessingContext : IDisposable
        {
            public void Dispose()
            {
                _isProcessingInOrchestrator.Value = false;
            }
        }

        public PriorityMediator(IServiceProvider serviceProvider, ILogger<PriorityMediator> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            // Lazy initialization - sadece gerektiğinde oluştur
            _innerMediator = new Lazy<IMediator>(() => CreateInnerMediator());
            _orchestrator = new Lazy<WorkflowOrchestrator>(() => CreateOrchestrator());

            _logger.LogInformation("🚀 PriorityMediator initialized - ready for enhanced MediatR processing");
        }

        /// <summary>
        /// Ana Send method - priority + orchestration ile enhanced
        /// CRITICAL: Recursive calls'ı detect eder ve inner mediator'a yönlendirir
        /// </summary>
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            // DEADLOCK PREVENTION: If we're already processing in orchestrator, use inner mediator
            if (_isProcessingInOrchestrator.Value)
            {
                _logger.LogDebug("🔄 Recursive call detected - using inner mediator for {CommandType}", request.GetType().Name);
                return await _innerMediator.Value.Send(request, cancellationToken);
            }

            // Priority attribute kontrolü
            var priority = GetCommandPriority(request);
            _logger.LogInformation("📨 Sending {CommandType} with priority {Priority}",
                request.GetType().Name, priority);

            try
            {
                // Orchestrator ile priority-aware execution
                var result = await _orchestrator.Value.ExecuteWithOrchestrationAsync(request, cancellationToken);

                _logger.LogInformation("✅ {CommandType} completed successfully", request.GetType().Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ {CommandType} failed", request.GetType().Name);
                throw;
            }
        }

        /// <summary>
        /// Non-generic Send method - MediatR compatibility
        /// </summary>
        public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            if (request is IRequest baseRequest)
            {
                _logger.LogInformation("📨 Sending non-generic {CommandType}", request.GetType().Name);

                try
                {
                    // Generic type'ı bulup orchestrator'a forward et
                    var commandType = request.GetType();
                    var responseType = GetResponseType(commandType);

                    if (responseType != null)
                    {
                        // IRequest<T> - generic Send methodunu call et
                        var method = typeof(WorkflowOrchestrator).GetMethod(nameof(WorkflowOrchestrator.ExecuteWithOrchestrationAsync));
                        var genericMethod = method!.MakeGenericMethod(responseType);

                        var task = (Task)genericMethod.Invoke(_orchestrator.Value, new object[] { request, cancellationToken })!;
                        await task;

                        var resultProperty = task.GetType().GetProperty("Result");
                        return resultProperty?.GetValue(task);
                    }
                    else
                    {
                        // IRequest (no response) - direct execution
                        await _innerMediator.Value.Send(request, cancellationToken);
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Non-generic {CommandType} failed", request.GetType().Name);
                    throw;
                }
            }

            // Fallback - direct MediatR
            return await _innerMediator.Value.Send(request, cancellationToken);
        }

        /// <summary>
        /// Publish method - event publishing (direct passthrough to MediatR)
        /// Priority sadece command'lar için, event'ler için değil
        /// </summary>
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("📢 Publishing {NotificationType}", notification.GetType().Name);
            return _innerMediator.Value.Publish(notification, cancellationToken);
        }

        /// <summary>
        /// Generic Publish method - event publishing
        /// </summary>
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            _logger.LogDebug("📢 Publishing {NotificationType}", typeof(TNotification).Name);
            return _innerMediator.Value.Publish(notification, cancellationToken);
        }

        // ===================================================================
        // ISender INTERFACE IMPLEMENTATION - MediatR 12.x Compatibility
        // ===================================================================

        /// <summary>
        /// ISender.Send - Void command implementation (IRequest without response)
        /// </summary>
        public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            _logger.LogInformation("📨 Sending void command {CommandType}", typeof(TRequest).Name);

            try
            {
                // Void command'ı inner MediatR'a delegate et
                await _innerMediator.Value.Send(request, cancellationToken);
                _logger.LogInformation("✅ Void command {CommandType} completed", typeof(TRequest).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Void command {CommandType} failed", typeof(TRequest).Name);
                throw;
            }
        }

        /// <summary>
        /// CreateStream method - MediatR 12.x streaming support
        /// </summary>
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("🌊 Creating stream for {RequestType}", request.GetType().Name);

            // Streaming işlemlerini inner MediatR'a delegate et
            if (_innerMediator.Value is ISender sender)
            {
                return sender.CreateStream(request, cancellationToken);
            }

            // Fallback - empty stream
            return AsyncEnumerableEmpty<TResponse>();
        }

        /// <summary>
        /// Non-generic CreateStream method
        /// </summary>
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("🌊 Creating non-generic stream for {RequestType}", request.GetType().Name);

            // Streaming işlemlerini inner MediatR'a delegate et
            if (_innerMediator.Value is ISender sender)
            {
                return sender.CreateStream(request, cancellationToken);
            }

            // Fallback - empty stream
            return AsyncEnumerableEmpty<object?>();
        }

        /// <summary>
        /// Helper method - empty async enumerable oluştur
        /// </summary>
        private static async IAsyncEnumerable<T> AsyncEnumerableEmpty<T>()
        {
            await Task.CompletedTask;
            yield break;
        }

        /// <summary>
        /// Command'ın priority'sini attribute'dan oku
        /// </summary>
        private Priority GetCommandPriority(object command)
        {
            var attribute = command.GetType().GetCustomAttribute<PriorityAttribute>();
            var priority = attribute?.Priority ?? Priority.Normal;

            _logger.LogDebug("🏷️ {CommandType} priority: {Priority}", command.GetType().Name, priority);
            return priority;
        }

        /// <summary>
        /// Command'ın response type'ını bul (IRequest<T>'deki T)
        /// </summary>
        private Type? GetResponseType(Type commandType)
        {
            var requestInterface = commandType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

            return requestInterface?.GetGenericArguments().FirstOrDefault();
        }

        /// <summary>
        /// Inner MediatR instance'ı oluştur
        /// ServiceProvider'dan original MediatR'ı al (circular dependency'yi önle)
        /// </summary>
        private IMediator CreateInnerMediator()
        {
            _logger.LogDebug("🔧 Creating inner MediatR instance...");

            // ServiceProvider'dan original Mediator instance'ı al (not IMediator interface)
            var originalMediator = _serviceProvider.GetRequiredService<Mediator>();

            return originalMediator;
        }

        /// <summary>
        /// WorkflowOrchestrator instance'ı oluştur
        /// </summary>
        private WorkflowOrchestrator CreateOrchestrator()
        {
            _logger.LogDebug("🔧 Creating WorkflowOrchestrator...");

            var orchestratorLogger = _serviceProvider.GetRequiredService<ILogger<WorkflowOrchestrator>>();
            return new WorkflowOrchestrator(_innerMediator.Value, orchestratorLogger);
        }

        /// <summary>
        /// Cleanup - orchestrator'ı dispose et
        /// </summary>
        public void Dispose()
        {
            _logger.LogInformation("🛑 Disposing PriorityMediator...");

            if (_orchestrator.IsValueCreated)
            {
                _orchestrator.Value.Dispose();
            }

            _logger.LogInformation("✅ PriorityMediator disposed");
        }
    }
}