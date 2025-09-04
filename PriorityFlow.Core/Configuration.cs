// PriorityFlow.Core - Fluent Configuration API

using System;
using System.Collections.Generic;

namespace PriorityFlow
{
    /// <summary>
    /// Configuration options for PriorityFlow
    /// </summary>
    public class PriorityFlowConfiguration
    {
        public bool EnableDebugLogging { get; set; } = true;
        public bool EnablePerformanceTracking { get; set; } = true;
        public bool EnablePerformanceAlerts { get; set; } = true;
        public long SlowCommandThresholdMs { get; set; } = 1000;
        public bool AutoDetectPriorities { get; set; } = true;
        public bool LearnFromUsage { get; set; } = false;

        internal Dictionary<string, Priority> CustomConventions { get; set; } = new();
    }

    /// <summary>
    /// Fluent configuration builder for PriorityFlow
    /// </summary>
    public class PriorityFlowConfigurationBuilder
    {
        private readonly PriorityFlowConfiguration _configuration = new();

        /// <summary>
        /// Enable or disable debug logging
        /// </summary>
        public PriorityFlowConfigurationBuilder WithDebugLogging(bool enabled = true)
        {
            _configuration.EnableDebugLogging = enabled;
            return this;
        }

        /// <summary>
        /// Configure performance monitoring
        /// </summary>
        public PriorityFlowConfigurationBuilder WithPerformanceMonitoring(Action<PerformanceConfigurationBuilder>? configure = null)
        {
            _configuration.EnablePerformanceTracking = true;
            
            if (configure != null)
            {
                var builder = new PerformanceConfigurationBuilder(_configuration);
                configure(builder);
            }

            return this;
        }

        /// <summary>
        /// Configure priority conventions
        /// </summary>
        public PriorityFlowConfigurationBuilder WithConventions(Action<ConventionConfigurationBuilder> configure)
        {
            var builder = new ConventionConfigurationBuilder(_configuration);
            configure(builder);
            return this;
        }

        /// <summary>
        /// Enable auto-detection of priorities based on naming patterns
        /// </summary>
        public PriorityFlowConfigurationBuilder WithAutoDetection(bool enabled = true)
        {
            _configuration.AutoDetectPriorities = enabled;
            return this;
        }

        /// <summary>
        /// Enable learning from usage patterns (experimental)
        /// </summary>
        public PriorityFlowConfigurationBuilder WithUsageLearning(bool enabled = true)
        {
            _configuration.LearnFromUsage = enabled;
            return this;
        }

        /// <summary>
        /// Build the configuration
        /// </summary>
        internal PriorityFlowConfiguration Build()
        {
            // Apply custom conventions to the static convention system
            foreach (var convention in _configuration.CustomConventions)
            {
                PriorityConventions.AddCustomConvention(convention.Key, convention.Value);
            }

            return _configuration;
        }
    }

    /// <summary>
    /// Performance configuration builder
    /// </summary>
    public class PerformanceConfigurationBuilder
    {
        private readonly PriorityFlowConfiguration _configuration;

        internal PerformanceConfigurationBuilder(PriorityFlowConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Enable performance alerts when commands are slow
        /// </summary>
        public PerformanceConfigurationBuilder EnableAlerts(long thresholdMs = 1000)
        {
            _configuration.EnablePerformanceAlerts = true;
            _configuration.SlowCommandThresholdMs = thresholdMs;
            return this;
        }

        /// <summary>
        /// Disable performance alerts
        /// </summary>
        public PerformanceConfigurationBuilder DisableAlerts()
        {
            _configuration.EnablePerformanceAlerts = false;
            return this;
        }

        /// <summary>
        /// Track all command executions
        /// </summary>
        public PerformanceConfigurationBuilder TrackAllCommands()
        {
            _configuration.EnablePerformanceTracking = true;
            return this;
        }
    }

    /// <summary>
    /// Convention configuration builder
    /// </summary>
    public class ConventionConfigurationBuilder
    {
        private readonly PriorityFlowConfiguration _configuration;

        internal ConventionConfigurationBuilder(PriorityFlowConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Add keywords that should be treated as High priority
        /// </summary>
        public ConventionConfigurationBuilder HighPriority(params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                _configuration.CustomConventions[keyword.ToLower()] = Priority.High;
            }
            return this;
        }

        /// <summary>
        /// Add keywords that should be treated as Normal priority
        /// </summary>
        public ConventionConfigurationBuilder NormalPriority(params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                _configuration.CustomConventions[keyword.ToLower()] = Priority.Normal;
            }
            return this;
        }

        /// <summary>
        /// Add keywords that should be treated as Low priority
        /// </summary>
        public ConventionConfigurationBuilder LowPriority(params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                _configuration.CustomConventions[keyword.ToLower()] = Priority.Low;
            }
            return this;
        }

        /// <summary>
        /// Add custom priority mapping
        /// </summary>
        public ConventionConfigurationBuilder CustomPriority(string keyword, Priority priority)
        {
            _configuration.CustomConventions[keyword.ToLower()] = priority;
            return this;
        }

        /// <summary>
        /// Clear all custom conventions
        /// </summary>
        public ConventionConfigurationBuilder ClearCustomConventions()
        {
            _configuration.CustomConventions.Clear();
            return this;
        }
    }

    /// <summary>
    /// Debug configuration builder for development scenarios
    /// </summary>
    public class DebugConfigurationBuilder
    {
        private readonly PriorityFlowConfiguration _configuration;

        internal DebugConfigurationBuilder(PriorityFlowConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Enable console logging for command execution
        /// </summary>
        public DebugConfigurationBuilder EnableConsoleLogging()
        {
            _configuration.EnableDebugLogging = true;
            return this;
        }

        /// <summary>
        /// Show execution order in logs
        /// </summary>
        public DebugConfigurationBuilder ShowExecutionOrder()
        {
            // This would be implemented with additional logging logic
            return this;
        }

        /// <summary>
        /// Track performance metrics
        /// </summary>
        public DebugConfigurationBuilder TrackPerformance()
        {
            _configuration.EnablePerformanceTracking = true;
            return this;
        }
    }
}