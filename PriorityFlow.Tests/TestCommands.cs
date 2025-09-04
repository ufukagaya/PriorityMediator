// PriorityFlow.Tests - Test Commands and Handlers

using System;
using System.Threading;
using System.Threading.Tasks;
using PriorityFlow;

namespace PriorityFlow.Tests
{
    // ===================================================================
    // HIGH PRIORITY COMMANDS (Auto-detected and Explicit)
    // ===================================================================

    // Auto-detected as High Priority (naming convention)
    public record PaymentProcessCommand(decimal Amount, string CustomerId) : IRequest<string>;
    
    public class PaymentProcessCommandHandler : IRequestHandler<PaymentProcessCommand, string>
    {
        public async Task<string> Handle(PaymentProcessCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"🔥 HIGH: Processing payment ${request.Amount} for customer {request.CustomerId}");
            await Task.Delay(300, cancellationToken); // Simulate payment processing
            return $"Payment ${request.Amount} processed successfully";
        }
    }

    // Auto-detected as High Priority (naming convention)
    public record SecurityValidationCommand(string UserId, string Action) : IRequest<bool>;
    
    public class SecurityValidationCommandHandler : IRequestHandler<SecurityValidationCommand, bool>
    {
        public async Task<bool> Handle(SecurityValidationCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"🔒 HIGH: Security validation for user {request.UserId}, action: {request.Action}");
            await Task.Delay(150, cancellationToken);
            return true;
        }
    }

    // Explicitly set as High Priority
    [Priority(Priority.High)]
    public record CriticalAlertCommand(string Message, string Severity) : IRequest;
    
    public class CriticalAlertCommandHandler : IRequestHandler<CriticalAlertCommand>
    {
        public async Task Handle(CriticalAlertCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"🚨 HIGH (Explicit): Critical alert - {request.Message} [{request.Severity}]");
            await Task.Delay(100, cancellationToken);
        }
    }

    // ===================================================================
    // NORMAL PRIORITY COMMANDS (Default behavior)
    // ===================================================================

    public record CreateProductCommand(string Name, decimal Price) : IRequest<int>;
    
    public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, int>
    {
        public async Task<int> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📦 NORMAL: Creating product '{request.Name}' - ${request.Price}");
            await Task.Delay(200, cancellationToken); // Simulate database operation
            var productId = new Random().Next(1000, 9999);
            return productId;
        }
    }

    public record UpdateInventoryCommand(int ProductId, int Quantity) : IRequest;
    
    public class UpdateInventoryCommandHandler : IRequestHandler<UpdateInventoryCommand>
    {
        public async Task Handle(UpdateInventoryCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📊 NORMAL: Updating inventory for product {request.ProductId}, qty: {request.Quantity}");
            await Task.Delay(180, cancellationToken);
        }
    }

    // ===================================================================
    // LOW PRIORITY COMMANDS (Auto-detected and Explicit)
    // ===================================================================

    // Auto-detected as Low Priority (naming convention)
    public record GenerateReportCommand(string ReportType, DateTime StartDate) : IRequest<string>;
    
    public class GenerateReportCommandHandler : IRequestHandler<GenerateReportCommand, string>
    {
        public async Task<string> Handle(GenerateReportCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📈 LOW: Generating {request.ReportType} report from {request.StartDate:yyyy-MM-dd}");
            await Task.Delay(400, cancellationToken); // Reports take longer
            return $"{request.ReportType}_Report_{DateTime.Now:yyyyMMdd}.pdf";
        }
    }

    // Auto-detected as Low Priority (naming convention)
    public record AnalyticsTrackingCommand(string EventType, object Data) : IRequest;
    
    public class AnalyticsTrackingCommandHandler : IRequestHandler<AnalyticsTrackingCommand>
    {
        public async Task Handle(AnalyticsTrackingCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📊 LOW: Tracking analytics event: {request.EventType}");
            await Task.Delay(120, cancellationToken);
        }
    }

    // Auto-detected as Low Priority (naming convention)  
    public record SendEmailNotificationCommand(string Email, string Subject, string Body) : IRequest;
    
    public class SendEmailNotificationCommandHandler : IRequestHandler<SendEmailNotificationCommand>
    {
        public async Task Handle(SendEmailNotificationCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📧 LOW: Sending email to {request.Email} - '{request.Subject}'");
            await Task.Delay(250, cancellationToken); // Email sending simulation
        }
    }

    // Explicitly set as Low Priority
    [Priority(Priority.Low)]
    public record CleanupTempFilesCommand(string Directory) : IRequest<int>;
    
    public class CleanupTempFilesCommandHandler : IRequestHandler<CleanupTempFilesCommand, int>
    {
        public async Task<int> Handle(CleanupTempFilesCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"🗑️ LOW (Explicit): Cleaning up temp files in {request.Directory}");
            await Task.Delay(300, cancellationToken);
            return new Random().Next(5, 25); // Number of files cleaned
        }
    }

    // ===================================================================
    // NOTIFICATION EVENTS (for testing publish functionality)
    // ===================================================================

    public record ProductCreatedEvent(int ProductId, string ProductName) : INotification;

    public class ProductCreatedEventHandler : INotificationHandler<ProductCreatedEvent>
    {
        public async Task Handle(ProductCreatedEvent notification, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📢 EVENT: Product created - {notification.ProductName} (ID: {notification.ProductId})");
            await Task.Delay(50, cancellationToken);
        }
    }

    public class ProductCreatedAnalyticsHandler : INotificationHandler<ProductCreatedEvent>
    {
        public async Task Handle(ProductCreatedEvent notification, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📊 ANALYTICS: Recording product creation event for {notification.ProductName}");
            await Task.Delay(30, cancellationToken);
        }
    }
}