// BankingSystem/Models/BankingModels.cs
using System;
using System.Collections.Generic;

namespace JetRentalOrchestration.BankingSystem.Models
{
    public enum TransactionType
    {
        Transfer,
        Withdrawal,
        Deposit,
        WireTransfer,
        BillPayment,
        BalanceInquiry
    }

    public enum TransactionStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Blocked,
        RequiresApproval
    }

    public enum FraudRiskLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class Account
    {
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsFrozen { get; set; } = false;
        public DateTime LastActivity { get; set; }
        public List<TransactionRecord> TransactionHistory { get; set; } = new();
    }

    public class TransactionRecord
    {
        public Guid TransactionId { get; set; }
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public Guid FromAccountId { get; set; }
        public Guid ToAccountId { get; set; }
        public TransactionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Description { get; set; } = string.Empty;
        public FraudRiskLevel FraudRisk { get; set; }
        public List<string> ProcessingSteps { get; set; } = new();
        public TimeSpan ProcessingTime { get; set; }
    }

    public class FraudAlert
    {
        public Guid AlertId { get; set; }
        public Guid AccountId { get; set; }
        public Guid TransactionId { get; set; }
        public FraudRiskLevel RiskLevel { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public bool IsResolved { get; set; } = false;
    }

    public class ProcessingMetrics
    {
        public int TotalTransactions { get; set; }
        public int CompletedTransactions { get; set; }
        public int FailedTransactions { get; set; }
        public int BlockedTransactions { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public TimeSpan TotalProcessingTime { get; set; }
        public Dictionary<string, int> CommandCounts { get; set; } = new();
        public Dictionary<string, TimeSpan> CommandTimes { get; set; } = new();
    }
}

