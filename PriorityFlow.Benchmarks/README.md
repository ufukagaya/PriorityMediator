# PriorityFlow Performance Benchmarks

This project contains comprehensive performance benchmarks comparing PriorityFlow with MediatR and analyzing PriorityFlow's specific performance characteristics.

## Running Benchmarks

### Quick Start
```bash
cd PriorityFlow.Benchmarks
dotnet run -c Release
```

### Run Specific Benchmarks
```bash
# Core performance comparison
dotnet run -c Release --filter "*PerformanceComparison*"

# Priority execution benchmarks
dotnet run -c Release --filter "*PriorityExecutionBenchmark*"

# Memory usage analysis
dotnet run -c Release --filter "*MemoryUsageBenchmark*"

# Streaming performance
dotnet run -c Release --filter "*StreamingPerformanceBenchmark*"
```

### Advanced Options
```bash
# Export results to different formats
dotnet run -c Release --exporters json,html,csv

# Run with profiler
dotnet run -c Release --profiler ETW

# Custom iteration count
dotnet run -c Release --iterationCount 100
```

## Benchmark Categories

### 1. PerformanceComparison
Direct comparison between MediatR and PriorityFlow for:
- Single command execution
- Single notification publishing
- Batch command processing (10, 100, 1000 commands)
- Concurrent notifications (5, 25, 100 notifications)

### 2. PriorityExecutionBenchmark
PriorityFlow-specific performance analysis:
- Mixed priority command execution
- Convention-based priority detection
- High throughput single priority scenarios
- Complex real-world priority scenarios

### 3. MemoryUsageBenchmark
Memory allocation and GC pressure analysis:
- Memory allocation patterns comparison
- Large object handling
- Notification memory usage
- Priority detection caching effectiveness

### 4. StreamingPerformanceBenchmark
Stream processing performance:
- Simple stream processing
- Priority-aware streaming
- Batched stream operations
- Concurrent multiple streams
- Throttled and filtered streaming

## Performance Metrics

The benchmarks measure:
- **Execution Time**: Mean execution time per operation
- **Memory Allocation**: Gen 0/1/2 collections and allocated bytes
- **Throughput**: Operations per second
- **Scalability**: Performance under different load levels

## Expected Results

### Performance Expectations
- **Single Operations**: PriorityFlow should be comparable to MediatR (within 5-10% overhead)
- **Batch Operations**: PriorityFlow may show slight overhead due to priority detection
- **Memory Usage**: Should be competitive with MediatR, with minimal additional allocations
- **Priority Detection**: First-time detection has overhead, but subsequent calls are cached

### Priority Execution Benefits
- High priority commands execute before lower priority ones
- System remains responsive under load
- Critical operations get preferential treatment

## Interpreting Results

### Key Metrics to Watch
1. **Mean Execution Time**: Lower is better
2. **Allocated Memory**: Lower is better
3. **Gen 0 Collections**: Fewer is better
4. **Scalability**: Performance should scale reasonably with load

### Performance Regression Indicators
- > 20% performance degradation vs MediatR
- Excessive memory allocations
- Poor scaling characteristics
- High GC pressure

## Troubleshooting

### Common Issues
1. **Slow Benchmarks**: Ensure running in Release mode (`-c Release`)
2. **Inconsistent Results**: Run benchmarks multiple times, use `--statisticalTest` option
3. **Memory Issues**: Monitor system memory during benchmarks
4. **Antivirus Interference**: Disable antivirus during benchmark runs

### System Requirements
- .NET 8.0 or higher
- At least 4GB RAM
- SSD recommended for consistent results
- Close unnecessary applications during benchmarking

## Continuous Integration

These benchmarks can be integrated into CI/CD pipelines to:
- Monitor performance regressions
- Compare performance across versions
- Validate performance optimizations
- Generate performance reports

Example GitHub Actions workflow:
```yaml
- name: Run Benchmarks
  run: dotnet run -c Release --project PriorityFlow.Benchmarks --exporters json
  
- name: Upload Results
  uses: actions/upload-artifact@v3
  with:
    name: benchmark-results
    path: BenchmarkDotNet.Artifacts/results/*.json
```

## Contributing

When adding new benchmarks:
1. Follow existing naming conventions
2. Include both baseline (MediatR) and PriorityFlow variants
3. Add appropriate memory diagnostics
4. Document expected behavior in comments
5. Test benchmarks locally before committing