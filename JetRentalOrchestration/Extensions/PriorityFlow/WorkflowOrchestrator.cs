// Extensions/PriorityFlow/WorkflowOrchestrator.cs
// Bu dosyayı Visual Studio'da Extensions/PriorityFlow klasörüne oluştur

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JetRentalOrchestration.Extensions.PriorityFlow
{
    /// <summary>
    /// Otomatik command workflow orchestration
    /// Priority-based background processing ve follow-up command execution
    /// </summary>
    public class WorkflowOrchestrator : IDisposable
    {
        private readonly IMediator _innerMediator;
        private readonly ILogger<WorkflowOrchestrator> _logger;
        private readonly PriorityQueue _queue;
        private readonly Task _processingTask;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _processingSemaphore;

        public WorkflowOrchestrator(IMediator innerMediator, ILogger<WorkflowOrchestrator> logger)
        {
            _innerMediator = innerMediator;
            _logger = logger;
            _queue = new PriorityQueue();
            _cancellationTokenSource = new CancellationTokenSource();
            _processingSemaphore = new SemaphoreSlim(1, 1); // Single-threaded processing

            // Background processing task'ini başlat
            _processingTask = ProcessQueueAsync(_cancellationTokenSource.Token);

            _logger.LogInformation("🚀 WorkflowOrchestrator started - ready for priority-based command processing");
        }

        /// <summary>
        /// Command'ı priority queue'ya ekle ve sonucunu bekle
        /// Ana thread block olmaz, background processing yapar
        /// </summary>
        public async Task<T> ExecuteWithOrchestrationAsync<T>(IRequest<T> command, CancellationToken cancellationToken = default)
        {
            var priority = GetCommandPriority(command);
            var queuedAt = DateTime.UtcNow;

            _logger.LogInformation("📥 Queueing {CommandType} with priority {Priority}",
                command.GetType().Name, priority);

            // Task completion source oluştur
            var completionSource = new TaskCompletionSource<object>();
            var queueItem = new PriorityQueueItem
            {
                Command = command, // IRequest<T> to IRequest cast otomatik olur
                Priority = priority,
                QueuedAt = queuedAt,
                CompletionSource = completionSource
            };

            // Priority queue'ya ekle
            _queue.Enqueue(queueItem);

            _logger.LogDebug("📊 Queue status: {QueueStatus}",
                string.Join(", ", _queue.GetQueueStatus().Select(kv => $"{kv.Key}:{kv.Value}")));

            // Background processing tamamlanmasını bekle
            var result = await completionSource.Task;

            var totalTime = DateTime.UtcNow - queuedAt;
            _logger.LogInformation("✅ {CommandType} completed in {TotalTime}ms",
                command.GetType().Name, totalTime.TotalMilliseconds);

            return (T)result;
        }

        /// <summary>
        /// Background'da sürekli çalışan queue processor
        /// Priority sırasıyla command'ları işler ve workflow orchestration yapar
        /// </summary>
        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔄 Starting background queue processing...");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _processingSemaphore.WaitAsync(cancellationToken);

                    try
                    {
                        var item = _queue.Dequeue();
                        if (item == null)
                        {
                            // Queue boş, kısa pause
                            await Task.Delay(50, cancellationToken);
                            continue;
                        }

                        await ProcessQueueItem(item, cancellationToken);
                    }
                    finally
                    {
                        _processingSemaphore.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("🛑 Queue processing cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Critical error in queue processor - continuing...");
                    await Task.Delay(1000, cancellationToken); // Error durumunda pause
                }
            }

            _logger.LogInformation("🏁 Background queue processing stopped");
        }

        /// <summary>
        /// Tek bir queue item'ını işle
        /// Command'ı execute et ve workflow orchestration yap
        /// </summary>
        private async Task ProcessQueueItem(PriorityQueueItem item, CancellationToken cancellationToken)
        {
            var waitTime = DateTime.UtcNow - item.QueuedAt;
            _logger.LogInformation("⚡ Processing {CommandType} [Priority: {Priority}] (waited: {WaitTime}ms)",
                item.CommandType, item.Priority, waitTime.TotalMilliseconds);

            try
            {
                var processingStart = DateTime.UtcNow;

                // Ana command'ı execute et
                var result = await ExecuteCommand(item.Command, cancellationToken);

                var processingTime = DateTime.UtcNow - processingStart;
                _logger.LogInformation("🎯 {CommandType} executed in {ProcessingTime}ms",
                    item.CommandType, processingTime.TotalMilliseconds);

                // Workflow orchestration - follow-up command'ları kontrol et
                _ = HandleWorkflowOrchestration(item.Command, result, cancellationToken);

                // Success - completion source'u set et
                item.CompletionSource.SetResult(result ?? new object());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error processing {CommandType}", item.CommandType);
                item.CompletionSource.SetException(ex);
            }
        }

        /// <summary>
        /// Command'ı execute et - generic type handling ile
        /// CRITICAL: Processing context'i set ederek recursive calls'ı önler
        /// </summary>
        private async Task<object?> ExecuteCommand(IBaseRequest command, CancellationToken cancellationToken)
        {
            try
            {
                // Set processing context to prevent deadlock from recursive PriorityMediator calls
                using (PriorityMediator.SetProcessingContext())
                {
                    // MediatR'ın Send methodunu call et
                    var result = await _innerMediator.Send(command, cancellationToken);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to execute {CommandType}", command.GetType().Name);
                throw;
            }
        }

        /// <summary>
        /// Workflow orchestration - follow-up command'ları handle et
        /// </summary>
        private Task HandleWorkflowOrchestration(IBaseRequest command, object? result, CancellationToken cancellationToken)
        {
            // Command IWorkflowCommand implement ediyor mu?
            if (command.GetType().GetInterfaces().Any(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IWorkflowCommand<>)))
            {
                try
                {
                    // GetFollowUpCommands methodunu call et
                    var method = command.GetType().GetMethod("GetFollowUpCommands");
                    if (method != null)
                    {
                        var followUpCommands = method.Invoke(command, new[] { result }) as IEnumerable<IBaseRequest>;

                        if (followUpCommands != null && followUpCommands.Any())
                        {
                            _logger.LogInformation("🔗 {CommandType} triggered {FollowUpCount} follow-up commands",
                                command.GetType().Name, followUpCommands.Count());

                            // Follow-up command'ları queue'ya ekle (priority sırasıyla işlenecek)
                            _ = QueueFollowUpCommands(followUpCommands);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in workflow orchestration for {CommandType}", command.GetType().Name);
                    // Orchestration hata verse de ana command başarılı sayılır
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Follow-up command'ları priority queue'ya ekle
        /// </summary>
        private Task QueueFollowUpCommands(IEnumerable<IBaseRequest> commands)
        {
            foreach (var command in commands)
            {
                var priority = GetCommandPriority(command);
                var queueItem = new PriorityQueueItem
                {
                    Command = command,
                    Priority = priority,
                    QueuedAt = DateTime.UtcNow,
                    CompletionSource = new TaskCompletionSource<object>()
                };

                _queue.Enqueue(queueItem);

                _logger.LogDebug("📬 Queued follow-up: {CommandType} [Priority: {Priority}]",
                    command.GetType().Name, priority);

                // Follow-up command'ın completion'ını background'da handle et
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await queueItem.CompletionSource.Task;
                        _logger.LogDebug("✅ Follow-up completed: {CommandType}", command.GetType().Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Follow-up command failed: {CommandType}", command.GetType().Name);
                    }
                });
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Command'ın priority'sini attribute'dan oku
        /// </summary>
        private Priority GetCommandPriority(IBaseRequest command)
        {
            var attribute = command.GetType().GetCustomAttribute<PriorityAttribute>();
            return attribute?.Priority ?? Priority.Normal;
        }

        /// <summary>
        /// Cleanup - background processing'i durdur
        /// </summary>
        public void Dispose()
        {
            _logger.LogInformation("🛑 Shutting down WorkflowOrchestrator...");

            _cancellationTokenSource.Cancel();

            try
            {
                _processingTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error during orchestrator shutdown");
            }

            _cancellationTokenSource.Dispose();
            _processingSemaphore.Dispose();
            _queue.Clear();

            _logger.LogInformation("✅ WorkflowOrchestrator disposed");
        }
    }
}