// BankingSystem/BankingSystemDemo.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using JetRentalOrchestration.BankingSystem.Commands;
using JetRentalOrchestration.BankingSystem.Models;
using JetRentalOrchestration.BankingSystem.Services;

namespace JetRentalOrchestration.BankingSystem
{
    public class BankingSystemDemo
    {
        private readonly IMediator _mediator;
        private readonly ILogger<BankingSystemDemo> _logger;
        private readonly IPerformanceMetricsService _metricsService;

        public BankingSystemDemo(IMediator mediator, ILogger<BankingSystemDemo> logger, IPerformanceMetricsService metricsService)
        {
            _mediator = mediator;
            _logger = logger;
            _metricsService = metricsService;
        }

        /// <summary>
        /// Demonstrate simple transaction processing
        /// </summary>
        public async Task RunSimpleTransactionDemo()
        {
            _logger.LogInformation("🏦 === SIMPLE TRANSACTION DEMO ===");
            
            var account1 = Guid.NewGuid();
            var account2 = Guid.NewGuid();

            // Test different priority commands in sequence
            var commands = new List<(string Name, Func<Task> Action)>
            {
                ("Balance Inquiry (LOW)", async () => 
                {
                    var balance = await _mediator.Send(new GetBalanceCommand { AccountId = account1 });
                    _logger.LogInformation("Balance: ${Balance}", balance);
                }),
                
                ("Regular Transfer (NORMAL)", async () => 
                {
                    var result = await _mediator.Send(new ProcessTransferCommand 
                    { 
                        FromAccountId = account1, 
                        ToAccountId = account2, 
                        Amount = 1000,
                        Description = "Regular transfer"
                    });
                    _logger.LogInformation("Transfer Status: {Status}", result.Status);
                }),
                
                ("Wire Transfer (HIGH)", async () => 
                {
                    var result = await _mediator.Send(new ProcessWireTransferCommand 
                    { 
                        FromAccountId = account1, 
                        ToAccountId = account2, 
                        Amount = 5000,
                        BeneficiaryName = "John Doe",
                        SwiftCode = "ABCDUS33",
                        Reference = "Business payment"
                    });
                    _logger.LogInformation("Wire Transfer Status: {Status}", result.Status);
                }),
                
                ("Large Withdrawal (HIGH)", async () => 
                {
                    var result = await _mediator.Send(new ProcessLargeWithdrawalCommand 
                    { 
                        AccountId = account1, 
                        Amount = 15000,
                        Purpose = "Property purchase"
                    });
                    _logger.LogInformation("Withdrawal Status: {Status}", result.Status);
                })
            };

            foreach (var (name, action) in commands)
            {
                _logger.LogInformation("▶️ Executing: {CommandName}", name);
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    await action();
                    stopwatch.Stop();
                    _logger.LogInformation("✅ {CommandName} completed in {Time}ms\n", name, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "❌ {CommandName} failed in {Time}ms\n", name, stopwatch.ElapsedMilliseconds);
                }
                
                await Task.Delay(100); // Small delay between commands
            }
        }

        /// <summary>
        /// Demonstrate concurrent transaction processing with different priorities
        /// </summary>
        public async Task RunConcurrentTransactionDemo(int transactionCount = 20)
        {
            _logger.LogInformation("🏦 === CONCURRENT TRANSACTION DEMO ({Count} transactions) ===", transactionCount);
            
            _metricsService.Reset();
            var overallStopwatch = Stopwatch.StartNew();

            // Create a mix of different priority transactions
            var tasks = new List<Task>();
            var random = new Random();

            for (int i = 0; i < transactionCount; i++)
            {
                var account1 = Guid.NewGuid();
                var account2 = Guid.NewGuid();
                
                // Randomly choose transaction type with weighted distribution
                var transactionType = GetRandomTransactionType(random);
                
                tasks.Add(ExecuteTransactionAsync(transactionType, account1, account2, random));
                
                // Small delay between starting transactions to simulate real-world timing
                if (i < transactionCount - 1)
                    await Task.Delay(50);
            }

            // Wait for all transactions to complete
            _logger.LogInformation("⏳ Waiting for {Count} concurrent transactions to complete...", tasks.Count);
            await Task.WhenAll(tasks);
            
            overallStopwatch.Stop();
            
            // Generate and display performance report
            _logger.LogInformation("✅ All transactions completed in {Time}ms", overallStopwatch.ElapsedMilliseconds);
            _logger.LogInformation("\n{Report}", _metricsService.GenerateReport());
        }

        /// <summary>
        /// Demonstrate fraud detection and account freezing (Critical priority)
        /// </summary>
        public async Task RunFraudDetectionDemo()
        {
            _logger.LogInformation("🏦 === FRAUD DETECTION DEMO ===");
            
            var suspiciousAccount = Guid.NewGuid();
            
            // Simulate a high-risk transaction that should trigger fraud detection
            _logger.LogInformation("🚨 Simulating suspicious large withdrawal...");
            
            var result = await _mediator.Send(new ProcessLargeWithdrawalCommand
            {
                AccountId = suspiciousAccount,
                Amount = 75000, // Large amount likely to trigger fraud detection
                Purpose = "Emergency cash withdrawal"
            });

            _logger.LogInformation("Transaction Result: {Status}", result.Status);
            
            if (result.Status == TransactionStatus.Blocked)
            {
                _logger.LogInformation("🧊 Account should now be frozen due to fraud alert");
                foreach (var step in result.ProcessingSteps)
                {
                    _logger.LogInformation("  📝 {Step}", step);
                }
            }
        }

        /// <summary>
        /// Stress test with high concurrency to show priority differences
        /// </summary>
        public async Task RunStressTest(int concurrentTransactions = 50)
        {
            _logger.LogInformation("🏦 === STRESS TEST ({Count} concurrent transactions) ===", concurrentTransactions);
            
            _metricsService.Reset();
            var overallStopwatch = Stopwatch.StartNew();

            // Create many concurrent transactions of different priorities
            var tasks = new List<Task>();
            var random = new Random();

            // Add some critical priority transactions (fraud checks)
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(ExecuteFraudCheckAsync(Guid.NewGuid(), random));
            }

            // Add many normal/low priority transactions
            for (int i = 0; i < concurrentTransactions - 5; i++)
            {
                var account1 = Guid.NewGuid();
                var account2 = Guid.NewGuid();
                var transactionType = GetRandomTransactionType(random);
                tasks.Add(ExecuteTransactionAsync(transactionType, account1, account2, random));
            }

            // Shuffle tasks to simulate random arrival order
            tasks = tasks.OrderBy(x => random.Next()).ToList();

            _logger.LogInformation("⏳ Starting {Count} concurrent transactions (including 5 critical fraud checks)...", tasks.Count);
            
            // Start all tasks concurrently
            await Task.WhenAll(tasks);
            
            overallStopwatch.Stop();
            
            _logger.LogInformation("✅ Stress test completed in {Time}ms", overallStopwatch.ElapsedMilliseconds);
            _logger.LogInformation("\n{Report}", _metricsService.GenerateReport());
        }

        private async Task ExecuteTransactionAsync(string transactionType, Guid account1, Guid account2, Random random)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _metricsService.StartTransaction(transactionType);
                
                switch (transactionType)
                {
                    case "BalanceInquiry":
                        await _mediator.Send(new GetBalanceCommand { AccountId = account1 });
                        break;
                        
                    case "Transfer":
                        await _mediator.Send(new ProcessTransferCommand 
                        { 
                            FromAccountId = account1, 
                            ToAccountId = account2, 
                            Amount = random.Next(100, 5000),
                            Description = "Automated transfer"
                        });
                        break;
                        
                    case "WireTransfer":
                        await _mediator.Send(new ProcessWireTransferCommand 
                        { 
                            FromAccountId = account1, 
                            ToAccountId = account2, 
                            Amount = random.Next(1000, 25000),
                            BeneficiaryName = "Test Beneficiary",
                            SwiftCode = "TESTUS33",
                            Reference = "Automated wire"
                        });
                        break;
                        
                    case "LargeWithdrawal":
                        await _mediator.Send(new ProcessLargeWithdrawalCommand 
                        { 
                            AccountId = account1, 
                            Amount = random.Next(10000, 50000),
                            Purpose = "Large withdrawal"
                        });
                        break;
                }
                
                stopwatch.Stop();
                _metricsService.CompleteTransaction(transactionType, true);
                _metricsService.RecordCommandExecution(transactionType, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _metricsService.CompleteTransaction(transactionType, false);
                _logger.LogError(ex, "Transaction {TransactionType} failed", transactionType);
            }
        }

        private async Task ExecuteFraudCheckAsync(Guid accountId, Random random)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _metricsService.StartTransaction("FraudCheck");
                
                await _mediator.Send(new DetectFraudCommand
                {
                    TransactionId = Guid.NewGuid(),
                    AccountId = accountId,
                    Amount = random.Next(10000, 100000),
                    Type = TransactionType.Withdrawal,
                    Description = "Suspicious transaction"
                });
                
                stopwatch.Stop();
                _metricsService.CompleteTransaction("FraudCheck", true);
                _metricsService.RecordCommandExecution("FraudCheck", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _metricsService.CompleteTransaction("FraudCheck", false);
                _logger.LogError(ex, "Fraud check failed");
            }
        }

        private string GetRandomTransactionType(Random random)
        {
            // Weighted distribution to simulate real banking scenarios
            var weights = new Dictionary<string, int>
            {
                { "BalanceInquiry", 40 },  // Most common
                { "Transfer", 35 },        // Common
                { "WireTransfer", 15 },    // Less common, high priority
                { "LargeWithdrawal", 10 }  // Rare, high priority
            };

            var totalWeight = weights.Values.Sum();
            var randomValue = random.Next(0, totalWeight);
            var currentWeight = 0;

            foreach (var kvp in weights)
            {
                currentWeight += kvp.Value;
                if (randomValue < currentWeight)
                    return kvp.Key;
            }

            return "BalanceInquiry"; // Fallback
        }
    }
}

