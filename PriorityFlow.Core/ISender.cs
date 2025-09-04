// PriorityFlow.Core - Sender Interface (MediatR equivalent)

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PriorityFlow
{
    /// <summary>
    /// Main interface for sending commands/queries - equivalent to MediatR's IMediator
    /// </summary>
    public interface ISender
    {
        /// <summary>
        /// Send a request without response
        /// </summary>
        /// <typeparam name="TRequest">Request type</typeparam>
        /// <param name="request">Request instance</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest;

        /// <summary>
        /// Send a request with response
        /// </summary>
        /// <typeparam name="TResponse">Response type</typeparam>
        /// <param name="request">Request instance</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a request (object-based, for advanced scenarios)
        /// </summary>
        /// <param name="request">Request instance</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<object?> Send(object request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a stream for handling stream requests
        /// </summary>
        /// <typeparam name="TResponse">Response type</typeparam>
        /// <param name="request">Stream request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a stream (object-based, for advanced scenarios)
        /// </summary>
        /// <param name="request">Stream request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for publishing notifications/events
    /// </summary>
    public interface IPublisher
    {
        /// <summary>
        /// Publish a notification to all handlers
        /// </summary>
        /// <typeparam name="TNotification">Notification type</typeparam>
        /// <param name="notification">Notification instance</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;

        /// <summary>
        /// Publish a notification (object-based)
        /// </summary>
        /// <param name="notification">Notification instance</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task Publish(object notification, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Combined interface for sending commands and publishing events
    /// Equivalent to MediatR's IMediator interface
    /// </summary>
    public interface IMediator : ISender, IPublisher
    {
    }
}