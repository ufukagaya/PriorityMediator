# 🚀 PriorityFlow MediatR Extension

## Quick Start (2 minutes setup)

### 1. Replace your MediatR registration:
```csharp
// OLD:
services.AddMediatR(typeof(Program).Assembly);

// NEW:
services.AddPriorityMediatR(typeof(Program).Assembly);
```

### 2. That's it! 🎉
Your existing handlers work unchanged, but now with:
- ✅ Priority-based execution
- ✅ Background processing
- ✅ Rich monitoring
- ✅ Automatic orchestration

## Priority Conventions (Zero Attributes Needed!)

Commands are automatically prioritized by name:
- **`ProcessPaymentCommand`** → Critical (10)
- **`SendEmailCommand`** → Normal (5)  
- **`UpdateInventoryCommand`** → Low (3)
- **`LogAnalyticsCommand`** → Background (1)

## Advanced: Explicit Priorities

```csharp
[Priority(Priority.Critical)]
public class UrgentCommand : IRequest<bool> { }
```

## Advanced: Automatic Orchestration

```csharp
public class BookOrderCommand : IWorkflowCommand<OrderResult>
{
    public IEnumerable<IBaseRequest> GetFollowUpCommands(OrderResult result)
    {
        if (result.Success)
        {
            yield return new ProcessPaymentCommand();  // Critical
            yield return new SendEmailCommand();       // Normal
            yield return new UpdateInventoryCommand(); // Low
        }
    }
}
```

## Debugging

Enhanced logging shows:
- 📥 Command queueing with priorities
- ⚡ Processing times and wait times
- 🔗 Automatic follow-up command triggers
- 🎯 Execution flow with priorities

## Migration Checklist

- [ ] Replace `AddMediatR` with `AddPriorityMediatR`
- [ ] Test existing functionality (should work unchanged)
- [ ] Optional: Add `[Priority]` attributes for fine control
- [ ] Optional: Implement `IWorkflowCommand` for auto-orchestration
- [ ] Monitor logs for priority-based execution

## Support

All existing MediatR patterns supported:
- ✅ `IRequest<T>` and `IRequest`
- ✅ `IRequestHandler<T, R>` and `IRequestHandler<T>`
- ✅ `INotification` and `INotificationHandler<T>`
- ✅ Dependency injection
- ✅ Cancellation tokens
- ✅ Generic constraints

**Zero breaking changes guaranteed!** 🛡️

