// PriorityFlow.Benchmarks - Simplified Benchmark Suite
// Essential performance comparison without complex dependencies

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PriorityFlow;
using PriorityFlow.Extensions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace PriorityFlow.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, iterationCount: 50)]
[SimpleJob(RunStrategy.Throughput, iterationCount: 200)]
public class SimpleBenchmark
{
    private ServiceProvider _priorityFlowProvider = null!;
    private ServiceProvider _mediatrProvider = null!;
    private IMediator _priorityFlowMediator = null!;
    private IMediator _mediatrMediator = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup PriorityFlow
        var priorityServices = new ServiceCollection();
        priorityServices.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Error));
        priorityServices.AddPriorityFlow(Assembly.GetExecutingAssembly());
        _priorityFlowProvider = priorityServices.BuildServiceProvider();
        _priorityFlowMediator = _priorityFlowProvider.GetRequiredService<IMediator>();

        // Setup simple comparison provider (just use PriorityFlow for comparison)
        var mediatrServices = new ServiceCollection();
        mediatrServices.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Error));
        mediatrServices.AddPriorityFlow(Assembly.GetExecutingAssembly());
        _mediatrProvider = mediatrServices.BuildServiceProvider();
        _mediatrMediator = _mediatrProvider.GetRequiredService<IMediator>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _priorityFlowProvider?.Dispose();
        _mediatrProvider?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task<string> Baseline_SimpleQuery()
    {
        var query = new SimpleQuery("test-data");
        return await _mediatrMediator.Send(query);
    }

    [Benchmark]
    public async Task<string> PriorityFlow_SimpleQuery()
    {
        var query = new SimpleQuery("test-data");
        return await _priorityFlowMediator.Send(query);
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    public async Task Baseline_BatchQueries(int count)
    {
        var tasks = Enumerable.Range(0, count)
            .Select(i => _mediatrMediator.Send(new SimpleQuery($"data-{i}")));
        
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    public async Task PriorityFlow_BatchQueries(int count)
    {
        var tasks = Enumerable.Range(0, count)
            .Select(i => _priorityFlowMediator.Send(new SimpleQuery($"data-{i}")));
        
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task Baseline_SimpleNotification()
    {
        var notification = new SimpleNotification("test-event");
        await _mediatrMediator.Publish(notification);
    }

    [Benchmark]
    public async Task PriorityFlow_SimpleNotification()
    {
        var notification = new SimpleNotification("test-event");
        await _priorityFlowMediator.Publish(notification);
    }

    [Benchmark]
    [Arguments(5)]
    [Arguments(20)]
    public async Task Baseline_BatchNotifications(int count)
    {
        var tasks = Enumerable.Range(0, count)
            .Select(i => _mediatrMediator.Publish(new SimpleNotification($"event-{i}")));
        
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    [Arguments(5)]
    [Arguments(20)]
    public async Task PriorityFlow_BatchNotifications(int count)
    {
        var tasks = Enumerable.Range(0, count)
            .Select(i => _priorityFlowMediator.Publish(new SimpleNotification($"event-{i}")));
        
        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task PriorityFlow_PriorityDetection()
    {
        var commands = new IRequest<string>[]
        {
            new PaymentQuery("payment-1"),
            new ReportQuery("report-1"),
            new SecurityQuery("security-1"),
            new AnalyticsQuery("analytics-1"),
            new BusinessQuery("business-1")
        };

        var tasks = commands.Select(cmd => _priorityFlowMediator.Send(cmd));
        await Task.WhenAll(tasks);
    }
}

#region Simple Test Types

// Shared types
public record SimpleQuery(string Data) : IRequest<string>;
public record SimpleNotification(string EventData) : INotification;

// Priority test queries
public record PaymentQuery(string Id) : IRequest<string>;
public record ReportQuery(string Id) : IRequest<string>;
public record SecurityQuery(string Id) : IRequest<string>;
public record AnalyticsQuery(string Id) : IRequest<string>;
public record BusinessQuery(string Id) : IRequest<string>;

// PriorityFlow Handlers
public class SimpleQueryHandler : IRequestHandler<SimpleQuery, string>
{
    public Task<string> Handle(SimpleQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"PriorityFlow: {request.Data}");
    }
}

public class SimpleNotificationHandler : INotificationHandler<SimpleNotification>
{
    public Task Handle(SimpleNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public class PaymentQueryHandler : IRequestHandler<PaymentQuery, string>
{
    public Task<string> Handle(PaymentQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Payment: {request.Id}");
    }
}

public class ReportQueryHandler : IRequestHandler<ReportQuery, string>
{
    public Task<string> Handle(ReportQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Report: {request.Id}");
    }
}

public class SecurityQueryHandler : IRequestHandler<SecurityQuery, string>
{
    public Task<string> Handle(SecurityQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Security: {request.Id}");
    }
}

public class AnalyticsQueryHandler : IRequestHandler<AnalyticsQuery, string>
{
    public Task<string> Handle(AnalyticsQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Analytics: {request.Id}");
    }
}

public class BusinessQueryHandler : IRequestHandler<BusinessQuery, string>
{
    public Task<string> Handle(BusinessQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Business: {request.Id}");
    }
}


#endregion