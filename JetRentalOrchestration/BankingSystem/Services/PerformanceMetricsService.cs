// BankingSystem/Services/PerformanceMetricsService.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using JetRentalOrchestration.BankingSystem.Models;

namespace JetRentalOrchestration.BankingSystem.Services
{
    public interface IPerformanceMetricsService
    {
        void StartTransaction(string transactionType);
        void CompleteTransaction(string transactionType, bool success);
        void RecordCommandExecution(string commandName, TimeSpan executionTime);
        ProcessingMetrics GetMetrics();
        void Reset();
        string GenerateReport();
    }

    public class PerformanceMetricsService : IPerformanceMetricsService
    {
        private readonly ConcurrentDictionary<string, Stopwatch> _activeTransactions = new();
        private readonly ConcurrentDictionary<string, int> _commandCounts = new();
        private readonly ConcurrentDictionary<string, TimeSpan> _commandTotalTimes = new();
        private readonly ConcurrentBag<TransactionMetric> _completedTransactions = new();
        private readonly object _lockObject = new();

        public void StartTransaction(string transactionType)
        {
            var key = $"{transactionType}_{Guid.NewGuid()}";
            var stopwatch = Stopwatch.StartNew();
            _activeTransactions.TryAdd(key, stopwatch);
        }

        public void CompleteTransaction(string transactionType, bool success)
        {
            var matchingKey = _activeTransactions.Keys.FirstOrDefault(k => k.StartsWith(transactionType));
            if (matchingKey != null && _activeTransactions.TryRemove(matchingKey, out var stopwatch))
            {
                stopwatch.Stop();
                _completedTransactions.Add(new TransactionMetric
                {
                    TransactionType = transactionType,
                    ExecutionTime = stopwatch.Elapsed,
                    Success = success,
                    CompletedAt = DateTime.UtcNow
                });
            }
        }

        public void RecordCommandExecution(string commandName, TimeSpan executionTime)
        {
            _commandCounts.AddOrUpdate(commandName, 1, (key, value) => value + 1);
            _commandTotalTimes.AddOrUpdate(commandName, executionTime, (key, value) => value + executionTime);
        }

        public ProcessingMetrics GetMetrics()
        {
            lock (_lockObject)
            {
                var completedTransactions = _completedTransactions.ToList();
                var totalTransactions = completedTransactions.Count;
                var successfulTransactions = completedTransactions.Count(t => t.Success);
                var failedTransactions = totalTransactions - successfulTransactions;

                return new ProcessingMetrics
                {
                    TotalTransactions = totalTransactions,
                    CompletedTransactions = successfulTransactions,
                    FailedTransactions = failedTransactions,
                    BlockedTransactions = 0, // Would need additional tracking
                    AverageProcessingTime = totalTransactions > 0 
                        ? TimeSpan.FromMilliseconds(completedTransactions.Average(t => t.ExecutionTime.TotalMilliseconds))
                        : TimeSpan.Zero,
                    TotalProcessingTime = TimeSpan.FromMilliseconds(completedTransactions.Sum(t => t.ExecutionTime.TotalMilliseconds)),
                    CommandCounts = new Dictionary<string, int>(_commandCounts),
                    CommandTimes = new Dictionary<string, TimeSpan>(_commandTotalTimes)
                };
            }
        }

        public void Reset()
        {
            _activeTransactions.Clear();
            _commandCounts.Clear();
            _commandTotalTimes.Clear();
            _completedTransactions.Clear();
        }

        public string GenerateReport()
        {
            var metrics = GetMetrics();
            var report = new StringBuilder();
            
            report.AppendLine("=== PERFORMANCE METRICS REPORT ===");
            report.AppendLine($"Total Transactions: {metrics.TotalTransactions}");
            report.AppendLine($"Completed: {metrics.CompletedTransactions}");
            report.AppendLine($"Failed: {metrics.FailedTransactions}");
            report.AppendLine($"Success Rate: {(metrics.TotalTransactions > 0 ? (metrics.CompletedTransactions * 100.0 / metrics.TotalTransactions):0):F1}%");
            report.AppendLine($"Average Processing Time: {metrics.AverageProcessingTime.TotalMilliseconds:F1}ms");
            report.AppendLine($"Total Processing Time: {metrics.TotalProcessingTime.TotalMilliseconds:F1}ms");
            report.AppendLine();
            
            if (metrics.CommandCounts.Any())
            {
                report.AppendLine("=== COMMAND EXECUTION STATISTICS ===");
                foreach (var command in metrics.CommandCounts.OrderByDescending(kv => kv.Value))
                {
                    var avgTime = metrics.CommandTimes.ContainsKey(command.Key) 
                        ? metrics.CommandTimes[command.Key].TotalMilliseconds / command.Value
                        : 0;
                    report.AppendLine($"{command.Key}: {command.Value} executions, avg {avgTime:F1}ms");
                }
            }
            
            return report.ToString();
        }

        private class TransactionMetric
        {
            public string TransactionType { get; set; } = string.Empty;
            public TimeSpan ExecutionTime { get; set; }
            public bool Success { get; set; }
            public DateTime CompletedAt { get; set; }
        }
    }
}
