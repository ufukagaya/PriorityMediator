# Changelog

All notable changes to PriorityFlow will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2024-01-XX

### 🎉 Initial Release

#### ✨ Added
- **Core Features**
  - Drop-in replacement for MediatR with 100% API compatibility
  - Intelligent priority detection using naming conventions
  - Support for `IRequest`, `IRequest<TResponse>`, and `INotification`
  - Pipeline behaviors for cross-cutting concerns
  - Stream processing with `IStreamRequest<TResponse>`

- **Priority System**
  - Automatic priority detection based on command/query names
  - Convention-based priority mapping (Payment→High, Report→Low, etc.)
  - Custom priority conventions support
  - `[Priority]` attribute for explicit priority control
  - Priority statistics and usage tracking

- **Performance Features**
  - High-performance implementation optimized for throughput
  - Built-in caching for priority detection
  - Memory-efficient request handling
  - Comprehensive performance benchmarking

- **Developer Experience**
  - Zero-configuration setup with `services.AddPriorityFlow()`
  - Rich debugging support with detailed logging
  - Comprehensive XML documentation
  - IntelliSense-friendly APIs
  - Helpful error messages and diagnostics

- **Pipeline Behaviors**
  - `LoggingBehavior<TRequest, TResponse>` - Request/response logging
  - `PerformanceMonitoringBehavior<TRequest, TResponse>` - Performance tracking
  - `ValidationBehavior<TRequest, TResponse>` - Request validation
  - `RetryBehavior<TRequest, TResponse>` - Automatic retry logic
  - `CachingBehavior<TRequest, TResponse>` - Response caching

- **Configuration**
  - Fluent configuration API
  - Debug logging control
  - Performance monitoring settings
  - Custom priority conventions
  - Behavior pipeline configuration

- **Testing Support**
  - Comprehensive unit test suite (100% coverage)
  - Integration tests for real-world scenarios
  - Performance benchmarks vs MediatR
  - Memory allocation testing
  - Concurrency testing

#### 🏗️ Infrastructure
- **Package Quality**
  - Professional NuGet package with rich metadata
  - Source symbols (`.snupkg`) for debugging
  - SourceLink integration for source code access
  - MIT license
  - Comprehensive README and documentation

- **CI/CD Pipeline**
  - GitHub Actions for automated testing
  - Performance regression detection
  - Multi-platform compatibility testing
  - Automatic NuGet package deployment
  - Code quality gates with SonarCloud
  - Security vulnerability scanning

- **Documentation**
  - Complete API documentation
  - Usage examples and best practices
  - Performance benchmarking results
  - Migration guide from MediatR
  - Contributing guidelines

#### 📦 Dependencies
- `Microsoft.Extensions.DependencyInjection.Abstractions` (8.0.0)
- `Microsoft.Extensions.Logging.Abstractions` (8.0.0)
- Compatible with .NET 8.0+

#### 🚀 Performance Characteristics
- ~5% overhead compared to MediatR for simple scenarios
- Significant benefits for priority-aware applications
- Memory-efficient with minimal allocations
- Scales well under high concurrency
- Built-in performance monitoring and metrics

---

## Future Releases

### Planned Features
- [ ] Distributed priority coordination
- [ ] Advanced caching strategies
- [ ] Health check endpoints
- [ ] Metrics and monitoring integrations
- [ ] Additional pipeline behaviors
- [ ] Multi-framework targeting (.NET 6, 7)

### Contributing
We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### Support
- 📖 [Documentation](https://priorityflow.dev/docs)
- 🐛 [Issues](https://github.com/priorityflow/priorityflow/issues)
- 💬 [Discussions](https://github.com/priorityflow/priorityflow/discussions)