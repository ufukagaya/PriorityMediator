// PriorityFlow.Core.Queuing - Priority Queue Channel Interface
// Defines the contract for priority-based request queuing and consumption

using System.Collections.Generic;
using System.Threading;

namespace PriorityFlow.Queuing
{
    /// <summary>
    /// Interface for priority-based request queuing operations
    /// Provides thread-safe enqueueing and async consumption of priority requests
    /// </summary>
    public interface IPriorityQueueChannel
    {
        /// <summary>
        /// Attempts to add a request to the priority queue
        /// </summary>
        /// <param name="request">The priority request to enqueue</param>
        /// <returns>True if successfully added, false if the queue is full or closed</returns>
        bool TryAdd(PriorityRequest request);

        /// <summary>
        /// Gets an async enumerable for consuming requests in priority order
        /// Higher priority requests (Priority.High = 3) are processed first
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for stopping consumption</param>
        /// <returns>Async enumerable of priority requests ordered by priority</returns>
        IAsyncEnumerable<PriorityRequest> GetConsumingAsyncEnumerable(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current number of requests waiting in the queue
        /// </summary>
        /// <returns>Number of queued requests</returns>
        int GetQueueLength();

        /// <summary>
        /// Gets the number of requests by priority level
        /// Useful for monitoring and debugging queue distribution
        /// </summary>
        /// <returns>Dictionary with priority as key and count as value</returns>
        Dictionary<Priority, int> GetQueueDistribution();

        /// <summary>
        /// Marks the queue as complete for adding new requests
        /// Existing requests will continue to be processed
        /// </summary>
        void CompleteAdding();

        /// <summary>
        /// Gets whether the queue is accepting new requests
        /// </summary>
        bool IsAddingCompleted { get; }

        /// <summary>
        /// Gets the maximum capacity of the queue (if configured)
        /// </summary>
        int? MaxCapacity { get; }
    }
}