// Extensions/PriorityFlow/ServiceCollectionExtensions.cs
// Simplified DI Extensions for Developer Experience

using System;
using System.Linq;
using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace JetRentalOrchestration.Extensions.PriorityFlow
{
    /// <summary>
    /// Simple extension methods for PriorityMediatR setup
    /// Focus: Easy configuration, clear error messages, minimal complexity
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add PriorityMediatR - simple drop-in replacement for MediatR
        /// 
        /// Usage:
        /// OLD: services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        /// NEW: services.AddPriorityMediatR(Assembly.GetExecutingAssembly());
        /// 
        /// Result: Same IMediator interface with priority support!
        /// </summary>
        public static IServiceCollection AddPriorityMediatR(this IServiceCollection services, Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly), "Assembly is required for handler registration");
            }

            // 1. Register standard MediatR services (handlers, behaviors, etc.)
            services.AddMediatR(assembly);

            // 2. Register the original Mediator as a concrete service (for PriorityMediator to use)
            services.AddScoped<Mediator>();

            // 3. Replace IMediator with our PriorityMediator
            var mediatorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));
            if (mediatorDescriptor != null)
            {
                services.Remove(mediatorDescriptor);
            }

            services.AddScoped<IMediator, PriorityMediator>();

            return services;
        }

        /// <summary>
        /// Add PriorityMediatR using a type from the target assembly
        /// </summary>
        public static IServiceCollection AddPriorityMediatR<TAssemblyMarker>(this IServiceCollection services)
        {
            return AddPriorityMediatR(services, typeof(TAssemblyMarker).Assembly);
        }

        /// <summary>
        /// Add PriorityMediatR using calling assembly (when called from your startup)
        /// </summary>
        public static IServiceCollection AddPriorityMediatR(this IServiceCollection services)
        {
            return AddPriorityMediatR(services, Assembly.GetCallingAssembly());
        }

        /// <summary>
        /// Add PriorityMediatR for multiple assemblies
        /// </summary>
        public static IServiceCollection AddPriorityMediatR(this IServiceCollection services, params Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
            {
                throw new ArgumentException("At least one assembly is required", nameof(assemblies));
            }

            // Register MediatR for all assemblies
            services.AddMediatR(assemblies);

            // Register concrete Mediator and replace interface
            services.AddScoped<Mediator>();
            
            var mediatorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMediator));
            if (mediatorDescriptor != null)
            {
                services.Remove(mediatorDescriptor);
            }

            services.AddScoped<IMediator, PriorityMediator>();

            return services;
        }

        /// <summary>
        /// Development mode setup with detailed logging
        /// </summary>
        public static IServiceCollection AddPriorityMediatRForDevelopment(this IServiceCollection services, Assembly assembly)
        {
            // Add standard PriorityMediatR
            AddPriorityMediatR(services, assembly);

            // Add basic logging configuration for development
            services.AddLogging();

            return services;
        }
    }
}

/*
🎯 SIMPLIFIED APPROACH EXPLANATION:

Why this approach is better for your project:

1. SIMPLE SETUP:
   services.AddPriorityMediatR(Assembly.GetExecutingAssembly());
   That's it! No complex configuration needed.

2. ZERO BREAKING CHANGES:
   Existing code: await _mediator.Send(command);
   Still works exactly the same, just with priority support!

3. CLEAR ERROR MESSAGES:
   If setup fails, you get helpful exceptions with guidance.

4. EASY DEBUGGING:
   - No background threads
   - No complex orchestration
   - Single-threaded, predictable behavior

5. GRADUAL ADOPTION:
   - Start without any [Priority] attributes → everything works as normal MediatR
   - Add [Priority(Priority.High)] to critical commands → they get prioritized
   - Add more priorities gradually as needed

This gives you all the benefits of priority handling without the complexity!
*/