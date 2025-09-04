// PriorityFlow.Core - Simplified Priority Mediator for Demo

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PriorityFlow
{
    /// <summary>
    /// Simplified Priority Mediator - focuses on core functionality
    /// </summary>
    public class SimplePriorityMediator : IMediator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SimplePriorityMediator> _logger;
        private readonly PriorityFlowConfiguration _configuration;

        public SimplePriorityMediator(
            IServiceProvider serviceProvider, 
            ILogger<SimplePriorityMediator> logger,
            PriorityFlowConfiguration? configuration = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration ?? new PriorityFlowConfiguration();
        }

        public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) 
            where TRequest : IRequest
        {
            var priority = PriorityConventions.GetConventionBasedPriority(typeof(TRequest));
            var commandType = typeof(TRequest).Name;

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("🎯 Executing {CommandType} with Priority.{Priority}", commandType, priority);
            }

            var startTime = DateTime.UtcNow;

            try
            {
                var handler = _serviceProvider.GetRequiredService<IRequestHandler<TRequest>>();
                await handler.Handle(request, cancellationToken);

                if (_configuration.EnableDebugLogging)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    _logger.LogInformation("✅ {CommandType} completed in {ElapsedMs}ms", commandType, elapsed.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ {CommandType} failed: {ErrorMessage}", commandType, ex.Message);
                
                if (ex.Message.Contains("No service for type"))
                {
                    throw new InvalidOperationException(
                        $"Handler not found for {commandType}. " +
                        $"Make sure you registered it: services.AddScoped<IRequestHandler<{commandType}>, YourHandler>();", 
                        ex);
                }
                
                throw;
            }
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var requestType = request.GetType();
            var priority = PriorityConventions.GetConventionBasedPriority(requestType);
            var commandType = requestType.Name;

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("🎯 Executing {CommandType} with Priority.{Priority}", commandType, priority);
            }

            var startTime = DateTime.UtcNow;

            try
            {
                var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
                var handler = _serviceProvider.GetRequiredService(handlerType);
                
                var handleMethod = handlerType.GetMethod("Handle")!;
                var task = (Task<TResponse>)handleMethod.Invoke(handler, new object[] { request, cancellationToken })!;
                var result = await task;

                if (_configuration.EnableDebugLogging)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    _logger.LogInformation("✅ {CommandType} completed in {ElapsedMs}ms", commandType, elapsed.TotalMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ {CommandType} failed: {ErrorMessage}", commandType, ex.Message);
                
                if (ex.Message.Contains("No service for type"))
                {
                    throw new InvalidOperationException(
                        $"Handler not found for {commandType}. " +
                        $"Make sure you registered it: services.AddScoped<IRequestHandler<{commandType}, {typeof(TResponse).Name}>, YourHandler>();", 
                        ex);
                }
                
                throw;
            }
        }

        public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            var requestType = request.GetType();
            
            // Check if it's IRequest<T> (has response)
            var requestInterface = requestType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

            if (requestInterface != null)
            {
                // Has response - use reflection to call Send<T>
                var responseType = requestInterface.GetGenericArguments()[0];
                var sendMethod = GetType().GetMethods()
                    .Where(m => m.Name == nameof(Send) && m.IsGenericMethodDefinition)
                    .FirstOrDefault(m => m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.IsGenericType);

                if (sendMethod != null)
                {
                    var genericMethod = sendMethod.MakeGenericMethod(responseType);
                    var task = (Task)genericMethod.Invoke(this, new[] { request, cancellationToken })!;
                    await task;
                    return task.GetType().GetProperty("Result")?.GetValue(task);
                }
            }
            
            // No response - assume IRequest
            await Send((IRequest)request, cancellationToken);
            return null;
        }

        public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) 
            where TNotification : INotification
        {
            var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>();
            
            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("📢 Publishing {NotificationType} to {HandlerCount} handlers", 
                    typeof(TNotification).Name, handlers.Count());
            }

            var tasks = handlers.Select(handler => handler.Handle(notification, cancellationToken));
            await Task.WhenAll(tasks);
        }

        public async Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            var notificationType = notification.GetType();
            var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);
            var handlers = _serviceProvider.GetServices(handlerType);

            if (_configuration.EnableDebugLogging)
            {
                _logger.LogInformation("📢 Publishing {NotificationType} to {HandlerCount} handlers", 
                    notificationType.Name, handlers.Count());
            }

            var handleMethod = handlerType.GetMethod("Handle");
            var tasks = handlers.Select(handler => (Task)handleMethod!.Invoke(handler, new[] { notification, cancellationToken })!);
            await Task.WhenAll(tasks);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var handler = _serviceProvider.GetRequiredService<IStreamRequestHandler<IStreamRequest<TResponse>, TResponse>>();
            return handler.Handle(request, cancellationToken);
        }

        public async IAsyncEnumerable<object?> CreateStream(object request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Simplified implementation
            yield return await Task.FromResult<object?>(request);
            await Task.Delay(1, cancellationToken);
        }
    }
}