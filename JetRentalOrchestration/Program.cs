// Program.cs - Standard MediatR ile Problem Demonstration
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetRentalOrchestration.Extensions.PriorityFlow;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JetRentalOrchestration
{
    // ===================================================================
    // BUSINESS MODELS
    // ===================================================================
    public class BookingResult
    {
        public Guid BookingId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Steps { get; set; } = new();
        public TimeSpan TotalTime { get; set; }
    }

    // ===================================================================
    // MEDIATR COMMANDS - AUTOMATIC ORCHESTRATION WITH PRIORITYFLOW
    // ===================================================================

    public class BookJetCommand : IRequest<BookingResult>
    {
        public Guid CustomerId { get; set; }
        public Guid JetId { get; set; }
        public decimal Amount { get; set; }
    }

    public class ProcessPaymentCommand : IRequest<bool>
    {
        public Guid CustomerId { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
    }

    public class SendEmailCommand : IRequest<bool>
    {
        public Guid CustomerId { get; set; }
        public Guid BookingId { get; set; }
    }

    public class UpdateInventoryCommand : IRequest<bool>
    {
        public Guid JetId { get; set; }
    }

    public class LogAnalyticsCommand : IRequest<bool>
    {
        public string EventType { get; set; } = string.Empty;
        public object Data { get; set; } = new();
    }

    // ===================================================================
    // STANDARD MEDIATR HANDLERS - MANUAL ORCHESTRATION
    // ===================================================================

    public class BookJetHandler : IRequestHandler<BookJetCommand, BookingResult>
    {
        private readonly IMediator _mediator;
        private readonly ILogger<BookJetHandler> _logger;

        public BookJetHandler(IMediator mediator, ILogger<BookJetHandler> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<BookingResult> Handle(BookJetCommand request, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var result = new BookingResult { BookingId = Guid.NewGuid() };

            _logger.LogInformation("🚁 Starting jet booking process...");

            try
            {

                // Step 1: Create booking record
                _logger.LogInformation("✈️ Checking jet availability for {JetId}", request.JetId);
                await Task.Delay(100, cancellationToken); // Simulate availability check
                
                _logger.LogInformation("📝 Creating booking record {BookingId}", result.BookingId);
                await Task.Delay(50, cancellationToken); // Simulate database write

                // Step 2: Payment (Should be CRITICAL priority)
                _logger.LogInformation("💳 Processing payment...");
                var paymentSuccess = await _mediator.Send(new ProcessPaymentCommand
                {
                    CustomerId = request.CustomerId,
                    Amount = request.Amount,
                    Reference = $"JET-{result.BookingId}"
                }, cancellationToken);

                if (!paymentSuccess)
                {
                    result.Success = false;
                    result.Message = "Payment failed";
                    return result;
                }
                result.Steps.Add("Payment processed");

                // Step 3: Email (Should be NORMAL priority, can wait)
                _logger.LogInformation("📧 Sending confirmation email...");
                await _mediator.Send(new SendEmailCommand
                {
                    CustomerId = request.CustomerId,
                    BookingId = result.BookingId
                }, cancellationToken);
                result.Steps.Add("Email sent");

                // Step 4: Inventory (Should be LOW priority)
                _logger.LogInformation("📦 Updating inventory...");
                await _mediator.Send(new UpdateInventoryCommand
                {
                    JetId = request.JetId
                }, cancellationToken);
                result.Steps.Add("Inventory updated");

                // Step 5: Analytics (Should be BACKGROUND priority)
                _logger.LogInformation("📊 Logging analytics...");
                await _mediator.Send(new LogAnalyticsCommand
                {
                    EventType = "JetBooked",
                    Data = new { request.JetId, request.CustomerId, request.Amount }
                }, cancellationToken);
                result.Steps.Add("Analytics logged");

                result.Success = true;
                result.Message = "Booking completed";
                result.TotalTime = DateTime.UtcNow - startTime;

                _logger.LogInformation("✅ Booking completed in {Time}ms", result.TotalTime.TotalMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Booking failed");
                result.Success = false;
                result.Message = ex.Message;
                result.TotalTime = DateTime.UtcNow - startTime;
                return result;
            }
        }
    }

    // Individual handlers with different performance characteristics
    public class ProcessPaymentHandler : IRequestHandler<ProcessPaymentCommand, bool>
    {
        private readonly ILogger<ProcessPaymentHandler> _logger;

        public ProcessPaymentHandler(ILogger<ProcessPaymentHandler> logger)
        {
            _logger = logger;
        }

        public async Task<bool> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("💳 Processing payment of ${Amount} for customer {CustomerId}",
                request.Amount, request.CustomerId);

            // Simulate CRITICAL payment processing - should be highest priority
            await Task.Delay(800, cancellationToken); // Payment gateway call

            _logger.LogInformation("✅ Payment successful");
            return true;
        }
    }

    public class SendEmailHandler : IRequestHandler<SendEmailCommand, bool>
    {
        private readonly ILogger<SendEmailHandler> _logger;

        public SendEmailHandler(ILogger<SendEmailHandler> logger)
        {
            _logger = logger;
        }

        public async Task<bool> Handle(SendEmailCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("📧 Sending email to customer {CustomerId} for booking {BookingId}",
                request.CustomerId, request.BookingId);

            // Simulate NORMAL email sending - can wait if system busy
            await Task.Delay(400, cancellationToken);

            _logger.LogInformation("✅ Email sent");
            return true;
        }
    }

    public class UpdateInventoryHandler : IRequestHandler<UpdateInventoryCommand, bool>
    {
        private readonly ILogger<UpdateInventoryHandler> _logger;

        public UpdateInventoryHandler(ILogger<UpdateInventoryHandler> logger)
        {
            _logger = logger;
        }

        public async Task<bool> Handle(UpdateInventoryCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("📦 Updating inventory for jet {JetId}", request.JetId);

            // Simulate LOW priority inventory update - can be delayed
            await Task.Delay(200, cancellationToken);

            _logger.LogInformation("✅ Inventory updated");
            return true;
        }
    }

    public class LogAnalyticsHandler : IRequestHandler<LogAnalyticsCommand, bool>
    {
        private readonly ILogger<LogAnalyticsHandler> _logger;

        public LogAnalyticsHandler(ILogger<LogAnalyticsHandler> logger)
        {
            _logger = logger;
        }

        public async Task<bool> Handle(LogAnalyticsCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("📊 Logging analytics event: {EventType}", request.EventType);

            // Simulate BACKGROUND analytics - lowest priority
            await Task.Delay(100, cancellationToken);

            _logger.LogInformation("✅ Analytics logged");
            return true;
        }
    }

    // ===================================================================
    // PROGRAM ENTRY POINT
    // ===================================================================

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 PriorityFlow Demo - Testing Simplified Extensions");
            Console.WriteLine("====================================================\n");
            
            // Test our simplified PriorityFlow extensions
            await JetRentalOrchestration.TestScenario.PriorityTestRunner.RunPriorityTests();
            
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("🎯 Original Jet Rental Demo (with PriorityMediatR)");
            Console.WriteLine(new string('=', 60));

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    // Use our simplified PriorityMediatR instead of standard MediatR
                    services.AddPriorityMediatR(typeof(Program).Assembly);

                    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
                })
                .Build();

            var mediator = host.Services.GetRequiredService<IMediator>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            // Simulate jet booking request
            var bookingCommand = new BookJetCommand
            {
                CustomerId = Guid.NewGuid(),
                JetId = Guid.NewGuid(),
                Amount = 25000m
            };

            // Execute booking with PriorityMediatR
            var overallStart = DateTime.UtcNow;
            
            // Add timeout to prevent hanging
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            try
            {
                var result = await mediator.Send(bookingCommand, timeoutCts.Token);
                var overallEnd = DateTime.UtcNow;

                // Display results
                logger.LogInformation("Success: {Success}", result.Success);
                logger.LogInformation("Message: {Message}", result.Message);
                logger.LogInformation("Total Processing Time: {Time}ms", (overallEnd - overallStart).TotalMilliseconds);

                logger.LogInformation("\n📝 Steps Completed:");
                foreach (var step in result.Steps)
                {
                    logger.LogInformation("  ✅ {Step}", step);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("⏱️ Operation timed out - this indicates a potential deadlock in the current implementation");
                logger.LogInformation("🔍 This shows the complexity of building robust async systems!");
            }

            await host.StopAsync();
        }
    }
}