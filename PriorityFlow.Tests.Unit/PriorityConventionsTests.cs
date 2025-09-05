// PriorityFlow.Tests.Unit - Priority Conventions Unit Tests
// Comprehensive test coverage for priority detection logic

using FluentAssertions;
using PriorityFlow;
using Xunit;

namespace PriorityFlow.Tests.Unit
{
    public class PriorityConventionsTests
    {
        public class GetConventionBasedPriorityTests
        {
            [Fact]
            public void Should_Return_High_Priority_For_Payment_Commands()
            {
                // Arrange
                var commandType = typeof(PaymentProcessCommand);

                // Act
                var priority = PriorityConventions.GetConventionBasedPriority(commandType);

                // Assert
                priority.Should().Be(Priority.High);
            }

            [Fact]
            public void Should_Return_High_Priority_For_Security_Commands()
            {
                // Arrange
                var commandType = typeof(SecurityValidationCommand);

                // Act
                var priority = PriorityConventions.GetConventionBasedPriority(commandType);

                // Assert
                priority.Should().Be(Priority.High);
            }

            [Fact]
            public void Should_Return_Low_Priority_For_Report_Commands()
            {
                // Arrange
                var commandType = typeof(ReportGenerationCommand);

                // Act
                var priority = PriorityConventions.GetConventionBasedPriority(commandType);

                // Assert
                priority.Should().Be(Priority.Low);
            }

            [Fact]
            public void Should_Return_Low_Priority_For_Analytics_Commands()
            {
                // Arrange
                var commandType = typeof(AnalyticsTrackingCommand);

                // Act
                var priority = PriorityConventions.GetConventionBasedPriority(commandType);

                // Assert
                priority.Should().Be(Priority.Low);
            }

            [Fact]
            public void Should_Return_Normal_Priority_For_Unknown_Commands()
            {
                // Arrange
                var commandType = typeof(UnknownBusinessCommand);

                // Act
                var priority = PriorityConventions.GetConventionBasedPriority(commandType);

                // Assert
                priority.Should().Be(Priority.Normal);
            }

            [Fact]
            public void Should_Prioritize_Explicit_Attribute_Over_Naming_Convention()
            {
                // Arrange
                var commandType = typeof(ExplicitHighPriorityReportCommand);

                // Act
                var priority = PriorityConventions.GetConventionBasedPriority(commandType);

                // Assert
                priority.Should().Be(Priority.High, "explicit Priority attribute should take precedence");
            }

            [Theory]
            [InlineData("PaymentProcessCommand", Priority.High)]
            [InlineData("PaymentRefundCommand", Priority.High)]
            [InlineData("SecurityCheckCommand", Priority.High)]
            [InlineData("AuthenticationCommand", Priority.High)]
            [InlineData("CriticalSystemCommand", Priority.High)]
            [InlineData("ReportGenerateCommand", Priority.Low)]
            [InlineData("AnalyticsCollectCommand", Priority.Low)]
            [InlineData("AuditLogCommand", Priority.Low)]
            [InlineData("EmailSendCommand", Priority.Low)]
            [InlineData("NotificationDispatchCommand", Priority.Low)]
            [InlineData("CreateUserCommand", Priority.Normal)]
            [InlineData("UpdateProductCommand", Priority.Normal)]
            [InlineData("DeleteOrderCommand", Priority.Normal)]
            public void Should_Detect_Priority_From_Command_Name_Pattern(string commandTypeName, Priority expectedPriority)
            {
                // Arrange
                var mockType = new MockCommandType(commandTypeName);

                // Act
                var priority = PriorityConventions.GetConventionBasedPriority(mockType);

                // Assert
                priority.Should().Be(expectedPriority, $"command name '{commandTypeName}' should map to {expectedPriority} priority");
            }

            [Fact]
            public void Should_Handle_Namespace_Based_Priority_Detection()
            {
                // Arrange
                var paymentCommandType = new MockCommandType("ProcessCommand", "MyApp.Payment.Commands");
                var reportCommandType = new MockCommandType("GenerateCommand", "MyApp.Reporting.Commands");
                var securityCommandType = new MockCommandType("ValidateCommand", "MyApp.Security.Commands");

                // Act
                var paymentPriority = PriorityConventions.GetConventionBasedPriority(paymentCommandType);
                var reportPriority = PriorityConventions.GetConventionBasedPriority(reportCommandType);
                var securityPriority = PriorityConventions.GetConventionBasedPriority(securityCommandType);

                // Assert
                paymentPriority.Should().Be(Priority.High, "Payment namespace should indicate high priority");
                reportPriority.Should().Be(Priority.Low, "Reporting namespace should indicate low priority");  
                securityPriority.Should().Be(Priority.High, "Security namespace should indicate high priority");
            }

            [Fact]
            public void Should_Track_Usage_Statistics()
            {
                // Arrange
                var commandType = typeof(PaymentProcessCommand);
                var initialStats = PriorityConventions.GetUsageStatistics();
                var initialCount = initialStats.Count;

                // Act
                PriorityConventions.GetConventionBasedPriority(commandType);
                PriorityConventions.GetConventionBasedPriority(commandType);

                // Assert
                var finalStats = PriorityConventions.GetUsageStatistics();
                finalStats.Should().HaveCountGreaterThan(initialCount);
            }
        }

        public class CustomConventionTests
        {
            [Fact]
            public void Should_Add_Custom_Convention_Successfully()
            {
                // Arrange
                const string keyword = "Invoice";
                const Priority priority = Priority.High;

                // Act
                PriorityConventions.AddCustomConvention(keyword, priority);
                var testType = new MockCommandType("InvoiceProcessCommand");
                var result = PriorityConventions.GetConventionBasedPriority(testType);

                // Assert
                result.Should().Be(priority);
            }

            [Fact]
            public void Should_Override_Built_In_Convention_With_Custom()
            {
                // Arrange
                const string keyword = "Report";
                const Priority customPriority = Priority.High;

                // Act
                PriorityConventions.AddCustomConvention(keyword, customPriority);
                var testType = new MockCommandType("ReportGenerateCommand");
                var result = PriorityConventions.GetConventionBasedPriority(testType);

                // Assert
                result.Should().Be(customPriority, "custom convention should override built-in");
                
                // Cleanup
                PriorityConventions.ClearCustomConventions();
            }

            [Fact]
            public void Should_Handle_Multiple_Custom_Conventions()
            {
                // Arrange
                var conventions = new Dictionary<string, Priority>
                {
                    { "Billing", Priority.High },
                    { "Archive", Priority.Low },
                    { "Maintenance", Priority.Low }
                };

                // Act
                PriorityConventions.AddCustomConventions(conventions);

                // Assert
                var billingResult = PriorityConventions.GetConventionBasedPriority(new MockCommandType("BillingProcessCommand"));
                var archiveResult = PriorityConventions.GetConventionBasedPriority(new MockCommandType("ArchiveDataCommand"));
                var maintenanceResult = PriorityConventions.GetConventionBasedPriority(new MockCommandType("MaintenanceTaskCommand"));

                billingResult.Should().Be(Priority.High);
                archiveResult.Should().Be(Priority.Low);
                maintenanceResult.Should().Be(Priority.Low);

                // Cleanup
                PriorityConventions.ClearCustomConventions();
            }
        }

        public class ConventionManagementTests
        {
            [Fact]
            public void Should_Clear_Custom_Conventions()
            {
                // Arrange
                PriorityConventions.AddCustomConvention("TestKeyword", Priority.High);
                var beforeClear = PriorityConventions.GetAllConventions();

                // Act
                PriorityConventions.ClearCustomConventions();
                var afterClear = PriorityConventions.GetAllConventions();

                // Assert
                beforeClear.Should().ContainKey("TestKeyword".ToLower());
                afterClear.Should().NotContainKey("TestKeyword".ToLower());
            }

            [Fact]
            public void Should_Return_All_Active_Conventions()
            {
                // Arrange
                PriorityConventions.ClearCustomConventions();
                PriorityConventions.AddCustomConvention("CustomKeyword", Priority.High);

                // Act
                var allConventions = PriorityConventions.GetAllConventions();

                // Assert
                allConventions.Should().NotBeEmpty();
                allConventions.Should().ContainKey("payment"); // Built-in convention
                allConventions.Should().ContainKey("customkeyword"); // Custom convention (lowercased)
                
                // Cleanup
                PriorityConventions.ClearCustomConventions();
            }
        }

        #region Test Command Types

        public record PaymentProcessCommand : IRequest<bool>;
        public record SecurityValidationCommand : IRequest<bool>;
        public record ReportGenerationCommand : IRequest<string>;
        public record AnalyticsTrackingCommand : IRequest;
        public record UnknownBusinessCommand : IRequest;

        [Priority(Priority.High)]
        public record ExplicitHighPriorityReportCommand : IRequest<string>;

        #endregion

        #region Mock Types for Testing

        /// <summary>
        /// Mock type for testing naming convention detection
        /// </summary>
        private class MockCommandType : Type
        {
            private readonly string _name;
            private readonly string _namespace;

            public MockCommandType(string name, string namespaceName = "TestNamespace")
            {
                _name = name;
                _namespace = namespaceName;
            }

            public override string Name => _name;
            public override string? Namespace => _namespace;

            // Minimal Type implementation for testing
            public override Type? BaseType => typeof(object);
            public override string? AssemblyQualifiedName => $"{_namespace}.{_name}";
            public override string? FullName => $"{_namespace}.{_name}";
            public override Guid GUID => Guid.Empty;
            public override Module Module => throw new NotImplementedException();
            public override Assembly Assembly => throw new NotImplementedException();
            public override Type UnderlyingSystemType => this;

            public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();
            public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();
            public override bool IsDefined(Type attributeType, bool inherit) => false;
            protected override TypeAttributes GetAttributeFlagsImpl() => TypeAttributes.Class;
            protected override ConstructorInfo? GetConstructorImpl(BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[] types, ParameterModifier[]? modifiers) => null;
            public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) => Array.Empty<ConstructorInfo>();
            protected override MethodInfo? GetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers) => null;
            public override MethodInfo[] GetMethods(BindingFlags bindingAttr) => Array.Empty<MethodInfo>();
            public override FieldInfo? GetField(string name, BindingFlags bindingAttr) => null;
            public override FieldInfo[] GetFields(BindingFlags bindingAttr) => Array.Empty<FieldInfo>();
            public override Type? GetInterface(string name, bool ignoreCase) => null;
            public override Type[] GetInterfaces() => Array.Empty<Type>();
            public override EventInfo? GetEvent(string name, BindingFlags bindingAttr) => null;
            public override EventInfo[] GetEvents(BindingFlags bindingAttr) => Array.Empty<EventInfo>();
            protected override PropertyInfo? GetPropertyImpl(string name, BindingFlags bindingAttr, Binder? binder, Type? returnType, Type[]? types, ParameterModifier[]? modifiers) => null;
            public override PropertyInfo[] GetProperties(BindingFlags bindingAttr) => Array.Empty<PropertyInfo>();
            public override Type[] GetNestedTypes(BindingFlags bindingAttr) => Array.Empty<Type>();
            public override Type? GetNestedType(string name, BindingFlags bindingAttr) => null;
            public override MemberInfo[] GetMembers(BindingFlags bindingAttr) => Array.Empty<MemberInfo>();
            protected override bool IsArrayImpl() => false;
            protected override bool IsByRefImpl() => false;
            protected override bool IsPointerImpl() => false;
            protected override bool IsPrimitiveImpl() => false;
            protected override bool IsCOMObjectImpl() => false;
            public override Type? GetElementType() => null;
            protected override bool HasElementTypeImpl() => false;
            public override object? InvokeMember(string name, BindingFlags invokeAttr, Binder? binder, object? target, object?[]? args, ParameterModifier[]? modifiers, CultureInfo? culture, string[]? namedParameters) => null;
        }

        #endregion
    }
}