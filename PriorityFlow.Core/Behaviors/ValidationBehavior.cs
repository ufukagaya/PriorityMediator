// PriorityFlow.Core.Behaviors - Validation Behavior with FluentValidation
// Automatic request validation using FluentValidation library

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PriorityFlow.Behaviors
{
    /// <summary>
    /// Pipeline behavior that validates requests using FluentValidation
    /// Automatically resolves and executes all registered validators for the request type
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    public class ValidationBehavior<TRequest> : IPipelineBehavior<TRequest>
        where TRequest : IRequest
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ValidationBehavior<TRequest>> _logger;

        public ValidationBehavior(IServiceProvider serviceProvider, ILogger<ValidationBehavior<TRequest>> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handle validation for requests without response
        /// </summary>
        public async Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken)
        {
            var requestTypeName = typeof(TRequest).Name;
            
            _logger.LogDebug("🔍 Validating {RequestType}...", requestTypeName);

            await ValidateRequest(request, requestTypeName, cancellationToken);

            _logger.LogDebug("✅ {RequestType} passed validation", requestTypeName);

            await next();
        }

        /// <summary>
        /// Performs the actual validation logic
        /// </summary>
        private async Task ValidateRequest(TRequest request, string requestTypeName, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ValidationException($"Request {requestTypeName} cannot be null");
            }

            // Resolve all validators for this request type
            var validators = _serviceProvider.GetServices<IValidator<TRequest>>();
            var validatorList = validators.ToList();

            if (!validatorList.Any())
            {
                _logger.LogDebug("📝 No validators found for {RequestType} - skipping validation", requestTypeName);
                return;
            }

            _logger.LogDebug("📋 Found {ValidatorCount} validators for {RequestType}", validatorList.Count, requestTypeName);

            // Collect all validation failures
            var failures = new List<FluentValidation.Results.ValidationFailure>();
            var validationContext = new ValidationContext<TRequest>(request);

            // Execute all validators
            foreach (var validator in validatorList)
            {
                try
                {
                    var result = await validator.ValidateAsync(validationContext, cancellationToken);
                    
                    if (!result.IsValid)
                    {
                        failures.AddRange(result.Errors);
                        _logger.LogWarning("⚠️ Validation failed for {RequestType} using {ValidatorType}: {ErrorCount} errors", 
                            requestTypeName, validator.GetType().Name, result.Errors.Count);
                    }
                    else
                    {
                        _logger.LogDebug("✅ {RequestType} passed validation using {ValidatorType}", 
                            requestTypeName, validator.GetType().Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Error executing validator {ValidatorType} for {RequestType}", 
                        validator.GetType().Name, requestTypeName);
                    
                    // Add a failure for the validator error
                    failures.Add(new FluentValidation.Results.ValidationFailure(
                        "ValidationBehavior", 
                        $"Validator {validator.GetType().Name} threw an exception: {ex.Message}")
                    {
                        ErrorCode = "VALIDATOR_EXCEPTION"
                    });
                }
            }

            // If we have any failures, throw a validation exception
            if (failures.Any())
            {
                var errorDetails = string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
                
                _logger.LogError("❌ Validation failed for {RequestType}: {ErrorDetails}", requestTypeName, errorDetails);
                
                throw new ValidationException($"Validation failed for {requestTypeName}", failures);
            }
        }
    }

    /// <summary>
    /// Pipeline behavior that validates requests with response using FluentValidation
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

        public ValidationBehavior(IServiceProvider serviceProvider, ILogger<ValidationBehavior<TRequest, TResponse>> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handle validation for requests with response
        /// </summary>
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestTypeName = typeof(TRequest).Name;
            
            _logger.LogDebug("🔍 Validating {RequestType}...", requestTypeName);

            await ValidateRequest(request, requestTypeName, cancellationToken);

            _logger.LogDebug("✅ {RequestType} passed validation", requestTypeName);

            return await next();
        }

        /// <summary>
        /// Performs the actual validation logic
        /// </summary>
        private async Task ValidateRequest(TRequest request, string requestTypeName, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ValidationException($"Request {requestTypeName} cannot be null");
            }

            // Resolve all validators for this request type
            var validators = _serviceProvider.GetServices<IValidator<TRequest>>();
            var validatorList = validators.ToList();

            if (!validatorList.Any())
            {
                _logger.LogDebug("📝 No validators found for {RequestType} - skipping validation", requestTypeName);
                return;
            }

            _logger.LogDebug("📋 Found {ValidatorCount} validators for {RequestType}", validatorList.Count, requestTypeName);

            // Collect all validation failures
            var failures = new List<FluentValidation.Results.ValidationFailure>();
            var validationContext = new ValidationContext<TRequest>(request);

            // Execute all validators
            foreach (var validator in validatorList)
            {
                try
                {
                    var result = await validator.ValidateAsync(validationContext, cancellationToken);
                    
                    if (!result.IsValid)
                    {
                        failures.AddRange(result.Errors);
                        _logger.LogWarning("⚠️ Validation failed for {RequestType} using {ValidatorType}: {ErrorCount} errors", 
                            requestTypeName, validator.GetType().Name, result.Errors.Count);
                    }
                    else
                    {
                        _logger.LogDebug("✅ {RequestType} passed validation using {ValidatorType}", 
                            requestTypeName, validator.GetType().Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Error executing validator {ValidatorType} for {RequestType}", 
                        validator.GetType().Name, requestTypeName);
                    
                    // Add a failure for the validator error
                    failures.Add(new FluentValidation.Results.ValidationFailure(
                        "ValidationBehavior", 
                        $"Validator {validator.GetType().Name} threw an exception: {ex.Message}")
                    {
                        ErrorCode = "VALIDATOR_EXCEPTION"
                    });
                }
            }

            // If we have any failures, throw a validation exception
            if (failures.Any())
            {
                var errorDetails = string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
                
                _logger.LogError("❌ Validation failed for {RequestType}: {ErrorDetails}", requestTypeName, errorDetails);
                
                throw new ValidationException($"Validation failed for {requestTypeName}", failures);
            }
        }
    }

    /// <summary>
    /// Generic validation behavior that works with any request type through reflection
    /// Useful when you want to register a single behavior for all request types
    /// </summary>
    public class GenericValidationBehavior : IGenericPipelineBehavior<object, object>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GenericValidationBehavior> _logger;

        public GenericValidationBehavior(IServiceProvider serviceProvider, ILogger<GenericValidationBehavior> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handle validation for any request type using reflection
        /// </summary>
        public async Task<object> Handle(object request, RequestHandlerDelegate<object> next, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ValidationException("Request cannot be null");
            }

            var requestType = request.GetType();
            var requestTypeName = requestType.Name;
            
            _logger.LogDebug("🔍 Generic validation for {RequestType}...", requestTypeName);

            // Use reflection to find validators
            var validatorType = typeof(IValidator<>).MakeGenericType(requestType);
            var validators = _serviceProvider.GetServices(validatorType);
            var validatorList = validators.ToList();

            if (!validatorList.Any())
            {
                _logger.LogDebug("📝 No validators found for {RequestType} - skipping validation", requestTypeName);
                return await next();
            }

            _logger.LogDebug("📋 Found {ValidatorCount} validators for {RequestType}", validatorList.Count, requestTypeName);

            // Validate using reflection
            var failures = new List<FluentValidation.Results.ValidationFailure>();
            var validationContextType = typeof(ValidationContext<>).MakeGenericType(requestType);
            var validationContext = Activator.CreateInstance(validationContextType, request);

            foreach (var validator in validatorList)
            {
                try
                {
                    var validateMethod = validator.GetType().GetMethod("ValidateAsync", new[] { validationContextType, typeof(CancellationToken) });
                    if (validateMethod != null)
                    {
                        var resultTask = (Task)validateMethod.Invoke(validator, new[] { validationContext, cancellationToken })!;
                        await resultTask;

                        var result = resultTask.GetType().GetProperty("Result")?.GetValue(resultTask);
                        var isValid = (bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!;

                        if (!isValid)
                        {
                            var errors = result.GetType().GetProperty("Errors")?.GetValue(result) as IEnumerable<FluentValidation.Results.ValidationFailure>;
                            if (errors != null)
                            {
                                failures.AddRange(errors);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "💥 Error executing validator {ValidatorType} for {RequestType}", 
                        validator.GetType().Name, requestTypeName);
                    
                    failures.Add(new FluentValidation.Results.ValidationFailure(
                        "GenericValidationBehavior", 
                        $"Validator {validator.GetType().Name} threw an exception: {ex.Message}")
                    {
                        ErrorCode = "VALIDATOR_EXCEPTION"
                    });
                }
            }

            // If we have any failures, throw a validation exception
            if (failures.Any())
            {
                var errorDetails = string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
                _logger.LogError("❌ Validation failed for {RequestType}: {ErrorDetails}", requestTypeName, errorDetails);
                throw new ValidationException($"Validation failed for {requestTypeName}", failures);
            }

            _logger.LogDebug("✅ {RequestType} passed generic validation", requestTypeName);

            return await next();
        }
    }
}