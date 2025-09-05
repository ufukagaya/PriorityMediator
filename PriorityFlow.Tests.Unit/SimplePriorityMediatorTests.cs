// PriorityFlow.Tests.Unit - SimplePriorityMediator Unit Tests
// Comprehensive test coverage for mediator functionality

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using PriorityFlow;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PriorityFlow.Tests.Unit
{
    public class SimplePriorityMediatorTests
    {
        public class SendCommandTests
        {
            private readonly Mock<IServiceProvider> _serviceProviderMock;
            private readonly FakeLogger<SimplePriorityMediator> _logger;
            private readonly PriorityFlowConfiguration _configuration;
            private readonly SimplePriorityMediator _mediator;

            public SendCommandTests()
            {
                _serviceProviderMock = new Mock<IServiceProvider>();
                _logger = new FakeLogger<SimplePriorityMediator>();
                _configuration = new PriorityFlowConfiguration { EnableDebugLogging = true };
                _mediator = new SimplePriorityMediator(_serviceProviderMock.Object, _logger, _configuration);
            }

            [Fact]
            public async Task Send_Should_Execute_Handler_For_Request_Without_Response()
            {
                // Arrange
                var command = new TestCommand("test-data");
                var handlerMock = new Mock<IRequestHandler<TestCommand>>();
                
                _serviceProviderMock
                    .Setup(sp => sp.GetService(typeof(IRequestHandler<TestCommand>)))
                    .Returns(handlerMock.Object);

                // Act
                await _mediator.Send(command);

                // Assert
                handlerMock.Verify(h => h.Handle(command, It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task Send_Should_Execute_Handler_For_Request_With_Response()
            {
                // Arrange
                var query = new TestQuery("test-data");
                var expectedResult = "test-result";
                var handlerMock = new Mock<IRequestHandler<TestQuery, string>>();
                
                handlerMock
                    .Setup(h => h.Handle(query, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expectedResult);

                _serviceProviderMock
                    .Setup(sp => sp.GetService(typeof(IRequestHandler<TestQuery, string>)))
                    .Returns(handlerMock.Object);

                // Act
                var result = await _mediator.Send(query);

                // Assert
                result.Should().Be(expectedResult);
                handlerMock.Verify(h => h.Handle(query, It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task Send_Should_Log_Debug_Information_When_Enabled()
            {
                // Arrange
                var command = new TestCommand("test-data");
                var handlerMock = new Mock<IRequestHandler<TestCommand>>();
                
                _serviceProviderMock
                    .Setup(sp => sp.GetService(typeof(IRequestHandler<TestCommand>)))
                    .Returns(handlerMock.Object);

                // Act
                await _mediator.Send(command);

                // Assert
                _logger.Collector.GetSnapshot().Should().Contain(log => 
                    log.Message.Contains("Executing TestCommand with Priority.Normal"));
                _logger.Collector.GetSnapshot().Should().Contain(log => 
                    log.Message.Contains("TestCommand completed in"));
            }

            [Fact]
            public async Task Send_Should_Not_Log_Debug_When_Disabled()
            {
                // Arrange
                var configuration = new PriorityFlowConfiguration { EnableDebugLogging = false };
                var mediator = new SimplePriorityMediator(_serviceProviderMock.Object, _logger, configuration);
                
                var command = new TestCommand("test-data");
                var handlerMock = new Mock<IRequestHandler<TestCommand>>();
                
                _serviceProviderMock
                    .Setup(sp => sp.GetService(typeof(IRequestHandler<TestCommand>)))
                    .Returns(handlerMock.Object);

                // Act
                await _mediator.Send(command);

                // Assert
                _logger.Collector.GetSnapshot().Should().NotContain(log => 
                    log.Message.Contains("Executing TestCommand"));
            }

            [Fact]
            public async Task Send_Should_Throw_Helpful_Exception_When_Handler_Not_Found()
            {
                // Arrange
                var command = new TestCommand("test-data");
                
                _serviceProviderMock
                    .Setup(sp => sp.GetService(typeof(IRequestHandler<TestCommand>)))
                    .Throws(new InvalidOperationException("No service for type"));

                // Act & Assert
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _mediator.Send(command));

                exception.Message.Should().Contain("Handler not found for TestCommand");
                exception.Message.Should().Contain("Make sure you registered it");
            }

            [Fact]
            public async Task Send_Should_Respect_Cancellation_Token()
            {
                // Arrange
                var command = new TestCommand("test-data");
                var handlerMock = new Mock<IRequestHandler<TestCommand>>();
                var cancellationToken = new CancellationToken(true); // Already cancelled
                
                handlerMock
                    .Setup(h => h.Handle(command, cancellationToken))
                    .ThrowsAsync(new OperationCanceledException());

                _serviceProviderMock
                    .Setup(sp => sp.GetService(typeof(IRequestHandler<TestCommand>)))
                    .Returns(handlerMock.Object);

                // Act & Assert
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => _mediator.Send(command, cancellationToken));
            }

            [Theory]
            [InlineData(typeof(PaymentCommand), Priority.High)]
            [InlineData(typeof(ReportCommand), Priority.Low)]
            [InlineData(typeof(BusinessCommand), Priority.Normal)]
            public async Task Send_Should_Detect_Priority_Correctly(Type commandType, Priority expectedPriority)
            {
                // Arrange
                var command = (IRequest)Activator.CreateInstance(commandType)!;
                var handlerType = typeof(IRequestHandler<>).MakeGenericType(commandType);
                var handlerMock = new Mock<IRequestHandler<IRequest>>();
                
                _serviceProviderMock
                    .Setup(sp => sp.GetService(handlerType))
                    .Returns(handlerMock.Object);

                // Act
                await _mediator.Send(command);

                // Assert
                _logger.Collector.GetSnapshot().Should().Contain(log => 
                    log.Message.Contains($"Priority.{expectedPriority}"));
            }
        }

        public class PublishNotificationTests
        {
            private readonly Mock<IServiceProvider> _serviceProviderMock;
            private readonly FakeLogger<SimplePriorityMediator> _logger;
            private readonly SimplePriorityMediator _mediator;

            public PublishNotificationTests()
            {
                _serviceProviderMock = new Mock<IServiceProvider>();
                _logger = new FakeLogger<SimplePriorityMediator>();
                var configuration = new PriorityFlowConfiguration { EnableDebugLogging = true };
                _mediator = new SimplePriorityMediator(_serviceProviderMock.Object, _logger, configuration);
            }

            [Fact]
            public async Task Publish_Should_Execute_All_Handlers()
            {
                // Arrange
                var notification = new TestNotification("test-data");
                var handler1Mock = new Mock<INotificationHandler<TestNotification>>();
                var handler2Mock = new Mock<INotificationHandler<TestNotification>>();
                
                var handlers = new INotificationHandler<TestNotification>[] { handler1Mock.Object, handler2Mock.Object };
                
                _serviceProviderMock
                    .Setup(sp => sp.GetServices(typeof(INotificationHandler<TestNotification>)))
                    .Returns(handlers);

                // Act
                await _mediator.Publish(notification);

                // Assert
                handler1Mock.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
                handler2Mock.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
            }

            [Fact]
            public async Task Publish_Should_Log_Handler_Count()
            {
                // Arrange
                var notification = new TestNotification("test-data");
                var handler1Mock = new Mock<INotificationHandler<TestNotification>>();
                var handler2Mock = new Mock<INotificationHandler<TestNotification>>();
                
                var handlers = new INotificationHandler<TestNotification>[] { handler1Mock.Object, handler2Mock.Object };
                
                _serviceProviderMock
                    .Setup(sp => sp.GetServices(typeof(INotificationHandler<TestNotification>)))
                    .Returns(handlers);

                // Act
                await _mediator.Publish(notification);

                // Assert
                _logger.Collector.GetSnapshot().Should().Contain(log => 
                    log.Message.Contains("Publishing TestNotification to 2 handlers"));
            }

            [Fact]
            public async Task Publish_Should_Handle_No_Handlers_Gracefully()
            {
                // Arrange
                var notification = new TestNotification("test-data");
                
                _serviceProviderMock
                    .Setup(sp => sp.GetServices(typeof(INotificationHandler<TestNotification>)))
                    .Returns(Enumerable.Empty<INotificationHandler<TestNotification>>());

                // Act & Assert
                await _mediator.Publish(notification); // Should not throw
            }
        }

        public class ObjectBasedSendTests
        {
            private readonly Mock<IServiceProvider> _serviceProviderMock;
            private readonly FakeLogger<SimplePriorityMediator> _logger;
            private readonly SimplePriorityMediator _mediator;

            public ObjectBasedSendTests()
            {
                _serviceProviderMock = new Mock<IServiceProvider>();
                _logger = new FakeLogger<SimplePriorityMediator>();
                var configuration = new PriorityFlowConfiguration();
                _mediator = new SimplePriorityMediator(_serviceProviderMock.Object, _logger, configuration);
            }

            [Fact]
            public async Task Send_Object_Should_Handle_Request_With_Response()
            {
                // Arrange
                object query = new TestQuery("test-data");
                var expectedResult = "test-result";
                var handlerMock = new Mock<IRequestHandler<TestQuery, string>>();
                
                handlerMock
                    .Setup(h => h.Handle((TestQuery)query, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expectedResult);

                _serviceProviderMock
                    .Setup(sp => sp.GetService(typeof(IRequestHandler<TestQuery, string>)))
                    .Returns(handlerMock.Object);

                // Act
                var result = await _mediator.Send(query);

                // Assert
                result.Should().Be(expectedResult);
            }

            [Fact]
            public async Task Send_Object_Should_Handle_Request_Without_Response()
            {
                // Arrange
                object command = new TestCommand("test-data");
                var handlerMock = new Mock<IRequestHandler<TestCommand>>();
                
                _serviceProviderMock
                    .Setup(sp => sp.GetService(typeof(IRequestHandler<TestCommand>)))
                    .Returns(handlerMock.Object);

                // Act
                var result = await _mediator.Send(command);

                // Assert
                result.Should().BeNull();
                handlerMock.Verify(h => h.Handle((TestCommand)command, It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        public class StreamSupportTests
        {
            private readonly Mock<IServiceProvider> _serviceProviderMock;
            private readonly FakeLogger<SimplePriorityMediator> _logger;
            private readonly SimplePriorityMediator _mediator;

            public StreamSupportTests()
            {
                _serviceProviderMock = new Mock<IServiceProvider>();
                _logger = new FakeLogger<SimplePriorityMediator>();
                var configuration = new PriorityFlowConfiguration();
                _mediator = new SimplePriorityMediator(_serviceProviderMock.Object, _logger, configuration);
            }

            [Fact]
            public async Task CreateStream_Should_Return_Simple_Stream()
            {
                // Arrange
                var request = new TestStreamRequest();

                // Act
                var stream = _mediator.CreateStream(request);

                // Assert
                var items = new List<object?>();
                await foreach (var item in stream)
                {
                    items.Add(item);
                }

                items.Should().HaveCount(1);
                items.First().Should().Be(request);
            }
        }

        #region Test Types

        public record TestCommand(string Data) : IRequest;
        public record TestQuery(string Data) : IRequest<string>;
        public record TestNotification(string Data) : INotification;
        public record TestStreamRequest : IStreamRequest<string>;

        // Priority test commands
        public record PaymentCommand : IRequest;
        public record ReportCommand : IRequest;
        public record BusinessCommand : IRequest;

        #endregion
    }
}