namespace ScrollShot.Tooling.Models;

public sealed class BenchmarkCommandOptions
{
    public required string SuitePath { get; init; }

    public string? OutputDirectory { get; init; }
}
