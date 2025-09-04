// PriorityFlow.Core - Core MediatR-like Interfaces
// Drop-in replacement for MediatR with priority support

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PriorityFlow
{
    /// <summary>
    /// Marker interface to represent a request (command without response)
    /// </summary>
    public interface IRequest { }

    /// <summary>
    /// Marker interface to represent a request with a response
    /// </summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IRequest<out TResponse> { }

    /// <summary>
    /// Handler for a request without response
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    public interface IRequestHandler<in TRequest> where TRequest : IRequest
    {
        Task Handle(TRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Handler for a request with response
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Notification (event) interface
    /// </summary>
    public interface INotification { }

    /// <summary>
    /// Handler for notifications
    /// </summary>
    /// <typeparam name="TNotification">Notification type</typeparam>
    public interface INotificationHandler<in TNotification> where TNotification : INotification
    {
        Task Handle(TNotification notification, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Stream request interface
    /// </summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IStreamRequest<out TResponse> { }

    /// <summary>
    /// Stream handler interface
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IStreamRequestHandler<in TRequest, out TResponse> where TRequest : IStreamRequest<TResponse>
    {
        IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Pipeline behavior for requests without response
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    public interface IPipelineBehavior<in TRequest> where TRequest : IRequest
    {
        Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Pipeline behavior for requests with response
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Stream pipeline behavior
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IStreamPipelineBehavior<in TRequest, TResponse> where TRequest : IStreamRequest<TResponse>
    {
        IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Delegate representing the continuation of the request handling pipeline
    /// </summary>
    public delegate Task RequestHandlerDelegate();

    /// <summary>
    /// Delegate representing the continuation of the request handling pipeline with response
    /// </summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

    /// <summary>
    /// Delegate representing the continuation of the stream handling pipeline
    /// </summary>
    /// <typeparam name="TResponse">Response type</typeparam>
    public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>();
}