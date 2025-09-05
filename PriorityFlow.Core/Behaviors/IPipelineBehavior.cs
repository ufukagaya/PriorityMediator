// PriorityFlow.Core.Behaviors - Pipeline Behavior Support
// Full MediatR compatibility for cross-cutting concerns

using System;
using System.Threading;
using System.Threading.Tasks;

namespace PriorityFlow.Behaviors
{
    /// <summary>
    /// Pipeline behavior for requests without response - compatible with MediatR
    /// Use for cross-cutting concerns like logging, validation, caching, etc.
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    public interface IPipelineBehavior<in TRequest> where TRequest : IRequest
    {
        /// <summary>
        /// Handle pipeline behavior
        /// </summary>
        /// <param name="request">Request instance</param>
        /// <param name="next">Next behavior in pipeline</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Pipeline behavior for requests with response - compatible with MediatR
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handle pipeline behavior
        /// </summary>
        /// <param name="request">Request instance</param>
        /// <param name="next">Next behavior in pipeline</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Generic pipeline behavior that applies to all requests
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public interface IGenericPipelineBehavior<TRequest, TResponse>
    {
        Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
    }
}

