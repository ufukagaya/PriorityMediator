// Extensions/PriorityFlow/Priority.cs
// Simplified Priority System - Developer Experience Focused

using System;
using System.Collections.Generic;
using System.ComponentModel;
using MediatR;

namespace JetRentalOrchestration.Extensions.PriorityFlow
{
    /// <summary>
    /// Simple 3-level priority system for developer clarity
    /// Higher number = Higher priority
    /// </summary>
    public enum Priority
    {
        [Description("Background operations, reports, analytics")]
        Low = 1,

        [Description("Standard business operations")]
        Normal = 2,

        [Description("Time-critical operations like payments, security")]
        High = 3
    }

    /// <summary>
    /// Safe convention-based priority detection
    /// Only obvious cases to avoid naming conflicts
    /// </summary>
    public static class PriorityConventions
    {
        private static readonly Dictionary<string, Priority> _safeConventions = new()
        {
            // Very obvious high priority cases
            { "payment", Priority.High },
            { "security", Priority.High },
            { "auth", Priority.High },
            
            // Very obvious low priority cases  
            { "report", Priority.Low },
            { "analytics", Priority.Low },
            { "log", Priority.Low }
        };

        /// <summary>
        /// Get priority based on command name conventions
        /// Uses exact prefix matching to avoid conflicts
        /// </summary>
        public static Priority GetConventionBasedPriority(Type commandType)
        {
            var commandName = commandType.Name.ToLower();
            
            // Only check for exact prefix matches to avoid conflicts
            foreach (var convention in _safeConventions)
            {
                if (commandName.StartsWith(convention.Key))
                {
                    return convention.Value;
                }
            }
            
            return Priority.Normal; // Safe default
        }
    }

    /// <summary>
    /// Developer-friendly priority attribute
    /// Usage: [Priority(Priority.High)]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PriorityAttribute : Attribute
    {
        public Priority Priority { get; }

        public PriorityAttribute(Priority priority)
        {
            Priority = priority;
        }
    }

    /// <summary>
    /// Simple command wrapper for priority queue
    /// No complex orchestration, just basic priority handling
    /// </summary>
    internal class PriorityCommandItem
    {
        public object Command { get; set; } = null!;
        public Priority Priority { get; set; }
        public DateTime QueuedAt { get; set; }
        public string CommandType => Command.GetType().Name;
    }
}