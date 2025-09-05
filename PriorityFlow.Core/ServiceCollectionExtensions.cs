// PriorityFlow.Core - Dependency Injection Extensions

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PriorityFlow.Queuing;
using PriorityFlow.Behaviors;
using PriorityFlow.Observability;

namespace PriorityFlow.Extensions
{
    /// <summary>
    /// Service collection extensions for PriorityFlow registration
    /// Drop-in replacement for MediatR registration
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add PriorityFlow with default configuration
        /// Usage: services.AddPriorityFlow(Assembly.GetExecutingAssembly());
        /// </summary>
        public static IServiceCollection AddPriorityFlow(this IServiceCollection services, Assembly assembly)
        {
            return AddPriorityFlow(services, assembly, null);
        }

        /// <summary>
        /// Add PriorityFlow with custom configuration
        /// </summary>
        public static IServiceCollection AddPriorityFlow(this IServiceCollection services, Assembly assembly, 
            Action<PriorityFlowConfigurationBuilder>? configureOptions)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly), "Assembly is required for handler registration");

            // Build configuration
            var configBuilder = new PriorityFlowConfigurationBuilder();
            configureOptions?.Invoke(configBuilder);
            var configuration = configBuilder.Build();

            // Register configuration
            services.AddSingleton(configuration);

            // Register all handlers from assembly
            RegisterHandlers(services, assembly);

            // Register core services based on configuration
            if (configuration.EnableQueuedProcessing)
            {
                // Register queuing services
                services.AddSingleton<IPriorityQueueChannel>(provider => 
                    new PriorityQueueChannel(configuration.MaxQueueCapacity, provider.GetService<ILogger<PriorityQueueChannel>>()));
                services.AddHostedService<PriorityRequestWorker>();
                
                // Use queued mediator
                services.AddScoped<IMediator, QueuedPriorityMediator>();
                services.AddScoped<ISender, QueuedPriorityMediator>();
                services.AddScoped<IPublisher, QueuedPriorityMediator>();
            }
            else
            {
                // Use enhanced mediator for immediate processing with behaviors
                services.AddScoped<IMediator, EnhancedPriorityMediator>();
                services.AddScoped<ISender, EnhancedPriorityMediator>();
                services.AddScoped<IPublisher, EnhancedPriorityMediator>();
            }

            // Register observability services
            if (configuration.EnableMetrics)
            {
                services.AddSingleton<IPriorityFlowMetrics, PriorityFlowMetrics>();
            }

            // Register behaviors based on configuration
            RegisterBehaviors(services, configuration);

            // Logging should be registered by host application

            return services;
        }

        /// <summary>
        /// Add PriorityFlow using a type from the target assembly
        /// </summary>
        public static IServiceCollection AddPriorityFlow<TAssemblyMarker>(this IServiceCollection services,
            Action<PriorityFlowConfigurationBuilder>? configureOptions = null)
        {
            return AddPriorityFlow(services, typeof(TAssemblyMarker).Assembly, configureOptions);
        }

        /// <summary>
        /// Add PriorityFlow using calling assembly
        /// </summary>
        public static IServiceCollection AddPriorityFlow(this IServiceCollection services,
            Action<PriorityFlowConfigurationBuilder>? configureOptions = null)
        {
            return AddPriorityFlow(services, Assembly.GetCallingAssembly(), configureOptions);
        }

        /// <summary>
        /// Add PriorityFlow for multiple assemblies
        /// </summary>
        public static IServiceCollection AddPriorityFlow(this IServiceCollection services, 
            Assembly[] assemblies, Action<PriorityFlowConfigurationBuilder>? configureOptions = null)
        {
            if (assemblies == null || assemblies.Length == 0)
                throw new ArgumentException("At least one assembly is required", nameof(assemblies));

            // Build configuration
            var configBuilder = new PriorityFlowConfigurationBuilder();
            configureOptions?.Invoke(configBuilder);
            var configuration = configBuilder.Build();

            // Register configuration
            services.AddSingleton(configuration);

            // Register all handlers from all assemblies
            foreach (var assembly in assemblies)
            {
                RegisterHandlers(services, assembly);
            }

            // Register core services based on configuration
            if (configuration.EnableQueuedProcessing)
            {
                // Register queuing services
                services.AddSingleton<IPriorityQueueChannel>(provider => 
                    new PriorityQueueChannel(configuration.MaxQueueCapacity, provider.GetService<ILogger<PriorityQueueChannel>>()));
                services.AddHostedService<PriorityRequestWorker>();
                
                // Use queued mediator
                services.AddScoped<IMediator, QueuedPriorityMediator>();
                services.AddScoped<ISender, QueuedPriorityMediator>();
                services.AddScoped<IPublisher, QueuedPriorityMediator>();
            }
            else
            {
                // Use enhanced mediator for immediate processing with behaviors
                services.AddScoped<IMediator, EnhancedPriorityMediator>();
                services.AddScoped<ISender, EnhancedPriorityMediator>();
                services.AddScoped<IPublisher, EnhancedPriorityMediator>();
            }

            // Register observability services
            if (configuration.EnableMetrics)
            {
                services.AddSingleton<IPriorityFlowMetrics, PriorityFlowMetrics>();
            }

            // Register behaviors based on configuration
            RegisterBehaviors(services, configuration);

            // Logging should be registered by host application

            return services;
        }

        /// <summary>
        /// Developer-friendly setup for quick prototyping
        /// </summary>
        public static IServiceCollection AddPriorityFlowForDevelopment(this IServiceCollection services, Assembly assembly)
        {
            return AddPriorityFlow(services, assembly, config =>
            {
                config.WithDebugLogging(true)
                      .WithPerformanceMonitoring(perf =>
                      {
                          perf.EnableAlerts(500); // Alert on commands > 500ms in dev
                      })
                      .WithAutoDetection(true)
                      .WithConventions(conv =>
                      {
                          conv.HighPriority("Critical", "Urgent", "Priority", "Important")
                              .LowPriority("Background", "Cleanup", "Maintenance");
                      });
            });
        }

        /// <summary>
        /// Production-ready setup with minimal logging
        /// </summary>
        public static IServiceCollection AddPriorityFlowForProduction(this IServiceCollection services, Assembly assembly)
        {
            return AddPriorityFlow(services, assembly, config =>
            {
                config.WithDebugLogging(false)
                      .WithPerformanceMonitoring(perf =>
                      {
                          perf.EnableAlerts(2000); // Alert on commands > 2s in prod
                      })
                      .WithAutoDetection(true);
            });
        }

        /// <summary>
        /// Register all handlers from assembly using reflection
        /// </summary>
        private static void RegisterHandlers(IServiceCollection services, Assembly assembly)
        {
            // Register request handlers (IRequest)
            foreach (var type in assembly.GetTypes())
            {
                var interfaces = type.GetInterfaces();
                
                foreach (var interfaceType in interfaces)
                {
                    // Register IRequestHandler<TRequest>
                    if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IRequestHandler<>))
                    {
                        services.AddTransient(interfaceType, type);
                    }
                    // Register IRequestHandler<TRequest, TResponse>
                    else if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                    {
                        services.AddTransient(interfaceType, type);
                    }
                    // Register INotificationHandler<TNotification>
                    else if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                    {
                        services.AddTransient(interfaceType, type);
                    }
                    // Register IStreamRequestHandler<TRequest, TResponse>
                    else if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IStreamRequestHandler<,>))
                    {
                        services.AddTransient(interfaceType, type);
                    }
                }
            }
        }

        /// <summary>
        /// Register pipeline behaviors based on configuration
        /// </summary>
        private static void RegisterBehaviors(IServiceCollection services, PriorityFlowConfiguration configuration)
        {
            // Register validation behavior if enabled
            if (configuration.EnableValidation)
            {
                services.AddTransient(typeof(IPipelineBehavior<>), typeof(ValidationBehavior<>));
                services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            }

            // Register performance monitoring behavior if enabled
            if (configuration.EnablePerformanceTracking)
            {
                services.AddTransient(typeof(IPipelineBehavior<>), typeof(PerformanceMonitoringBehavior<>));
                services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceMonitoringBehavior<,>));
            }
        }

        /// <summary>
        /// Add validation behavior to the pipeline
        /// </summary>
        public static IServiceCollection AddValidationBehavior(this IServiceCollection services)
        {
            services.AddTransient(typeof(IPipelineBehavior<>), typeof(ValidationBehavior<>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            return services;
        }

        /// <summary>
        /// Add performance monitoring behavior to the pipeline
        /// </summary>
        public static IServiceCollection AddPerformanceMonitoringBehavior(this IServiceCollection services)
        {
            services.AddTransient(typeof(IPipelineBehavior<>), typeof(PerformanceMonitoringBehavior<>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceMonitoringBehavior<,>));
            return services;
        }

        /// <summary>
        /// Add PriorityFlow health check for monitoring integration
        /// </summary>
        public static IServiceCollection AddPriorityFlowHealthCheck(this IServiceCollection services, string name = "priorityflow")
        {
            return services.AddHealthChecks()
                          .AddCheck<PriorityQueueHealthCheck>(name)
                          .Services;
        }

        /// <summary>
        /// Add PriorityFlow health check with custom configuration
        /// </summary>
        public static IServiceCollection AddPriorityFlowHealthCheck(
            this IServiceCollection services, 
            string name,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null,
            TimeSpan? timeout = null)
        {
            return services.AddHealthChecks()
                          .AddCheck<PriorityQueueHealthCheck>(
                              name, 
                              failureStatus, 
                              tags, 
                              timeout)
                          .Services;
        }

        /// <summary>
        /// Add PriorityFlow observability services (metrics and health checks)
        /// </summary>
        public static IServiceCollection AddPriorityFlowObservability(this IServiceCollection services)
        {
            services.AddSingleton<IPriorityFlowMetrics, PriorityFlowMetrics>();
            services.AddPriorityFlowHealthCheck();
            return services;
        }
    }

    /// <summary>
    /// Additional extension methods for advanced scenarios
    /// </summary>
    public static class PriorityFlowAdvancedExtensions
    {
        /// <summary>
        /// Add PriorityFlow with custom mediator implementation
        /// </summary>
        public static IServiceCollection AddPriorityFlowWithCustomMediator<TMediator>(
            this IServiceCollection services, Assembly assembly) 
            where TMediator : class, IMediator
        {
            // Register handlers
            ServiceCollectionExtensions.AddPriorityFlow(services, assembly);
            
            // Replace mediator with custom implementation (simplified approach)
            services.AddScoped<IMediator, TMediator>();
            services.AddScoped<ISender, TMediator>();
            services.AddScoped<IPublisher, TMediator>();

            return services;
        }


    }

}