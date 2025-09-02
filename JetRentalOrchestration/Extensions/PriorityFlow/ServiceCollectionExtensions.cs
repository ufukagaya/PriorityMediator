// Extensions/PriorityFlow/ServiceCollectionExtensions.cs
// Bu dosyayı Visual Studio'da Extensions/PriorityFlow klasörüne oluştur

using System;
using System.Linq;
using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace JetRentalOrchestration.Extensions.PriorityFlow
{
    /// <summary>
    /// MediatR Extension Methods
    /// Bu class sayesinde services.AddPriorityMediatR() method'u kullanılabilir
    /// Extension method pattern ile Microsoft'un IServiceCollection'ına yeni method ekler
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// MediatR'ı priority + auto-orchestration ile enhance et
        /// 
        /// KULLANIM:
        /// services.AddMediatR(); // Eski yöntem
        /// ↓
        /// services.AddPriorityMediatR(); // Yeni enhanced yöntem
        /// 
        /// SONUÇ:
        /// - Aynı IMediator interface
        /// - Priority-based execution
        /// - Auto-orchestration capabilities
        /// - Zero breaking changes!
        /// </summary>
        public static IServiceCollection AddPriorityMediatR(this IServiceCollection services,
            params Assembly[] assemblies)
        {
            // Eğer assembly verilmemişse, calling assembly'i kullan
            if (assemblies == null || assemblies.Length == 0)
            {
                assemblies = new[] { Assembly.GetCallingAssembly() };
            }

            // 1. Normal MediatR services'lerini register et
            services.AddMediatR(assemblies);

            // 2. Store original IMediator registration before replacing
            var originalMediatorDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IMediator));
            if (originalMediatorDescriptor != null)
            {
                services.Remove(originalMediatorDescriptor);
                
                // Register original mediator as concrete type
                services.Add(ServiceDescriptor.Describe(
                    typeof(Mediator),
                    originalMediatorDescriptor.ImplementationFactory ?? 
                    (provider => ActivatorUtilities.CreateInstance(provider, originalMediatorDescriptor.ImplementationType!)),
                    originalMediatorDescriptor.Lifetime));
            }
            
            // 3. Replace IMediator with PriorityMediator
            services.AddSingleton<IMediator, PriorityMediator>();

            return services;
        }

        /// <summary>
        /// Service decoration extension method
        /// Existing service'i wrapper ile sar
        /// </summary>
        private static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
            where TDecorator : class, TService
            where TService : class
        {
            // Find existing service registration
            var existingService = services.LastOrDefault(s => s.ServiceType == typeof(TService));
            if (existingService == null)
            {
                throw new InvalidOperationException($"Service {typeof(TService).Name} not registered before decoration");
            }

            // Remove existing registration
            services.Remove(existingService);

            // Re-register original service with different key
            services.Add(ServiceDescriptor.Describe(
                typeof(TService),
                provider => CreateOriginalService(provider, existingService),
                existingService.Lifetime));

            // Register decorator
            services.Add(ServiceDescriptor.Describe(
                typeof(TService),
                provider => ActivatorUtilities.CreateInstance<TDecorator>(provider,
                    provider.GetServices<TService>().Skip(1).First()), // Get original, skip decorator
                ServiceLifetime.Singleton));

            return services;
        }

        private static object CreateOriginalService(IServiceProvider provider, ServiceDescriptor serviceDescriptor)
        {
            if (serviceDescriptor.ImplementationType != null)
            {
                return ActivatorUtilities.CreateInstance(provider, serviceDescriptor.ImplementationType);
            }

            if (serviceDescriptor.ImplementationFactory != null)
            {
                return serviceDescriptor.ImplementationFactory(provider);
            }

            if (serviceDescriptor.ImplementationInstance != null)
            {
                return serviceDescriptor.ImplementationInstance;
            }

            throw new InvalidOperationException("Invalid service descriptor");
        }

        /// <summary>
        /// Overload: Type-based assembly registration
        /// Belirli bir type'ın bulunduğu assembly'den command/handler'ları register et
        /// </summary>
        public static IServiceCollection AddPriorityMediatR(this IServiceCollection services, Type typeFromAssembly)
        {
            return AddPriorityMediatR(services, typeFromAssembly.Assembly);
        }

        /// <summary>
        /// Overload: Generic type-based assembly registration
        /// Generic type kullanarak assembly belirt
        /// </summary>
        public static IServiceCollection AddPriorityMediatR<T>(this IServiceCollection services)
        {
            return AddPriorityMediatR(services, typeof(T).Assembly);
        }

        /// <summary>
        /// Advanced configuration method
        /// İleride configuration options eklenmek istenirse buraya eklenebilir
        /// </summary>
        public static IServiceCollection AddPriorityMediatR(this IServiceCollection services,
            Action<PriorityFlowOptions> configureOptions,
            params Assembly[] assemblies)
        {
            // Configuration options'ı register et
            services.Configure(configureOptions);

            // Normal registration'ı yap
            return AddPriorityMediatR(services, assemblies);
        }
    }

    /// <summary>
    /// PriorityFlow configuration options
    /// İleride eklenmek istenebilecek configuration'lar için placeholder
    /// </summary>
    public class PriorityFlowOptions
    {
        /// <summary>
        /// Default priority when no [Priority] attribute found
        /// </summary>
        public Priority DefaultPriority { get; set; } = Priority.Normal;

        /// <summary>
        /// Maximum number of concurrent command processing
        /// </summary>
        public int MaxConcurrency { get; set; } = 1; // Single-threaded by default

        /// <summary>
        /// Queue processing interval in milliseconds
        /// </summary>
        public int ProcessingIntervalMs { get; set; } = 50;

        /// <summary>
        /// Enable detailed logging for priority operations
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = true;

        /// <summary>
        /// Timeout for command execution (optional)
        /// </summary>
        public TimeSpan? CommandTimeout { get; set; } = null;
    }
}

/*
🎯 EXTENSION METHOD PATTERN AÇIKLAMASI:

Bu dosyadaki "magic" nasıl çalışıyor?

1. EXTENSION METHOD PATTERN:
   public static IServiceCollection AddPriorityMediatR(this IServiceCollection services)
   
   "this" keyword ile Microsoft'un IServiceCollection class'ına yeni method ekliyoruz
   Artık herkes services.AddPriorityMediatR() kullanabilir

2. DEPENDENCY INJECTION OVERRIDE:
   services.AddMediatR();                    // Original MediatR'ı register et
   services.AddSingleton<IMediator, PriorityMediator>(); // IMediator'ı replace et
   
   Sonuç: IMediator isteyenler PriorityMediator alır

3. WRAPPER PATTERN:
   PriorityMediator internally original IMediator'ı kullanır
   Dışarıdan aynı interface, içeride enhanced functionality

4. ZERO BREAKING CHANGE:
   Existing code: mediator.Send(command)
   Enhanced code: mediator.Send(command) // Same call, enhanced behavior!

Bu pattern sayesinde mentor'un istediği "kod değişikliği olmadan enhancement" sağlanıyor!
*/