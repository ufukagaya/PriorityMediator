// Test Commands for Priority Flow Testing
using MediatR;
using JetRentalOrchestration.Extensions.PriorityFlow;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace JetRentalOrchestration.TestScenario
{
    // High Priority Command
    [Priority(Priority.High)]
    public record PaymentProcessCommand(decimal Amount, string CustomerId) : IRequest<string>;

    public class PaymentProcessCommandHandler : IRequestHandler<PaymentProcessCommand, string>
    {
        public async Task<string> Handle(PaymentProcessCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"🔥 HIGH: Processing payment ${request.Amount} for customer {request.CustomerId}");
            await Task.Delay(100); // Simulate processing
            return $"Payment of ${request.Amount} processed successfully";
        }
    }

    // Normal Priority Command (no attribute, uses convention)
    public record BookJetCommand(string JetId, string CustomerId, DateTime Date) : IRequest<string>;

    public class BookJetCommandHandler : IRequestHandler<BookJetCommand, string>
    {
        public async Task<string> Handle(BookJetCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"✈️  NORMAL: Booking jet {request.JetId} for customer {request.CustomerId}");
            await Task.Delay(200); // Simulate processing
            return $"Jet {request.JetId} booked for {request.Date}";
        }
    }

    // Low Priority Command (uses naming convention)
    public record ReportGenerateCommand(string ReportType) : IRequest<string>;

    public class ReportGenerateCommandHandler : IRequestHandler<ReportGenerateCommand, string>
    {
        public async Task<string> Handle(ReportGenerateCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📊 LOW: Generating report {request.ReportType}");
            await Task.Delay(150); // Simulate processing
            return $"Report {request.ReportType} generated";
        }
    }

    // Explicit Low Priority Command
    [Priority(Priority.Low)]
    public record AnalyticsProcessCommand(string DataSet) : IRequest<string>;

    public class AnalyticsProcessCommandHandler : IRequestHandler<AnalyticsProcessCommand, string>
    {
        public async Task<string> Handle(AnalyticsProcessCommand request, CancellationToken cancellationToken)
        {
            Console.WriteLine($"📈 LOW: Processing analytics for {request.DataSet}");
            await Task.Delay(180); // Simulate processing
            return $"Analytics processed for {request.DataSet}";
        }
    }
}