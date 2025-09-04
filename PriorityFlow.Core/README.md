# 🚀 PriorityFlow - Priority-Aware MediatR Alternative

**Drop-in replacement for MediatR with intelligent priority-based command execution.**

## ⚡ Quick Start (30 seconds)

```csharp
// 1. Install Package
// Install-Package PriorityFlow.Core

// 2. Replace MediatR registration
// OLD: services.AddMediatR(Assembly.GetExecutingAssembly());
services.AddPriorityFlow(Assembly.GetExecutingAssembly());

// 3. Use exactly like MediatR - priorities are auto-detected!
public record PaymentCommand(decimal Amount) : IRequest<bool>;     // 🔥 Auto: High Priority
public record SendEmailCommand(string Email) : IRequest;          // ⚡ Auto: Normal Priority  
public record GenerateReportCommand(string Type) : IRequest<string>; // 📊 Auto: Low Priority

// 4. Override with attributes when needed
[Priority(Priority.High)]
public record CustomCriticalCommand() : IRequest;
```

## 🎯 Key Features

### ✅ **Zero Configuration**
- Auto-detects priorities from command names and patterns
- Works with existing MediatR handlers without changes
- Intelligent naming conventions (PaymentCommand = High, ReportCommand = Low)

### ✅ **Developer Experience First**
- Fluent configuration API
- Rich debug logging with execution order
- Performance monitoring with slow command alerts
- Helpful error messages with setup guidance

### ✅ **Smart Priority System**
```csharp
// Built-in conventions automatically detect:
PaymentCommand     → High Priority   🔥
SecurityCommand    → High Priority   🔥
ReportCommand      → Low Priority    📊
AnalyticsCommand   → Low Priority    📊
EmailCommand       → Low Priority    📧
```

### ✅ **Performance Focused**
- Minimal overhead compared to MediatR
- Direct execution when no queuing needed
- Priority queue only when necessary
- Built-in performance metrics

## 📖 Usage Examples

### Basic Usage
```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Simple setup - just like MediatR
        services.AddPriorityFlow(Assembly.GetExecutingAssembly());
    }
}

// Your existing handlers work without changes
public class PaymentCommandHandler : IRequestHandler<PaymentCommand, bool>
{
    public async Task<bool> Handle(PaymentCommand request, CancellationToken cancellationToken)
    {
        // Your payment logic here
        return true;
    }
}
```

### Advanced Configuration
```csharp
services.AddPriorityFlow(Assembly.GetExecutingAssembly(), config =>
{
    config.WithDebugLogging(true)
          .WithPerformanceMonitoring(perf =>
          {
              perf.EnableAlerts(1000); // Alert on commands > 1s
          })
          .WithConventions(conv =>
          {
              conv.HighPriority("Critical", "Urgent", "Emergency")
                  .LowPriority("Background", "Cleanup", "Archive");
          });
});
```

### Development vs Production
```csharp
// Development - verbose logging and alerts
services.AddPriorityFlowForDevelopment(Assembly.GetExecutingAssembly());

// Production - minimal logging, performance focused  
services.AddPriorityFlowForProduction(Assembly.GetExecutingAssembly());
```

## 🔧 Migration from MediatR

**Step 1:** Replace package reference
```xml
<!-- Remove -->
<PackageReference Include="MediatR" Version="12.0.0" />
<PackageReference Include="MediatR.Extensions.Microsoft.DependencyInjection" Version="11.0.0" />

<!-- Add -->
<PackageReference Include="PriorityFlow.Core" Version="1.0.0" />
```

**Step 2:** Update registration
```csharp
// OLD
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// NEW  
services.AddPriorityFlow(Assembly.GetExecutingAssembly());
```

**Step 3:** That's it! Your existing code works unchanged.

## 📊 Priority Detection Logic

1. **Explicit Attribute:** `[Priority(Priority.High)]` - Always takes precedence
2. **Naming Conventions:** Built-in smart detection from command names
3. **Namespace Hints:** Commands in `.Security.` or `.Payment.` namespaces
4. **Custom Rules:** Configure your own patterns
5. **Default:** Normal priority if no patterns match

## 🚀 Performance

- **Direct Execution:** No overhead when no queue needed (90% of cases)
- **Smart Queueing:** Priority queue only when multiple commands overlap  
- **Metrics:** Built-in performance tracking and slow command detection
- **Memory Efficient:** Minimal allocations, smart cleanup

## 🛠️ Advanced Features

### Custom Priority Rules
```csharp
PriorityConventions.AddCustomConvention("Invoice", Priority.High);
PriorityConventions.AddCustomConventions(new Dictionary<string, Priority>
{
    { "Billing", Priority.High },
    { "Archive", Priority.Low }
});
```

### Performance Monitoring  
```csharp
var mediator = serviceProvider.GetService<PriorityMediator>();
var metrics = mediator.GetPerformanceMetrics();

foreach (var metric in metrics)
{
    Console.WriteLine($"{metric.Key}: {metric.Value.AverageExecutionTime}ms avg");
}
```

## 🤝 Why Choose PriorityFlow?

| Feature | MediatR | PriorityFlow |
|---------|---------|--------------|
| **Learning Curve** | Medium | Low |
| **Setup Time** | 15+ minutes | 30 seconds |
| **Priority Support** | ❌ | ✅ Auto-detected |
| **Performance Monitoring** | ❌ | ✅ Built-in |  
| **Developer Experience** | Good | Excellent |
| **Migration Effort** | N/A | < 5 minutes |

## 📝 License

MIT License - see LICENSE file for details.

## 🤝 Contributing

We welcome contributions! Please see CONTRIBUTING.md for guidelines.

---

**Made with ❤️ for developers who want intelligent command execution without complexity.**