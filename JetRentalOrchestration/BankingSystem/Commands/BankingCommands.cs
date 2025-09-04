// BankingSystem/Commands/BankingCommands.cs
using System;
using MediatR;
using JetRentalOrchestration.BankingSystem.Models;
using JetRentalOrchestration.Extensions.PriorityFlow;

namespace JetRentalOrchestration.BankingSystem.Commands
{
    // ===================================================================
    // CRITICAL PRIORITY COMMANDS - Must execute immediately
    // ===================================================================

    [Priority(Priority.High)]
    public class DetectFraudCommand : IRequest<FraudAlert?>
    {
        public Guid TransactionId { get; set; }
        public Guid AccountId { get; set; }
        public decimal Amount { get; set; }
        public TransactionType Type { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    [Priority(Priority.High)]
    public class FreezeAccountCommand : IRequest<bool>
    {
        public Guid AccountId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public Guid AlertId { get; set; }
    }

    // ===================================================================
    // HIGH PRIORITY COMMANDS - Important financial operations
    // ===================================================================

    [Priority(Priority.High)]
    public class ProcessWireTransferCommand : IRequest<TransactionRecord>
    {
        public Guid FromAccountId { get; set; }
        public Guid ToAccountId { get; set; }
        public decimal Amount { get; set; }
        public string BeneficiaryName { get; set; } = string.Empty;
        public string SwiftCode { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
    }

    [Priority(Priority.High)]
    public class ProcessLargeWithdrawalCommand : IRequest<TransactionRecord>
    {
        public Guid AccountId { get; set; }
        public decimal Amount { get; set; }
        public string Purpose { get; set; } = string.Empty;
    }

    // ===================================================================
    // NORMAL PRIORITY COMMANDS - Regular banking operations
    // ===================================================================

    public class ProcessTransferCommand : IRequest<TransactionRecord>
    {
        public Guid FromAccountId { get; set; }
        public Guid ToAccountId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class PayBillCommand : IRequest<TransactionRecord>
    {
        public Guid AccountId { get; set; }
        public string BillerName { get; set; } = string.Empty;
        public string BillReference { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class ValidateAccountCommand : IRequest<bool>
    {
        public Guid AccountId { get; set; }
        public decimal RequiredBalance { get; set; }
    }

    // ===================================================================
    // LOW PRIORITY COMMANDS - Non-urgent operations
    // ===================================================================

    [Priority(Priority.Low)]
    public class GetBalanceCommand : IRequest<decimal>
    {
        public Guid AccountId { get; set; }
    }

    [Priority(Priority.Low)]
    public class GenerateStatementCommand : IRequest<string>
    {
        public Guid AccountId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    // ===================================================================
    // BACKGROUND PRIORITY COMMANDS - Can be delayed
    // ===================================================================

    [Priority(Priority.Low)]
    public class LogTransactionAnalyticsCommand : IRequest<bool>
    {
        public Guid TransactionId { get; set; }
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
    }

    [Priority(Priority.Low)]
    public class CreateAuditLogCommand : IRequest<bool>
    {
        public string Action { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public Guid AccountId { get; set; }
        public object Data { get; set; } = new();
    }

    [Priority(Priority.Low)]
    public class SendNotificationCommand : IRequest<bool>
    {
        public Guid AccountId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string NotificationType { get; set; } = string.Empty;
    }

    // ===================================================================
    // COMPLEX ORCHESTRATION COMMAND
    // ===================================================================

    public class ProcessComplexTransactionCommand : IRequest<TransactionRecord>
    {
        public Guid FromAccountId { get; set; }
        public Guid ToAccountId { get; set; }
        public decimal Amount { get; set; }
        public TransactionType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool RequiresFraudCheck { get; set; } = true;
        public bool IsHighValue { get; set; } = false;
    }
}

