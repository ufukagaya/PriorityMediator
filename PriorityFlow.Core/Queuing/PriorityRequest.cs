// PriorityFlow.Core.Queuing - Priority Request Wrapper
// Wraps requests for asynchronous priority-based processing

using System;
using System.Threading;
using System.Threading.Tasks;

namespace PriorityFlow.Queuing
{
    /// <summary>
    /// Wrapper for requests in the priority queue
    /// Contains the request, priority metadata, and completion source for async results
    /// </summary>
    public class PriorityRequest
    {
        /// <summary>
        /// The original IRequest object to be processed
        /// </summary>
        public object Request { get; set; } = null!;

        /// <summary>
        /// The priority level determined for this request
        /// </summary>
        public Priority Priority { get; set; }

        /// <summary>
        /// Task completion source for returning results to the caller
        /// </summary>
        public TaskCompletionSource<object?> CompletionSource { get; set; } = null!;

        /// <summary>
        /// Cancellation token associated with the original request
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Timestamp when the request was enqueued
        /// </summary>
        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Unique identifier for tracking and debugging
        /// </summary>
        public Guid RequestId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Type name of the original request for logging and debugging
        /// </summary>
        public string RequestTypeName => Request?.GetType().Name ?? "Unknown";

        /// <summary>
        /// How long the request has been waiting in the queue
        /// </summary>
        public TimeSpan WaitTime => DateTime.UtcNow - EnqueuedAt;

        /// <summary>
        /// Creates a new PriorityRequest for requests without response
        /// </summary>
        /// <param name="request">The IRequest to wrap</param>
        /// <param name="priority">Priority level for processing</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Configured PriorityRequest</returns>
        public static PriorityRequest Create(object request, Priority priority, CancellationToken cancellationToken = default)
        {
            return new PriorityRequest
            {
                Request = request,
                Priority = priority,
                CompletionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously),
                CancellationToken = cancellationToken
            };
        }

        /// <summary>
        /// Creates a new PriorityRequest for requests with response
        /// </summary>
        /// <typeparam name="TResponse">Expected response type</typeparam>
        /// <param name="request">The IRequest&lt;TResponse&gt; to wrap</param>
        /// <param name="priority">Priority level for processing</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Configured PriorityRequest</returns>
        public static PriorityRequest Create<TResponse>(IRequest<TResponse> request, Priority priority, CancellationToken cancellationToken = default)
        {
            return new PriorityRequest
            {
                Request = request,
                Priority = priority,
                CompletionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously),
                CancellationToken = cancellationToken
            };
        }

        /// <summary>
        /// Sets the result for successful completion
        /// </summary>
        /// <param name="result">The result to set</param>
        public void SetResult(object? result = null)
        {
            if (!CompletionSource.Task.IsCompleted)
            {
                CompletionSource.SetResult(result);
            }
        }

        /// <summary>
        /// Sets an exception for failed completion
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        public void SetException(Exception exception)
        {
            if (!CompletionSource.Task.IsCompleted)
            {
                CompletionSource.SetException(exception);
            }
        }

        /// <summary>
        /// Sets cancellation for cancelled completion
        /// </summary>
        public void SetCanceled()
        {
            if (!CompletionSource.Task.IsCompleted)
            {
                CompletionSource.SetCanceled();
            }
        }

        /// <summary>
        /// Gets the awaitable task for the request result
        /// </summary>
        /// <returns>Task that completes when the request is processed</returns>
        public Task<object?> GetTask()
        {
            return CompletionSource.Task;
        }

        /// <summary>
        /// Gets the typed result for requests with response
        /// </summary>
        /// <typeparam name="TResponse">Expected response type</typeparam>
        /// <returns>Typed task result</returns>
        public async Task<TResponse> GetResult<TResponse>()
        {
            var result = await CompletionSource.Task;
            return result is TResponse typedResult ? typedResult : default!;
        }

        /// <summary>
        /// String representation for debugging and logging
        /// </summary>
        public override string ToString()
        {
            return $"PriorityRequest[{RequestId:N}]: {RequestTypeName} (Priority: {Priority}, Queued: {EnqueuedAt:HH:mm:ss.fff})";
        }
    }
}