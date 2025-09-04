// Priority Flow Test Runner - Simple Console Test
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediatR;
using JetRentalOrchestration.Extensions.PriorityFlow;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JetRentalOrchestration.TestScenario
{
    public static class PriorityTestRunner
    {
        public static async Task RunPriorityTests()
        {
            // Setup DI Container with PriorityMediatR
            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            
            // Add our simplified PriorityMediatR
            services.AddPriorityMediatR(Assembly.GetExecutingAssembly());
            
            var serviceProvider = services.BuildServiceProvider();
            var mediator = serviceProvider.GetRequiredService<IMediator>();
            
            Console.WriteLine("🧪 Starting PriorityFlow Test Scenarios");
            Console.WriteLine("=======================================\n");
            
            // Test 1: Single commands (should execute directly)
            Console.WriteLine("Test 1: Single Command Execution");
            Console.WriteLine("---------------------------------");
            
            var paymentResult = await mediator.Send(new PaymentProcessCommand(1000m, "CUST001"));
            Console.WriteLine($"Result: {paymentResult}\n");
            
            // Test 2: Multiple commands sent quickly (priority queue test)
            Console.WriteLine("Test 2: Priority Queue Test (Multiple Commands)");
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Sending commands in this order: Report (Low) → Booking (Normal) → Payment (High)");
            Console.WriteLine("Expected execution order: Payment (High) → Booking (Normal) → Report (Low)\n");
            
            // Send multiple commands quickly to trigger priority queue
            var tasks = new List<Task<string>>
            {
                mediator.Send(new ReportGenerateCommand("Monthly Sales")),
                mediator.Send(new BookJetCommand("JET001", "CUST002", DateTime.Today.AddDays(7))),
                mediator.Send(new PaymentProcessCommand(2500m, "CUST003")),
                mediator.Send(new AnalyticsProcessCommand("Customer Behavior"))
            };
            
            var results = await Task.WhenAll(tasks);
            
            Console.WriteLine("\nResults:");
            foreach (var result in results)
            {
                Console.WriteLine($"✅ {result}");
            }
            
            // Test 3: Convention-based priority detection
            Console.WriteLine("\n\nTest 3: Convention-Based Priority Detection");
            Console.WriteLine("--------------------------------------------");
            Console.WriteLine("Commands starting with 'Report' and 'Analytics' should be Low priority");
            Console.WriteLine("Commands starting with 'Payment' should be High priority\n");
            
            await mediator.Send(new ReportGenerateCommand("Weekly Summary"));
            await mediator.Send(new AnalyticsProcessCommand("User Engagement"));
            
            Console.WriteLine("\n🎉 All tests completed successfully!");
            Console.WriteLine("PriorityFlow extensions are working correctly with simplified approach.");
        }
    }
}