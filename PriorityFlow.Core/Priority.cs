// PriorityFlow.Core - Priority System with Smart Conventions

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace PriorityFlow
{
    /// <summary>
    /// Simple 3-level priority system optimized for developer experience
    /// Higher number = Higher priority
    /// </summary>
    public enum Priority
    {
        [Description("Background operations, analytics, reports")]
        Low = 1,

        [Description("Standard business operations")]
        Normal = 2,

        [Description("Critical operations like payments, security")]
        High = 3
    }

    /// <summary>
    /// Attribute for explicitly setting command priority
    /// Usage: [Priority(Priority.High)]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class PriorityAttribute : Attribute
    {
        public Priority Priority { get; }

        public PriorityAttribute(Priority priority)
        {
            Priority = priority;
        }
    }

    /// <summary>
    /// Smart convention-based priority detection with learning capabilities
    /// </summary>
    public static class PriorityConventions
    {
        // Built-in safe conventions
        private static readonly Dictionary<string, Priority> _builtInConventions = new(StringComparer.OrdinalIgnoreCase)
        {
            // High Priority Operations
            { "payment", Priority.High },
            { "security", Priority.High },
            { "auth", Priority.High },
            { "critical", Priority.High },
            { "urgent", Priority.High },
            { "fraud", Priority.High },
            { "alert", Priority.High },

            // Low Priority Operations
            { "report", Priority.Low },
            { "analytics", Priority.Low },
            { "audit", Priority.Low },
            { "log", Priority.Low },
            { "notification", Priority.Low },
            { "email", Priority.Low },
            { "export", Priority.Low },
            { "backup", Priority.Low }
        };

        // Custom conventions configured by developer
        private static readonly ConcurrentDictionary<string, Priority> _customConventions = new(StringComparer.OrdinalIgnoreCase);

        // Usage tracking for learning (optional feature)
        private static readonly ConcurrentDictionary<string, int> _usageTracker = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Get priority for a command type using conventions
        /// </summary>
        /// <param name="commandType">Command type</param>
        /// <returns>Detected priority or Normal if no match</returns>
        public static Priority GetConventionBasedPriority(Type commandType)
        {
            var commandName = commandType.Name.ToLower();

            // 1. Check explicit attribute first
            var attribute = commandType.GetCustomAttributes(typeof(PriorityAttribute), true)
                                     .Cast<PriorityAttribute>()
                                     .FirstOrDefault();
            if (attribute != null)
            {
                TrackUsage(commandName, attribute.Priority);
                return attribute.Priority;
            }

            // 2. Check custom conventions
            foreach (var customConvention in _customConventions)
            {
                if (commandName.Contains(customConvention.Key))
                {
                    TrackUsage(commandName, customConvention.Value);
                    return customConvention.Value;
                }
            }

            // 3. Check built-in conventions
            foreach (var convention in _builtInConventions)
            {
                if (commandName.Contains(convention.Key))
                {
                    TrackUsage(commandName, convention.Value);
                    return convention.Value;
                }
            }

            // 4. Check namespace for additional hints
            var namespacePriority = GetNamespaceBasedPriority(commandType.Namespace);
            if (namespacePriority != Priority.Normal)
            {
                TrackUsage(commandName, namespacePriority);
                return namespacePriority;
            }

            // 5. Default to Normal
            TrackUsage(commandName, Priority.Normal);
            return Priority.Normal;
        }

        /// <summary>
        /// Add custom naming convention
        /// </summary>
        /// <param name="keyword">Keyword to match in command name</param>
        /// <param name="priority">Priority to assign</param>
        public static void AddCustomConvention(string keyword, Priority priority)
        {
            _customConventions.AddOrUpdate(keyword, priority, (key, oldValue) => priority);
        }

        /// <summary>
        /// Add multiple custom conventions at once
        /// </summary>
        /// <param name="conventions">Dictionary of keyword-priority mappings</param>
        public static void AddCustomConventions(Dictionary<string, Priority> conventions)
        {
            foreach (var convention in conventions)
            {
                AddCustomConvention(convention.Key, convention.Value);
            }
        }

        /// <summary>
        /// Get namespace-based priority hints
        /// </summary>
        private static Priority GetNamespaceBasedPriority(string? namespaceName)
        {
            if (string.IsNullOrEmpty(namespaceName))
                return Priority.Normal;

            var lowerNamespace = namespaceName.ToLower();

            if (lowerNamespace.Contains("payment") || lowerNamespace.Contains("security") || lowerNamespace.Contains("critical"))
                return Priority.High;

            if (lowerNamespace.Contains("report") || lowerNamespace.Contains("analytics") || lowerNamespace.Contains("audit"))
                return Priority.Low;

            return Priority.Normal;
        }

        /// <summary>
        /// Track command usage for learning (optional)
        /// </summary>
        private static void TrackUsage(string commandName, Priority priority)
        {
            var key = $"{commandName}:{priority}";
            _usageTracker.AddOrUpdate(key, 1, (k, v) => v + 1);
        }

        /// <summary>
        /// Get usage statistics (for debugging/monitoring)
        /// </summary>
        public static Dictionary<string, int> GetUsageStatistics()
        {
            return _usageTracker.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Clear custom conventions (useful for testing)
        /// </summary>
        public static void ClearCustomConventions()
        {
            _customConventions.Clear();
        }

        /// <summary>
        /// Get all active conventions (built-in + custom)
        /// </summary>
        public static Dictionary<string, Priority> GetAllConventions()
        {
            var result = new Dictionary<string, Priority>(_builtInConventions);
            foreach (var custom in _customConventions)
            {
                result[custom.Key] = custom.Value;
            }
            return result;
        }
    }

    /// <summary>
    /// Internal class for priority queue management
    /// </summary>
    internal class PriorityCommandItem
    {
        public object Command { get; set; } = null!;
        public Priority Priority { get; set; }
        public DateTime QueuedAt { get; set; }
        public string CommandType => Command.GetType().Name;
        public TimeSpan WaitTime => DateTime.UtcNow - QueuedAt;
    }
}