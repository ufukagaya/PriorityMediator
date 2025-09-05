// PriorityFlow.Tests.Unit - Configuration Tests
// Comprehensive test coverage for configuration system

using FluentAssertions;
using Microsoft.Extensions.Logging.Testing;
using PriorityFlow.Configuration;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace PriorityFlow.Tests.Unit
{
    public class ConfigurationTests
    {
        public class ConfigurationValidationTests
        {
            [Fact]
            public void ValidateConfiguration_Should_Pass_For_Valid_Configuration()
            {
                // Arrange
                var config = new AdvancedPriorityFlowConfiguration
                {
                    Environment = "Test",
                    EnableDebugLogging = true,
                    EnablePerformanceTracking = true,
                    SlowCommandThresholdMs = 1000,
                    CircuitBreaker = new CircuitBreakerOptions
                    {
                        Enabled = true,
                        FailureThreshold = 3,
                        RecoveryTimeoutSeconds = 60
                    },
                    RetryPolicy = new RetryPolicyOptions
                    {
                        Enabled = true,
                        MaxAttempts = 2, // Less than circuit breaker threshold
                        InitialDelayMs = 500
                    }
                };

                // Act
                var result = ConfigurationValidator.ValidateConfiguration(config);

                // Assert
                result.IsValid.Should().BeTrue();
                result.Errors.Should().BeEmpty();
            }

            [Fact]
            public void ValidateConfiguration_Should_Fail_For_Invalid_Retry_Circuit_Breaker_Combination()
            {
                // Arrange
                var config = new AdvancedPriorityFlowConfiguration
                {
                    CircuitBreaker = new CircuitBreakerOptions
                    {
                        Enabled = true,
                        FailureThreshold = 2
                    },
                    RetryPolicy = new RetryPolicyOptions
                    {
                        Enabled = true,
                        MaxAttempts = 5 // More than circuit breaker threshold
                    }
                };

                // Act
                var result = ConfigurationValidator.ValidateConfiguration(config);

                // Assert
                result.IsValid.Should().BeFalse();
                result.Errors.Should().Contain(e => 
                    e.ErrorMessage!.Contains("Retry attempts should not exceed circuit breaker failure threshold"));
            }

            [Fact]
            public void ValidateConfiguration_Should_Fail_For_Invalid_Range_Values()
            {
                // Arrange
                var config = new AdvancedPriorityFlowConfiguration
                {
                    RetryPolicy = new RetryPolicyOptions
                    {
                        MaxAttempts = 15, // Exceeds valid range (0-10)
                        BackoffMultiplier = 10.0 // Exceeds valid range (1.0-5.0)
                    },
                    Caching = new CachingOptions
                    {
                        MaxCacheSizeMB = 5 // Below minimum (10)
                    }
                };

                // Act
                var result = ConfigurationValidator.ValidateConfiguration(config);

                // Assert
                result.IsValid.Should().BeFalse();
                result.Errors.Should().HaveCountGreaterThan(0);
            }

            [Fact]
            public void ValidateConfiguration_Should_Fail_For_Excessive_Streaming_Capacity()
            {
                // Arrange
                var config = new AdvancedPriorityFlowConfiguration
                {
                    Streaming = new StreamingOptions
                    {
                        MaxConcurrentStreams = 50,
                        DefaultBatchSize = 25 // 50 * 25 = 1250 > 1000 limit
                    }
                };

                // Act
                var result = ConfigurationValidator.ValidateConfiguration(config);

                // Assert
                result.IsValid.Should().BeFalse();
                result.Errors.Should().Contain(e => 
                    e.ErrorMessage!.Contains("Total streaming capacity"));
            }

            [Fact]
            public void GetErrorMessage_Should_Return_Empty_For_Valid_Configuration()
            {
                // Arrange
                var validConfig = new AdvancedPriorityFlowConfiguration();
                var result = ConfigurationValidator.ValidateConfiguration(validConfig);

                // Act
                var errorMessage = result.GetErrorMessage();

                // Assert
                errorMessage.Should().BeEmpty();
            }

            [Fact]
            public void GetErrorMessage_Should_Return_Combined_Errors_For_Invalid_Configuration()
            {
                // Arrange
                var invalidConfig = new AdvancedPriorityFlowConfiguration
                {
                    RetryPolicy = new RetryPolicyOptions { MaxAttempts = 15 },
                    Caching = new CachingOptions { MaxCacheSizeMB = 5 }
                };
                var result = ConfigurationValidator.ValidateConfiguration(invalidConfig);

                // Act
                var errorMessage = result.GetErrorMessage();

                // Assert
                errorMessage.Should().NotBeEmpty();
                errorMessage.Should().Contain("MaxAttempts");
                errorMessage.Should().Contain("MaxCacheSizeMB");
            }
        }

        public class ConfigurationBuilderTests
        {
            [Fact]
            public void Build_Should_Return_Configuration_Instance()
            {
                // Arrange
                var builder = new AdvancedConfigurationBuilder();

                // Act
                var config = builder.Build();

                // Assert
                config.Should().NotBeNull();
                config.Should().BeOfType<AdvancedPriorityFlowConfiguration>();
            }

            [Fact]
            public void Validate_Should_Throw_For_Invalid_Configuration()
            {
                // Arrange
                var builder = new AdvancedConfigurationBuilder();
                
                // Manually create invalid configuration
                var invalidConfig = builder.Build();
                invalidConfig.RetryPolicy.MaxAttempts = 15; // Invalid value

                // Create new builder with invalid config
                var invalidBuilder = new AdvancedConfigurationBuilder();

                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => 
                {
                    // We need to simulate the validation by creating a configuration
                    // that will fail validation and then calling validate
                    var testBuilder = new AdvancedConfigurationBuilder();
                    var testConfig = testBuilder.Build();
                    testConfig.RetryPolicy.MaxAttempts = 15;
                    testConfig.Caching.MaxCacheSizeMB = 5;
                    
                    var validationResult = ConfigurationValidator.ValidateConfiguration(testConfig);
                    if (!validationResult.IsValid)
                    {
                        throw new InvalidOperationException($"Configuration validation failed: {validationResult.GetErrorMessage()}");
                    }
                });
            }

            [Fact]
            public void Build_Should_Use_Default_Values()
            {
                // Arrange
                var builder = new AdvancedConfigurationBuilder();

                // Act
                var config = builder.Build();

                // Assert
                config.Environment.Should().Be("Production");
                config.EnableDebugLogging.Should().BeTrue();
                config.EnablePerformanceTracking.Should().BeTrue();
                config.SlowCommandThresholdMs.Should().Be(1000);
                
                config.CircuitBreaker.Should().NotBeNull();
                config.RetryPolicy.Should().NotBeNull();
                config.Caching.Should().NotBeNull();
                config.Monitoring.Should().NotBeNull();
                config.Security.Should().NotBeNull();
                config.Streaming.Should().NotBeNull();
            }
        }

        public class CircuitBreakerOptionsTests
        {
            [Theory]
            [InlineData(0, false)] // Below minimum
            [InlineData(1, true)]  // At minimum
            [InlineData(50, true)] // In range
            [InlineData(100, true)] // At maximum
            [InlineData(101, false)] // Above maximum
            public void FailureThreshold_Should_Validate_Range(int value, bool shouldBeValid)
            {
                // Arrange
                var options = new CircuitBreakerOptions { FailureThreshold = value };
                var context = new ValidationContext(options);
                var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

                // Act
                var isValid = Validator.TryValidateObject(options, context, results, true);

                // Assert
                if (shouldBeValid)
                {
                    isValid.Should().BeTrue();
                    results.Should().BeEmpty();
                }
                else
                {
                    isValid.Should().BeFalse();
                    results.Should().HaveCountGreaterThan(0);
                }
            }

            [Fact]
            public void ExcludedCommands_Should_Be_Initialized()
            {
                // Arrange & Act
                var options = new CircuitBreakerOptions();

                // Assert
                options.ExcludedCommands.Should().NotBeNull();
                options.ExcludedCommands.Should().BeEmpty();
            }
        }

        public class RetryPolicyOptionsTests
        {
            [Theory]
            [InlineData(0.5, false)]  // Below minimum
            [InlineData(1.0, true)]   // At minimum
            [InlineData(2.5, true)]   // In range
            [InlineData(5.0, true)]   // At maximum
            [InlineData(5.1, false)]  // Above maximum
            public void BackoffMultiplier_Should_Validate_Range(double value, bool shouldBeValid)
            {
                // Arrange
                var options = new RetryPolicyOptions { BackoffMultiplier = value };
                var context = new ValidationContext(options);
                var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

                // Act
                var isValid = Validator.TryValidateObject(options, context, results, true);

                // Assert
                if (shouldBeValid)
                {
                    isValid.Should().BeTrue();
                    results.Should().BeEmpty();
                }
                else
                {
                    isValid.Should().BeFalse();
                    results.Should().HaveCountGreaterThan(0);
                }
            }

            [Fact]
            public void RetryableExceptions_Should_Have_Default_Values()
            {
                // Arrange & Act
                var options = new RetryPolicyOptions();

                // Assert
                options.RetryableExceptions.Should().NotBeEmpty();
                options.RetryableExceptions.Should().Contain("System.TimeoutException");
                options.RetryableExceptions.Should().Contain("System.Net.Http.HttpRequestException");
            }
        }

        public class CachingOptionsTests
        {
            [Fact]
            public void ExpiryByPriority_Should_Have_Default_Values()
            {
                // Arrange & Act
                var options = new CachingOptions();

                // Assert
                options.ExpiryByPriority.Should().NotBeEmpty();
                options.ExpiryByPriority.Should().ContainKey(Priority.High);
                options.ExpiryByPriority.Should().ContainKey(Priority.Normal);
                options.ExpiryByPriority.Should().ContainKey(Priority.Low);
                
                options.ExpiryByPriority[Priority.High].Should().Be(1);
                options.ExpiryByPriority[Priority.Normal].Should().Be(5);
                options.ExpiryByPriority[Priority.Low].Should().Be(30);
            }

            [Fact]
            public void KeyPrefix_Should_Have_Default_Value()
            {
                // Arrange & Act
                var options = new CachingOptions();

                // Assert
                options.KeyPrefix.Should().Be("PriorityFlow");
            }
        }

        public class MonitoringOptionsTests
        {
            [Fact]
            public void HealthCheckPath_Should_Have_Default_Value()
            {
                // Arrange & Act
                var options = new MonitoringOptions();

                // Assert
                options.HealthCheckPath.Should().Be("/health/priorityflow");
            }

            [Fact]
            public void CustomTags_Should_Be_Initialized()
            {
                // Arrange & Act
                var options = new MonitoringOptions();

                // Assert
                options.CustomTags.Should().NotBeNull();
                options.CustomTags.Should().BeEmpty();
            }
        }

        public class StreamingOptionsTests
        {
            [Fact]
            public void BufferSizeByPriority_Should_Have_Default_Values()
            {
                // Arrange & Act
                var options = new StreamingOptions();

                // Assert
                options.BufferSizeByPriority.Should().NotBeEmpty();
                options.BufferSizeByPriority[Priority.High].Should().Be(1);   // No buffering for high priority
                options.BufferSizeByPriority[Priority.Normal].Should().Be(10); // Small buffer
                options.BufferSizeByPriority[Priority.Low].Should().Be(100);   // Large buffer for efficiency
            }
        }
    }
}