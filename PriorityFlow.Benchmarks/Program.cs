// PriorityFlow.Benchmarks - Performance Comparison Suite
// Comprehensive benchmarks comparing PriorityFlow vs MediatR

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using PriorityFlow.Benchmarks;

var config = DefaultConfig.Instance
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);