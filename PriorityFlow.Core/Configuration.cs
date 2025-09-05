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

        // New properties for enhanced features
        
        /// <summary>
        /// Enable background queue processing instead of immediate execution
        /// </summary>
        public bool EnableQueuedProcessing { get; set; } = false;

        /// <summary>
        /// Maximum capacity for the priority queue (null for unbounded)
        /// </summary>
        public int? MaxQueueCapacity { get; set; } = null;

        /// <summary>
        /// Queue length threshold for health checks and alerts
        /// </summary>
        public long QueueLengthThreshold { get; set; } = 1000;

        /// <summary>
        /// Enable automatic request validation using FluentValidation
        /// </summary>
        public bool EnableValidation { get; set; } = false;

        /// <summary>
        /// Enable observability metrics collection
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Enable health checks for monitoring integration
        /// </summary>
        public bool EnableHealthChecks { get; set; } = false;

        /// <summary>
        /// Maximum error rate percentage before marking system as unhealthy
        /// </summary>
        public double MaxErrorRateThreshold { get; set; } = 5.0;

        /// <summary>
        /// Maximum average wait time in milliseconds before marking system as unhealthy
        /// </summary>
        public double MaxAverageWaitTimeThreshold { get; set; } = 5000.0;

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
        /// Enable background queue processing for priority-based execution
        /// </summary>
        public PriorityFlowConfigurationBuilder WithQueuing(bool enabled = true, int? maxCapacity = null)
        {
            _configuration.EnableQueuedProcessing = enabled;
            _configuration.MaxQueueCapacity = maxCapacity;
            return this;
        }

        /// <summary>
        /// Configure validation behavior
        /// </summary>
        public PriorityFlowConfigurationBuilder WithValidation(bool enabled = true)
        {
            _configuration.EnableValidation = enabled;
            return this;
        }

        /// <summary>
        /// Configure observability and metrics collection
        /// </summary>
        public PriorityFlowConfigurationBuilder WithObservability(Action<ObservabilityConfigurationBuilder>? configure = null)
        {
            _configuration.EnableMetrics = true;
            
            if (configure != null)
            {
                var builder = new ObservabilityConfigurationBuilder(_configuration);
                configure(builder);
            }

            return this;
        }

        /// <summary>
        /// Enable health checks for monitoring integration
        /// </summary>
        public PriorityFlowConfigurationBuilder WithHealthChecks(bool enabled = true, long queueLengthThreshold = 1000)
        {
            _configuration.EnableHealthChecks = enabled;
            _configuration.QueueLengthThreshold = queueLengthThreshold;
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
    /// Observability configuration builder for metrics and monitoring
    /// </summary>
    public class ObservabilityConfigurationBuilder
    {
        private readonly PriorityFlowConfiguration _configuration;

        internal ObservabilityConfigurationBuilder(PriorityFlowConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Enable metrics collection
        /// </summary>
        public ObservabilityConfigurationBuilder EnableMetrics(bool enabled = true)
        {
            _configuration.EnableMetrics = enabled;
            return this;
        }

        /// <summary>
        /// Enable health checks
        /// </summary>
        public ObservabilityConfigurationBuilder EnableHealthChecks(bool enabled = true)
        {
            _configuration.EnableHealthChecks = enabled;
            return this;
        }

        /// <summary>
        /// Configure health check thresholds
        /// </summary>
        public ObservabilityConfigurationBuilder WithHealthThresholds(
            long queueLengthThreshold = 1000,
            double maxErrorRate = 5.0,
            double maxAverageWaitTime = 5000.0)
        {
            _configuration.QueueLengthThreshold = queueLengthThreshold;
            _configuration.MaxErrorRateThreshold = maxErrorRate;
            _configuration.MaxAverageWaitTimeThreshold = maxAverageWaitTime;
            return this;
        }

        /// <summary>
        /// Enable all observability features
        /// </summary>
        public ObservabilityConfigurationBuilder EnableAll()
        {
            _configuration.EnableMetrics = true;
            _configuration.EnableHealthChecks = true;
            _configuration.EnablePerformanceTracking = true;
            _configuration.EnablePerformanceAlerts = true;
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