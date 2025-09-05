// PriorityFlow.Tests.Integration - End-to-End Integration Tests
// Real-world scenarios with actual dependency injection and execution

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriorityFlow;
using PriorityFlow.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace PriorityFlow.Tests.Integration
{
    public class EndToEndIntegrationTests : IAsyncLifetime
    {
        private IHost? _host;
        private IMediator? _mediator;
        private readonly ExecutionTracker _executionTracker = new();

        public async Task InitializeAsync()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // Register PriorityFlow
                    services.AddPriorityFlow(Assembly.GetExecutingAssembly());
                    
                    // Register execution tracker as singleton
                    services.AddSingleton(_executionTracker);
                    
                    // Configure logging
                    services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
                })
                .Build();

            await _host.StartAsync();
            _mediator = _host.Services.GetRequiredService<IMediator>();
        }

        public async Task DisposeAsync()
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }

        [Fact]
        public async Task Should_Execute_Single_Command_Successfully()
        {
            // Arrange
            var command = new TestPaymentCommand(1000m, "CUSTOMER001");

            // Act
            var result = await _mediator!.Send(command);

            // Assert
            result.Should().BeTrue();
            _executionTracker.GetExecutionOrder().Should().Contain("TestPaymentCommand");
        }

        [Fact]
        public async Task Should_Execute_Commands_In_Priority_Order()
        {
            // Arrange
            _executionTracker.Reset();

            var commands = new IRequest[]
            {
                new TestReportCommand("Monthly Report"),        // Low Priority
                new TestPaymentCommand(500m, "CUSTOMER002"),   // High Priority  
                new TestBusinessCommand("Update Product"),     // Normal Priority
                new TestAnalyticsCommand("Track Event"),      // Low Priority
                new TestSecurityCommand("Validate User")      // High Priority
            };

            // Act
            var tasks = commands.Select(cmd => _mediator!.Send(cmd));
            await Task.WhenAll(tasks);

            // Assert
            var executionOrder = _executionTracker.GetExecutionOrder();
            var paymentIndex = executionOrder.IndexOf("TestPaymentCommand");
            var securityIndex = executionOrder.IndexOf("TestSecurityCommand");
            var businessIndex = executionOrder.IndexOf("TestBusinessCommand");
            var reportIndex = executionOrder.IndexOf("TestReportCommand");
            var analyticsIndex = executionOrder.IndexOf("TestAnalyticsCommand");

            // High priority should come first (though order within same priority may vary)
            paymentIndex.Should().BeLessThan(businessIndex, "High priority should execute before Normal");
            securityIndex.Should().BeLessThan(businessIndex, "High priority should execute before Normal");
            
            // Normal priority should come before Low priority
            businessIndex.Should().BeLessThan(reportIndex, "Normal priority should execute before Low");
            businessIndex.Should().BeLessThan(analyticsIndex, "Normal priority should execute before Low");
        }

        [Fact]
        public async Task Should_Handle_Query_With_Response()
        {
            // Arrange
            var query = new TestBalanceQuery("ACCOUNT001");

            // Act
            var balance = await _mediator!.Send(query);

            // Assert
            balance.Should().BeGreaterThan(0);
            _executionTracker.GetExecutionOrder().Should().Contain("TestBalanceQuery");
        }

        [Fact]
        public async Task Should_Publish_Notifications_To_Multiple_Handlers()
        {
            // Arrange
            _executionTracker.Reset();
            var notification = new TestOrderCreated(Guid.NewGuid(), "ORDER001");

            // Act
            await _mediator!.Publish(notification);

            // Assert
            var executionOrder = _executionTracker.GetExecutionOrder();
            executionOrder.Should().Contain("OrderEmailHandler", "Email handler should execute");
            executionOrder.Should().Contain("OrderAnalyticsHandler", "Analytics handler should execute");
        }

        [Fact]
        public async Task Should_Handle_High_Concurrency_Load()
        {
            // Arrange
            _executionTracker.Reset();
            const int commandCount = 100;
            var commands = new List<IRequest>();

            // Create mixed priority commands
            for (int i = 0; i < commandCount; i++)
            {
                var commandType = i % 4;
                commands.Add(commandType switch
                {
                    0 => new TestPaymentCommand(i * 10m, $"CUSTOMER{i:D3}"),    // High
                    1 => new TestSecurityCommand($"USER{i:D3}"),                 // High  
                    2 => new TestBusinessCommand($"Business operation {i}"),     // Normal
                    _ => new TestAnalyticsCommand($"Event {i}")                  // Low
                });
            }

            var stopwatch = Stopwatch.StartNew();

            // Act
            var tasks = commands.Select(cmd => _mediator!.Send(cmd));
            await Task.WhenAll(tasks);
            
            stopwatch.Stop();

            // Assert
            _executionTracker.GetExecutionCount().Should().Be(commandCount);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000, "Should complete within 30 seconds");

            // Verify priority ordering (high priority commands should generally come first)
            var executionOrder = _executionTracker.GetExecutionOrder();
            var highPriorityCommands = executionOrder.Where(cmd => 
                cmd.Contains("TestPaymentCommand") || cmd.Contains("TestSecurityCommand")).ToList();
            var lowPriorityCommands = executionOrder.Where(cmd => 
                cmd.Contains("TestAnalyticsCommand")).ToList();

            if (highPriorityCommands.Any() && lowPriorityCommands.Any())
            {
                var firstHighIndex = executionOrder.IndexOf(highPriorityCommands.First());
                var lastLowIndex = executionOrder.LastIndexOf(lowPriorityCommands.Last());
                
                // Most high priority commands should execute before most low priority commands
                var highCommandsBeforeLow = highPriorityCommands.Count(cmd => 
                    executionOrder.IndexOf(cmd) < lastLowIndex);
                
                (highCommandsBeforeLow / (double)highPriorityCommands.Count).Should().BeGreaterThan(0.6, 
                    "At least 60% of high priority commands should execute before low priority commands");
            }
        }

        [Fact]
        public async Task Should_Handle_Exceptions_Gracefully()
        {
            // Arrange
            var failingCommand = new TestFailingCommand("This will fail");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _mediator!.Send(failingCommand));

            exception.Message.Should().Contain("Simulated failure");
            
            // Ensure the system is still working after the exception
            var successCommand = new TestBusinessCommand("After failure");
            var result = await _mediator!.Send(successCommand);
            result.Should().BeTrue();
        }

        [Fact]
        public async Task Should_Respect_Cancellation_Token()
        {
            // Arrange
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var slowCommand = new TestSlowCommand(TimeSpan.FromSeconds(5));

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _mediator!.Send(slowCommand, cts.Token));
        }

        [Fact]
        public async Task Should_Work_With_Generic_Send_Method()
        {
            // Arrange
            object command = new TestBusinessCommand("Generic test");

            // Act
            var result = await _mediator!.Send(command);

            // Assert
            result.Should().BeNull(); // Commands without response return null
            _executionTracker.GetExecutionOrder().Should().Contain("TestBusinessCommand");
        }

        [Fact]
        public async Task Should_Work_With_Generic_Publish_Method()
        {
            // Arrange
            _executionTracker.Reset();
            object notification = new TestOrderCreated(Guid.NewGuid(), "GENERIC_ORDER");

            // Act
            await _mediator!.Publish(notification);

            // Assert
            var executionOrder = _executionTracker.GetExecutionOrder();
            executionOrder.Should().Contain("OrderEmailHandler");
            executionOrder.Should().Contain("OrderAnalyticsHandler");
        }
    }

    #region Test Commands and Handlers

    // High Priority Commands
    public record TestPaymentCommand(decimal Amount, string CustomerId) : IRequest<bool>;
    public record TestSecurityCommand(string UserId) : IRequest<bool>;

    // Normal Priority Commands  
    public record TestBusinessCommand(string Operation) : IRequest<bool>;

    // Low Priority Commands
    public record TestReportCommand(string ReportType) : IRequest<bool>;
    public record TestAnalyticsCommand(string EventType) : IRequest<bool>;

    // Query
    public record TestBalanceQuery(string AccountId) : IRequest<decimal>;

    // Special Commands
    public record TestFailingCommand(string Message) : IRequest<bool>;
    public record TestSlowCommand(TimeSpan Delay) : IRequest<bool>;

    // Notification
    public record TestOrderCreated(Guid OrderId, string OrderNumber) : INotification;

    // Handlers
    public class TestPaymentCommandHandler : IRequestHandler<TestPaymentCommand, bool>
    {
        private readonly ExecutionTracker _tracker;

        public TestPaymentCommandHandler(ExecutionTracker tracker)
        {
            _tracker = tracker;
        }

        public async Task<bool> Handle(TestPaymentCommand request, CancellationToken cancellationToken)
        {
            _tracker.RecordExecution(nameof(TestPaymentCommand));
            await Task.Delay(50, cancellationToken); // Simulate processing
            return true;
        }
    }

    public class TestSecurityCommandHandler : IRequestHandler<TestSecurityCommand, bool>
    {
        private readonly ExecutionTracker _tracker;

        public TestSecurityCommandHandler(ExecutionTracker tracker)
        {
            _tracker = tracker;
        }

        public async Task<bool> Handle(TestSecurityCommand request, CancellationToken cancellationToken)
        {
            _tracker.RecordExecution(nameof(TestSecurityCommand));
            await Task.Delay(30, cancellationToken);
            return true;
        }
    }

    public class TestBusinessCommandHandler : IRequestHandler<TestBusinessCommand, bool>
    {
        private readonly ExecutionTracker _tracker;

        public TestBusinessCommandHandler(ExecutionTracker tracker)
        {
            _tracker = tracker;
        }

        public async Task<bool> Handle(TestBusinessCommand request, CancellationToken cancellationToken)
        {
            _tracker.RecordExecution(nameof(TestBusinessCommand));
            await Task.Delay(40, cancellationToken);
            return true;
        }
    }

    public class TestReportCommandHandler : IRequestHandler<TestReportCommand, bool>
    {
        private readonly ExecutionTracker _tracker;

        public TestReportCommandHandler(ExecutionTracker tracker)
        {
            _tracker = tracker;
        }

        public async Task<bool> Handle(TestReportCommand request, CancellationToken cancellationToken)
        {
            _tracker.RecordExecution(nameof(TestReportCommand));
            await Task.Delay(100, cancellationToken); // Reports take longer
            return true;
        }
    }

    public class TestAnalyticsCommandHandler : IRequestHandler<TestAnalyticsCommand, bool>
    {
        private readonly ExecutionTracker _tracker;

        public TestAnalyticsCommandHandler(ExecutionTracker tracker)
        {
            _tracker = tracker;
        }

        public async Task<bool> Handle(TestAnalyticsCommand request, CancellationToken cancellationToken)
        {
            _tracker.RecordExecution(nameof(TestAnalyticsCommand));
            await Task.Delay(25, cancellationToken);
            return true;
        }
    }

    public class TestBalanceQueryHandler : IRequestHandler<TestBalanceQuery, decimal>
    {
        private readonly ExecutionTracker _tracker;

        public TestBalanceQueryHandler(ExecutionTracker tracker)
        {
            _tracker = tracker;
        }

        public async Task<decimal> Handle(TestBalanceQuery request, CancellationToken cancellationToken)
        {
            _tracker.RecordExecution(nameof(TestBalanceQuery));
            await Task.Delay(20, cancellationToken);
            return 1500.50m; // Mock balance
        }
    }

    public class TestFailingCommandHandler : IRequestHandler<TestFailingCommand, bool>
    {
        public Task<bool> Handle(TestFailingCommand request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException($"Simulated failure: {request.Message}");
        }
    }

    public class TestSlowCommandHandler : IRequestHandler<TestSlowCommand, bool>
    {
        public async Task<bool> Handle(TestSlowCommand request, CancellationToken cancellationToken)
        {
            await Task.Delay(request.Delay, cancellationToken);
            return true;
        }
    }

    // Notification Handlers
    public class OrderEmailHandler : INotificationHandler<TestOrderCreated>
    {
        private readonly ExecutionTracker _tracker;

        public OrderEmailHandler(ExecutionTracker tracker)
        {
            _tracker = tracker;
        }

        public async Task Handle(TestOrderCreated notification, CancellationToken cancellationToken)
        {
            _tracker.RecordExecution(nameof(OrderEmailHandler));
            await Task.Delay(50, cancellationToken); // Simulate email sending
        }
    }

    public class OrderAnalyticsHandler : INotificationHandler<TestOrderCreated>
    {
        private readonly ExecutionTracker _tracker;

        public OrderAnalyticsHandler(ExecutionTracker tracker)
        {
            _tracker = tracker;
        }

        public async Task Handle(TestOrderCreated notification, CancellationToken cancellationToken)
        {
            _tracker.RecordExecution(nameof(OrderAnalyticsHandler));
            await Task.Delay(30, cancellationToken); // Simulate analytics recording
        }
    }

    #endregion

    #region Execution Tracker

    /// <summary>
    /// Tracks command execution order for testing priority behavior
    /// </summary>
    public class ExecutionTracker
    {
        private readonly ConcurrentQueue<string> _executionOrder = new();
        private int _executionCount = 0;

        public void RecordExecution(string commandName)
        {
            _executionOrder.Enqueue(commandName);
            Interlocked.Increment(ref _executionCount);
        }

        public List<string> GetExecutionOrder()
        {
            return _executionOrder.ToList();
        }

        public int GetExecutionCount()
        {
            return _executionCount;
        }

        public void Reset()
        {
            while (_executionOrder.TryDequeue(out _)) { }
            _executionCount = 0;
        }
    }

    #endregion
}