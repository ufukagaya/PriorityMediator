// PriorityFlow.Core - Dependency Injection Extensions

using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

            // Register core services
            services.AddScoped<IMediator, SimplePriorityMediator>();
            services.AddScoped<ISender, SimplePriorityMediator>();
            services.AddScoped<IPublisher, SimplePriorityMediator>();

            // Add logging if not already registered
            services.AddLogging();

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

            // Register core services
            services.AddScoped<IMediator, SimplePriorityMediator>();
            services.AddScoped<ISender, SimplePriorityMediator>();
            services.AddScoped<IPublisher, SimplePriorityMediator>();

            // Add logging
            services.AddLogging();

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

        /// <summary>
        /// Add pipeline behaviors (similar to MediatR)
        /// </summary>
        public static IServiceCollection AddPriorityFlowBehavior<TBehavior>(this IServiceCollection services)
            where TBehavior : class
        {
            // This would be implemented if pipeline behaviors are needed
            // For now, it's a placeholder for future enhancement
            services.AddTransient<TBehavior>();
            return services;
        }
    }
}