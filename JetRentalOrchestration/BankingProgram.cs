// BankingProgram.cs - Advanced Banking System Demo
// Demonstrates the differences between standard MediatR and PriorityFlow MediatR

using System;
using System.Threading.Tasks;
using JetRentalOrchestration.BankingSystem;
using JetRentalOrchestration.BankingSystem.Services;
using JetRentalOrchestration.Extensions.PriorityFlow;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JetRentalOrchestration
{
    class BankingProgram
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🏦 === ADVANCED BANKING SYSTEM DEMO ===");
            Console.WriteLine("Comparing Standard MediatR vs PriorityFlow MediatR");
            Console.WriteLine();

            // Test 1: Standard MediatR
            await RunBankingSystemTest("STANDARD MEDIATR", false);
            
            Console.WriteLine("\n" + new string('=', 80) + "\n");
            
            // Test 2: PriorityFlow MediatR
            await RunBankingSystemTest("PRIORITYFLOW MEDIATR", true);
            
            Console.WriteLine("\n🎯 === COMPARISON COMPLETE ===");
            Console.WriteLine("Notice the execution order differences:");
            Console.WriteLine("• Standard MediatR: Sequential execution, no priority handling");
            Console.WriteLine("• PriorityFlow MediatR: Priority-based execution (Critical → High → Normal → Low → Background)");
            Console.WriteLine("• Fraud detection (Critical) executes immediately in PriorityFlow");
            Console.WriteLine("• Background tasks (Analytics, Notifications) are deferred in PriorityFlow");
        }

        static async Task RunBankingSystemTest(string testName, bool usePriorityFlow)
        {
            Console.WriteLine($"🚀 === {testName} TEST ===");
            
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // Register banking system handlers
                    if (usePriorityFlow)
                    {
                        services.AddPriorityMediatR(typeof(BankingProgram).Assembly);
                        Console.WriteLine("✅ Using PriorityFlow MediatR with priority-based execution");
                    }
                    else
                    {
                        services.AddMediatR(typeof(BankingProgram).Assembly);
                        Console.WriteLine("✅ Using Standard MediatR with sequential execution");
                    }

                    // Register services
                    services.AddSingleton<IPerformanceMetricsService, PerformanceMetricsService>();
                    services.AddTransient<BankingSystemDemo>();
                    
                    // Configure logging
                    services.AddLogging(builder => 
                    {
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });
                })
                .Build();

            var demo = host.Services.GetRequiredService<BankingSystemDemo>();
            var logger = host.Services.GetRequiredService<ILogger<BankingProgram>>();

            try
            {
                Console.WriteLine();
                
                // Demo 1: Simple sequential transactions
                logger.LogInformation("📋 Demo 1: Simple Transaction Processing");
                await demo.RunSimpleTransactionDemo();
                
                Console.WriteLine(new string('-', 60));
                
                // Demo 2: Concurrent transactions (shows priority differences)
                logger.LogInformation("📋 Demo 2: Concurrent Transaction Processing");
                await demo.RunConcurrentTransactionDemo(15);
                
                Console.WriteLine(new string('-', 60));
                
                // Demo 3: Fraud detection (Critical priority)
                logger.LogInformation("📋 Demo 3: Fraud Detection (Critical Priority)");
                await demo.RunFraudDetectionDemo();
                
                Console.WriteLine(new string('-', 60));
                
                // Demo 4: Stress test
                logger.LogInformation("📋 Demo 4: Stress Test (High Concurrency)");
                await demo.RunStressTest(30);
                
                Console.WriteLine();
                logger.LogInformation("✅ {TestName} completed successfully", testName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ {TestName} failed", testName);
            }
            finally
            {
                await host.StopAsync();
            }
        }
    }
}

